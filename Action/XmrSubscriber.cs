/**
 * Copyright (C) 2025 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - https://xibosignage.com
 *
 * This file is part of Xibo.
 *
 * Xibo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version.
 *
 * Xibo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with Xibo.  If not, see <http://www.gnu.org/licenses/>.
 */
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using XiboClient.Control;
using XiboClient.Error;
using XiboClient.Log;
using XiboClient.Logic;
using WebSocketSharp;
using System.Security.Authentication;

namespace XiboClient.Action
{
    class XmrSubscriber
    {
        public static object _locker = new object();

        // Members to stop the thread
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        /// <summary>
        /// Last Heartbeat packet received
        /// Assume a successful connection so that a check doesn't immediately tear down the socket.
        /// </summary>
        public DateTime LastHeartBeat = DateTime.Now;

        // Events
        public delegate void OnActionDelegate(PlayerActionInterface action);
        public event OnActionDelegate OnAction;

        /// <summary>
        /// Client Hardware key
        /// </summary>
        public HardwareKey HardwareKey
        {
            set
            {
                _hardwareKey = value;
            }
        }
        private HardwareKey _hardwareKey;

        /// <summary>
        /// A WebSocket Client
        /// </summary>
        private WebSocket _webSocket;

        /// <summary>
        /// The MQ Poller
        /// </summary>
        private NetMQPoller _poller;

        /// <summary>
        /// Guards access to _webSocket. Restart()/Stop() run on other threads and can release
        /// the socket from underneath LoopForWs() while it is still being set up.
        /// </summary>
        private readonly object _socketLock = new object();

        /// <summary>
        /// The last status we logged, so that we don't write the same line every 60 seconds.
        /// </summary>
        private string _lastLoggedStatus;

