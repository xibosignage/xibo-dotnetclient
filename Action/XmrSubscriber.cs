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

                        // Check we have an address to connect to.
                        if (!string.IsNullOrEmpty(ApplicationSettings.Default.XmrNetworkAddress) && ApplicationSettings.Default.XmrNetworkAddress != "DISABLED")
                        {
                            // Decide whether we are connecting to a web socket based implementation, or a legacy ZMQ one.
                            if (ApplicationSettings.Default.XmrType == "ws")
                            {
                                LoopForWs();
                            }
                            else
                            {
                                LoopForZmq();
                            }

                            Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Disconnected, waiting to reconnect."), LogType.Info.ToString());

                            // Update status
                            ClientInfo.Instance.XmrSubscriberStatus = "Disconnected, waiting to reconnect, last activity: " + LastHeartBeat.ToString();
                        }
                        else
                        {
                            ClientInfo.Instance.XmrSubscriberStatus = "Not configured or Disabled";
                        }
                    }
                    catch (TerminatingException terminatingEx)
                    {
                        Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "NetMQ terminating: " + terminatingEx.Message), LogType.Audit.ToString());
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Unable to Subscribe: " + e.Message), LogType.Info.ToString());
                        ClientInfo.Instance.XmrSubscriberStatus = e.Message;
                    }

                    // Sleep for 60 seconds.
                    _manualReset.WaitOne(60 * 1000);
                }
            }

            Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Subscriber Stopped"), LogType.Info.ToString());
        }

        private void LoopForWs()
        {
            if (_webSocket != null && _webSocket.IsAlive)
            {
                return;
            }

            _webSocket = new WebSocket(GetWsAddress());
            _webSocket.SslConfiguration.EnabledSslProtocols |= SslProtocols.Tls12;
            _webSocket.OnOpen += _webSocket_OnOpen;
            _webSocket.OnClose += _webSocket_OnClose;
            _webSocket.OnMessage += _webSocket_OnMessage;
            _webSocket.OnError += _webSocket_OnError;
            _webSocket.Connect();
        }

        private void _webSocket_OnOpen(object sender, EventArgs e)
        {
            LogMessage.Audit("XmrSubscriber", "_webSocket_OnOpen", "Open");

            // Send the init message.
            JObject message = new JObject
            {
                { "type", "init" },
                { "key", ApplicationSettings.Default.XmrCmsKey },
                { "channel", _hardwareKey.Channel }
            };

            _webSocket.Send(message.ToString());
        }

        private void _webSocket_OnClose(object sender, CloseEventArgs e)
        {
            LogMessage.Audit("XmrSubscriber", "_webSocket_OnClose", e.Reason);
        }

        private void _webSocket_OnError(object sender, ErrorEventArgs e)
        {
            LogMessage.Error("XmrSubscriber", "_webSocket_OnError", e.Message);
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
        /// Get WebSocket address
        /// </summary>
        /// <returns></returns>
        private string GetWsAddress()
        {
            if (string.IsNullOrEmpty(ApplicationSettings.Default.XmrWebSocketAddress))
            {
                // Append /xmr to the CMS address
                return ApplicationSettings.Default.ServerUri
                    .Replace("https://", "wss://")
                    .Replace("http://", "ws://")
                        + "/xmr";
            }
            else
            {
                return ApplicationSettings.Default.XmrWebSocketAddress;
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
            string statusMessage = "Connected (" + ApplicationSettings.Default.XmrNetworkAddress + "), last activity: " + DateTime.Now.ToString();

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
                // Stop the socket
                if (_webSocket != null)
                {
                    _webSocket.Close();
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
                // Stop the socket
                if (_webSocket != null)
                {
                    _webSocket.Close();
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