        /// <summary>
        /// Runs the agent
        /// </summary>
        public void Run()
        {
            Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Thread Started"), LogType.Info.ToString());

            while (!_forceStop)
            {
                lock (_locker)
                {
                    try
                    {
                        // If we are restarting, reset
                        _manualReset.Reset();

                        // Check XMR is configured. Note that in web socket mode this deliberately
                        // does not depend on XmrNetworkAddress, which is a legacy ZMQ only setting.
                        if (IsXmrConfigured())
                        {
                            // Decide whether we are connecting to a web socket based implementation, or a legacy ZMQ one.
                            if (IsWebSocket())
                            {
                                LoopForWs();
                            }
                            else
                            {
                                LoopForZmq();

                                SetStatus("Disconnected, waiting to reconnect, last activity: " + LastHeartBeat.ToString(), LogType.Info);
                            }
                        }
                        else
                        {
                            ReportNotConfigured();
                        }
                    }
                    catch (XmrConfigurationException configEx)
                    {
                        // Not transient - the CMS has given us something we cannot use, so retrying
                        // will not help. Log at error so it survives the default LogLevel of error.
                        SetStatus("Configuration error: " + configEx.Message, LogType.Error);
                    }
                    catch (TerminatingException terminatingEx)
                    {
                        Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "NetMQ terminating: " + terminatingEx.Message), LogType.Audit.ToString());
                    }
                    catch (Exception e)
                    {
                        // This used to log at Info, which is discarded at the default LogLevel of
                        // error, so a failure to connect to XMR left no trace at all.
                        SetStatus("Unable to connect to XMR at [" + GetAddressForStatus() + "]: " + e.Message, LogType.Error);
                        Trace.WriteLine(new LogMessage("XmrSubscriber - Run", e.ToString()), LogType.Audit.ToString());
                    }

                    // Sleep for 60 seconds.
                    _manualReset.WaitOne(60 * 1000);
                }
            }

            Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Subscriber Stopped"), LogType.Info.ToString());
        }

        /// <summary>
        /// Are we using the web socket transport?
        /// Tolerant of whitespace and case - this value arrives from the CMS as free text.
        /// </summary>
        public static bool IsWebSocket()
        {
            return string.Equals(
                (ApplicationSettings.Default.XmrType ?? string.Empty).Trim(),
                "ws",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Has XMR been explicitly disabled?
        /// The CMS uses the magic value DISABLED, which may appear in either address field
        /// depending on which transport that CMS is configured for. We honour both, so that a
        /// display which was disabled under ZMQ doesn't silently start connecting after upgrade.
        /// </summary>
        public static bool IsXmrDisabled()
        {
            return string.Equals((ApplicationSettings.Default.XmrNetworkAddress ?? string.Empty).Trim(), "DISABLED", StringComparison.OrdinalIgnoreCase)
                || string.Equals((ApplicationSettings.Default.XmrWebSocketAddress ?? string.Empty).Trim(), "DISABLED", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Is XMR configured?
        ///
        /// ws  - we never need XmrNetworkAddress. The web socket address is either supplied by
        ///       the CMS or derived from the CMS address, so ws is configured unless it has been
        ///       explicitly disabled. Requiring XmrNetworkAddress here is what stopped web socket
        ///       XMR running at all against a CMS which leaves the legacy XMR Public Address empty.
        /// zmq - we must have an XmrNetworkAddress.
        /// </summary>
        public static bool IsXmrConfigured()
        {
            if (IsXmrDisabled())
            {
                return false;
            }

            if (IsWebSocket())
            {
                return true;
            }

            return !string.IsNullOrEmpty((ApplicationSettings.Default.XmrNetworkAddress ?? string.Empty).Trim());
        }

        /// <summary>
        /// Set the status shown on the info screen and in status.json, logging it if it changed.
        /// </summary>
        /// <param name="status">the status text</param>
        /// <param name="logType">the level to log a change at</param>
        private void SetStatus(string status, LogType logType)
        {
            ClientInfo.Instance.XmrSubscriberStatus = status;

            // Only log when the status actually changes. Run() loops every 60 seconds and we
            // don't want to fill the CMS log with the same line over and over.
            if (_lastLoggedStatus != status)
            {
                _lastLoggedStatus = status;
                Trace.WriteLine(new LogMessage("XmrSubscriber - Status", status), logType.ToString());
            }
        }

        /// <summary>
        /// Explain why XMR isn't running, at a level appropriate to whether that is deliberate.
        /// </summary>
        private void ReportNotConfigured()
        {
            if (IsXmrDisabled())
            {
                SetStatus("Disabled by the CMS", LogType.Audit);
            }
            else if (!string.IsNullOrEmpty((ApplicationSettings.Default.XmrWebSocketAddress ?? string.Empty).Trim()))
            {
                // The CMS gave us a web socket address but hasn't put us in ws mode, which the
                // user needs to see. Log at error so it survives the default LogLevel and gets
                // uploaded to the CMS by the LogAgent.
                SetStatus("Not configured: the CMS supplied a web socket address ["
                    + ApplicationSettings.Default.XmrWebSocketAddress
                    + "] but xmrType is [" + ApplicationSettings.Default.XmrType
                    + "], expected [ws]", LogType.Error);
            }
            else
            {
                SetStatus("Not configured (xmrType: [" + ApplicationSettings.Default.XmrType
                    + "], xmrNetworkAddress is empty)", LogType.Audit);
            }
        }

        private void LoopForWs()
        {
            // Resolve and validate the address before we take the lock or touch the library, so
            // that a configuration problem throws XmrConfigurationException up to Run() with a
            // clear message instead of dying inside the WebSocket constructor.
            string address = GetWsAddress();

            WebSocket socket;

            lock (_socketLock)
            {
                if (_webSocket != null && _webSocket.IsAlive)
                {
                    return;
                }

                // If there is an old, dead socket we must fully release it before replacing.
                // Previously we overwrote _webSocket without detaching handlers or disposing,
                // which leaked a WebSocket (and 4 delegate references back to this subscriber)
                // every 60 seconds whenever XMR was unavailable.
                ReleaseWebSocket();

                // Set the status directly rather than through SetStatus. This alternates with the
                // failure status on every retry, and going through SetStatus would reset the
                // de-duplication and log the same connection error once a minute forever.
                ClientInfo.Instance.XmrSubscriberStatus = "Connecting to " + address;
                LogMessage.Audit("XmrSubscriber", "LoopForWs", "Connecting to " + address);

                socket = new WebSocket(address);
                socket.SslConfiguration.EnabledSslProtocols = GetEnabledSslProtocols();
                socket.OnOpen += _webSocket_OnOpen;
                socket.OnClose += _webSocket_OnClose;
                socket.OnMessage += _webSocket_OnMessage;
                socket.OnError += _webSocket_OnError;

                // Publish before connecting - OnOpen can fire synchronously from Connect().
                _webSocket = socket;
            }

            // Connect outside the lock. Connect() blocks until the handshake completes or the
            // TCP connect times out, and holding _socketLock across that would stall Restart()
            // and Stop() for the duration. We hold a local reference, so a concurrent Restart()
            // releasing _webSocket just makes this Connect() fail, which is what we want.
            socket.Connect();
        }

        /// <summary>
        /// The SSL/TLS protocol versions we offer for wss:// connections.
        /// </summary>
        private static SslProtocols GetEnabledSslProtocols()
        {
            // We assign rather than OR. websocket-sharp defaults EnabledSslProtocols to
            // SslProtocols.Default, which is Ssl3 | Tls, so OR-ing Tls12 in gave us
            // Ssl3 | Tls1.0 | Tls1.2 - advertising SSL 3.0, and missing TLS 1.1 entirely.
            // Advertising SSLv3 is the part modern reverse proxies and CDNs object to, and
            // Schannel may refuse outright where SSL 3.0 is disabled by policy.
            //
            // So we drop SSL 3.0 and fill in the missing 1.1, but deliberately keep TLS 1.0 and
            // 1.1: removing them would break any wss:// endpoint that can't do 1.2, which would
            // be a breaking change for something that used to work. They should be dropped in a
            // future release with a release note, not here.
            //
            // SslProtocols.Tls13 is not available on .NET Framework 4.7.2 (it was added in 4.8)
            // and we are not moving the framework version of this player, so 1.2 is our ceiling.
            // If TLS 1.3 becomes a requirement the answer is to move off websocket-sharp to
            // ClientWebSocket, which uses Schannel's own policy.
            return SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
        }

        /// <summary>
        /// Detach handlers and dispose the current WebSocket.
        /// </summary>
        private void ReleaseWebSocket()
        {
            if (_webSocket == null)
            {
                return;
            }

            try
            {
                _webSocket.OnOpen -= _webSocket_OnOpen;
                _webSocket.OnClose -= _webSocket_OnClose;
                _webSocket.OnMessage -= _webSocket_OnMessage;
                _webSocket.OnError -= _webSocket_OnError;
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - ReleaseWebSocket", "Detach handlers failed: " + e.Message), LogType.Audit.ToString());
            }

            try
            {
                _webSocket.Close();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - ReleaseWebSocket", "Close failed: " + e.Message), LogType.Audit.ToString());
            }

            try
            {
                ((IDisposable)_webSocket).Dispose();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - ReleaseWebSocket", "Dispose failed: " + e.Message), LogType.Audit.ToString());
            }

            _webSocket = null;
        }

        private void _webSocket_OnOpen(object sender, EventArgs e)
        {
            LogMessage.Audit("XmrSubscriber", "_webSocket_OnOpen", "Open");

            ClientInfo.Instance.XmrSubscriberStatus = "XMR web socket open, sending handshake";

            // Send the init message.
            JObject message = new JObject
            {
                { "type", "init" },
                { "key", ApplicationSettings.Default.XmrCmsKey },
                { "channel", _hardwareKey.Channel }
            };

            // Send via sender rather than the field, so that a concurrent Restart() replacing
            // _webSocket cannot make us send the handshake on the wrong socket.
            ((WebSocket)sender).Send(message.ToString());
        }

        private void _webSocket_OnClose(object sender, CloseEventArgs e)
        {
            string reason = e.Reason;
            if (reason.IsNullOrEmpty())
            {
                reason = e.Code.ToString();
            }

            LogMessage.Audit("XmrSubscriber", "_webSocket_OnClose", reason);

            ClientInfo.Instance.XmrSubscriberStatus = "Disconnected, waiting to reconnect, reason: " + reason + " last activity: " + LastHeartBeat.ToString();
        }

        private void _webSocket_OnError(object sender, ErrorEventArgs e)
        {
            // e.Exception carries the real reason (TLS handshake failure, connection refused,
            // name resolution). We used to throw it away and log only the library's generic
            // message, which left no way to tell those apart.
            string detail = e.Message;
            Exception ex = e.Exception;
            while (ex != null)
            {
                detail += " -> " + ex.GetType().Name + ": " + ex.Message;
                ex = ex.InnerException;
            }

            SetStatus("Error connecting to XMR at [" + GetAddressForStatus() + "]: " + detail, LogType.Error);

            if (e.Exception != null)
            {
                LogMessage.Audit("XmrSubscriber", "_webSocket_OnError", e.Exception.ToString());
            }
        }

        private void _webSocket_OnMessage(object sender, MessageEventArgs e)
        {
            LogMessage.Audit("XmrSubscriber", "_webSocket_OnMessage", "Received");

            if (e.IsText)
            {
                UpdateStatus();

                if (e.Data.Equals("H"))
                {
                    LastHeartBeat = DateTime.Now;
                }
                else
                {
                    ProcessMessage(e.Data);
                }
            }
            else
            {
                LogMessage.Audit("XmrSubscriber", "_webSocket_OnMessage", "Not text");
            }
        }

        /// <summary>
        /// Get the web socket address we should connect to, normalised and validated.
        ///
        /// Deliberately side effect free (no logging) - this is called from the status paths on
        /// every heartbeat as well as at connect time. The resolved address is logged once by
        /// LoopForWs when it actually connects.
        /// </summary>
        /// <returns>a ws:// or wss:// address</returns>
        /// <exception cref="XmrConfigurationException">if the configured address can't be used</exception>
        public static string GetWsAddress()
        {
            string address = (ApplicationSettings.Default.XmrWebSocketAddress ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(address))
            {
                // Derive from the CMS address. TrimEnd('/') so that a CMS address of
                // https://cms.example.com/ doesn't produce wss://cms.example.com//xmr, and
                // StartsWith(OrdinalIgnoreCase) so that HTTPS:// is handled - the string.Replace
                // this used to do was case sensitive and did neither.
                string cms = (ApplicationSettings.Default.ServerUri ?? string.Empty).Trim().TrimEnd('/');

                if (cms.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    address = "wss://" + cms.Substring("https://".Length);
                }
                else if (cms.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    address = "ws://" + cms.Substring("http://".Length);
                }
                else
                {
                    throw new XmrConfigurationException("No XMR web socket address is set and the CMS address ["
                        + cms + "] isn't a http(s) URL, so one can't be derived.");
                }

                // Append /xmr to the CMS address
                address += "/xmr";
            }
            else if (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Be forgiving of a http(s):// URL pasted into the CMS web socket address field.
                address = "wss://" + address.Substring("https://".Length);
            }
            else if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                address = "ws://" + address.Substring("http://".Length);
            }

            // Validate before we hand it to the library, so that a bad address gives a clear
            // message instead of an opaque ArgumentException out of the WebSocket constructor.
            Uri uri;
            if (!Uri.TryCreate(address, UriKind.Absolute, out uri))
            {
                throw new XmrConfigurationException("XMR web socket address isn't a valid absolute URL: [" + address + "]");
            }

            if (!uri.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase)
                && !uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
            {
                throw new XmrConfigurationException("XMR web socket address must use ws:// or wss://, got [" + address + "]");
            }

            if (string.IsNullOrEmpty(uri.Host))
            {
                throw new XmrConfigurationException("XMR web socket address has no host: [" + address + "]");
            }

            // Return the trimmed string rather than uri.AbsoluteUri, so that we don't silently
            // re-encode a path the CMS deliberately chose.
            return address;
        }

        /// <summary>
        /// The address we are, or would be, connected to. For status and logging only, so this
        /// reports a resolution failure rather than throwing.
        /// </summary>
        public static string GetAddressForStatus()
        {
            try
            {
                return IsWebSocket() ? GetWsAddress() : ApplicationSettings.Default.XmrNetworkAddress;
            }
            catch (Exception e)
            {
                return "unresolved (" + e.Message + ")";
            }
        }

        /// <summary>
        /// Legacy loop for ZMQ
        /// </summary>
        private void LoopForZmq()
        {
            // Get the Private Key
            AsymmetricCipherKeyPair rsaKey = _hardwareKey.getXmrKey();

            // Connect to XMR
            try
            {
                // Create a Poller
                _poller = new NetMQPoller();

                // Create a Socket
                using (SubscriberSocket socket = new SubscriberSocket())
                {
                    // Options
                    socket.Options.ReconnectInterval = TimeSpan.FromSeconds(5);
                    socket.Options.Linger = TimeSpan.FromSeconds(0);

                    // Bind
                    socket.Connect(ApplicationSettings.Default.XmrNetworkAddress);
                    socket.Subscribe("H");
                    socket.Subscribe(_hardwareKey.Channel);

                    // Add Socket to Poller
                    _poller.Add(socket);

                    // Bind to the receive ready event
                    socket.ReceiveReady += ZmqSocketReceiveReady;

                    // Notify
                    ClientInfo.Instance.XmrSubscriberStatus = "Connected to " + ApplicationSettings.Default.XmrNetworkAddress + ". Waiting for messages.";

                    // Sit and wait, processing messages, indefinitely or until we are interrupted.
                    _poller.Run();
                }
            }
            finally
            {
                _poller.Dispose();
            }
        }

        /// <summary>
        /// Receive event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ZmqSocketReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            try
            {
                // Receive the message
                NetMQMessage message = e.Socket.ReceiveMultipartMessage();

                // Update status
                UpdateStatus();

                // Deal with heart beat
                if (message[0].ConvertToString() == "H")
                {
                    LastHeartBeat = DateTime.Now;
                    return;
                }

                // Decrypt the message
                try
                {
                    ProcessMessage(OpenSslInterop.decrypt(message[2].ConvertToString(), message[1].ConvertToString(), _hardwareKey.getXmrKey().Private));
                }
                catch (Exception decryptException)
                {
                    Trace.WriteLine(new LogMessage("XmrSubscriber - processMessage", "Unopenable Message: " + decryptException.Message), LogType.Error.ToString());
                    Trace.WriteLine(new LogMessage("XmrSubscriber - processMessage", e.ToString()), LogType.Audit.ToString());
                    return;
                }
            }
            catch (NetMQException netMQException)
            {
                throw netMQException;
            }
            catch (Exception ex)
            {
                // Log this message, but dont abort the thread
                Trace.WriteLine(new LogMessage("XmrSubscriber - _socket_ReceiveReady", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                Trace.WriteLine(new LogMessage("XmrSubscriber - _socket_ReceiveReady", e.ToString()), LogType.Audit.ToString());
                ClientInfo.Instance.XmrSubscriberStatus = "Error. " + ex.Message;
            }
        }

        /// <summary>
        /// Updates the status
        /// </summary>
        private void UpdateStatus()
        {
            // Update status
            string statusMessage = "Connected (" + GetAddressForStatus()
                + "), last activity: " + DateTime.Now.ToString();

            // Write this out to a log
            ClientInfo.Instance.XmrSubscriberStatus = statusMessage;
            Trace.WriteLine(new LogMessage("XmrSubscriber - Run", statusMessage), LogType.Audit.ToString());
        }

        /// <summary>
        /// Wait for a Message
        /// </summary>
        private void ProcessMessage(string opened)
        {
            // Decode into a JSON string
            PlayerAction action = JsonConvert.DeserializeObject<PlayerAction>(opened);

            // Make sure the TTL hasn't expired
            if (DateTime.Now > action.createdDt.AddSeconds(action.ttl))
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - processMessage", "Expired Message: " + action.action), LogType.Info.ToString());
                return;
            }

            // Decide what to do with the message, probably raise events according to the type of message we have
            switch (action.action)
            {
                case "commandAction":
                    // Create a schedule command out of the message
                    Dictionary<string, string> obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(opened);
                    ScheduleCommand command = new ScheduleCommand();
                    string code;
                    obj.TryGetValue("commandCode", out code);
                    command.Code = code;

                    new Thread(new ThreadStart(command.Run)).Start();
                    break;

                case "dataUpdate":
                    DataUpdatePlayerAction dataUpdate = JsonConvert.DeserializeObject<DataUpdatePlayerAction>(opened);
                    OnAction?.Invoke(dataUpdate);
                    break;

                case "collectNow":
                case RevertToSchedulePlayerAction.Name:
                    OnAction?.Invoke(action);
                    break;

                case LayoutChangePlayerAction.Name:
                    LayoutChangePlayerAction changeLayout = JsonConvert.DeserializeObject<LayoutChangePlayerAction>(opened);
                    OnAction?.Invoke(changeLayout);
                    break;

                case OverlayLayoutPlayerAction.Name:
                    OverlayLayoutPlayerAction overlayLayout = JsonConvert.DeserializeObject<OverlayLayoutPlayerAction>(opened);
                    OnAction?.Invoke(overlayLayout);
                    break;

                case "screenShot":
                    ScreenShot.TakeAndSend();
                    ClientInfo.Instance.NotifyStatusToXmds();
                    break;

                case TriggerWebhookAction.Name:
                    OnAction?.Invoke(JsonConvert.DeserializeObject<TriggerWebhookAction>(opened));
                    break;

                case "purgeAll":
                    OnAction?.Invoke(action);
                    break;

                case "criteriaUpdate":
                    // Process into a CriteriaUpdateAction
                    var update = JsonConvert.DeserializeObject<JObject>(opened);
                    var updateAction = new CriteriaUpdateAction();
                    foreach (var item in update["criteriaUpdates"])
                    {
                        updateAction.Items.Add(new CriteriaRequest
                        {
                            metric = item["metric"].ToString(),
                            value = item["value"].ToString(),
                            ttl = int.Parse(item["ttl"].ToString())
                        });
                    }
                    OnAction?.Invoke(updateAction);
                    break;

                default:
                    Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Unknown Message: " + action.action), LogType.Info.ToString());
                    break;
            }
        }

        /// <summary>
        /// Wake Up
        /// </summary>
        public void Restart()
        {
            try
            {
                // Fully release the socket so a fresh one will be created on the next loop iteration.
                lock (_socketLock)
                {
                    ReleaseWebSocket();
                }

                // Stop the poller
                if (_poller != null)
                {
                    _poller.Stop();
                    _poller.Dispose();
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - Restart", "Unable to stop XMR during restart: " + e.Message), LogType.Info.ToString());
            }

            // Wakeup
            _manualReset.Set();
        }

        /// <summary>
        /// Stop the agent
        /// </summary>
        public void Stop()
        {
            try
            {
                // Fully release the socket (detach handlers + close + dispose) so shutdown
                // does not strand a live WebSocket with handlers still pointing at this
                // subscriber instance.
                lock (_socketLock)
                {
                    ReleaseWebSocket();
                }

                // Stop the poller
                if (_poller != null)
                {
                    _poller.Stop();
                    _poller.Dispose();
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - Stop", "Unable to Stop XMR: " + e.Message), LogType.Info.ToString());
            }
            
            // Stop the thread at the next loop
            _forceStop = true;

            // Wakeup
            _manualReset.Set();
        }
    }
}
