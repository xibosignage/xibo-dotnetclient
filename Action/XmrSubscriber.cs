using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using XiboClient.Action;
using XiboClient.Log;

namespace XiboClient.Logic
{
    class XmrSubscriber
    {
        public static object _locker = new object();

        // Members to stop the thread
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        /// <summary>
        /// Last Heartbeat packet received
        /// </summary>
        public DateTime LastHeartBeat = DateTime.MinValue;

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
        /// Client Info Form
        /// </summary>
        public ClientInfo ClientInfoForm
        {
            set
            {
                _clientInfoForm = value;
            }
        }
        private ClientInfo _clientInfoForm;

        /// <summary>
        /// The MQ Poller
        /// </summary>
        private NetMQPoller _poller;

        /// <summary>
        /// The Init Address
        /// </summary>
        private string _address;

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
                        if (string.IsNullOrEmpty(ApplicationSettings.Default.XmrNetworkAddress))
                            throw new Exception("Empty XMR Network Address");

                        // Cache the address for this socket (the setting may change outside).
                        _address = ApplicationSettings.Default.XmrNetworkAddress;

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
                                socket.ReceiveReady += _socket_ReceiveReady;

                                // Notify
                                _clientInfoForm.XmrSubscriberStatus = "Connected to " + ApplicationSettings.Default.XmrNetworkAddress + ". Waiting for messages.";

                                // Sit and wait, processing messages, indefinitely or until we are interrupted.
                                _poller.Run();
                            }
                        }
                        finally
                        {
                            _poller.Dispose();
                        }

                        Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Socket Disconnected, waiting to reconnect."), LogType.Info.ToString());
                    }
                    catch (TerminatingException terminatingEx)
                    {
                        Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "NetMQ terminating: " + terminatingEx.Message), LogType.Audit.ToString());
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Unable to Subscribe: " + e.Message), LogType.Info.ToString());
                        _clientInfoForm.XmrSubscriberStatus = e.Message;
                    }

                    // Update status
                    _clientInfoForm.XmrSubscriberStatus = "Disconnected, waiting to reconnect, last activity: " + LastHeartBeat.ToString();

                    // Sleep for 60 seconds.
                    _manualReset.WaitOne(60 * 1000);
                }
            }

            Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Subscriber Stopped"), LogType.Info.ToString());
        }

        /// <summary>
        /// Receive event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _socket_ReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            try
            {
                processMessage(e.Socket.ReceiveMultipartMessage(), _hardwareKey.getXmrKey());
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
                _clientInfoForm.XmrSubscriberStatus = "Error. " + ex.Message;
            }
        }

        /// <summary>
        /// Wait for a Message
        /// </summary>
        private void processMessage(NetMQMessage message, AsymmetricCipherKeyPair rsaKey)
        {
            // Update status
            string statusMessage = "Connected (" + ApplicationSettings.Default.XmrNetworkAddress + "), last activity: " + DateTime.Now.ToString();

            // Write this out to a log
            _clientInfoForm.XmrSubscriberStatus = statusMessage;
            Trace.WriteLine(new LogMessage("XmrSubscriber - Run", statusMessage), LogType.Audit.ToString());

            // Deal with heart beat
            if (message[0].ConvertToString() == "H")
            {
                LastHeartBeat = DateTime.Now;
                return;
            }

            // Decrypt the message
            string opened;
            try
            {
                opened = OpenSslInterop.decrypt(message[2].ConvertToString(), message[1].ConvertToString(), rsaKey.Private);
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - processMessage", "Unopenable Message: " + e.Message), LogType.Error.ToString());
                Trace.WriteLine(new LogMessage("XmrSubscriber - processMessage", e.ToString()), LogType.Audit.ToString());
                return;
            }

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

                case "collectNow":
                case RevertToSchedulePlayerAction.Name:
                    if (OnAction != null)
                        OnAction(action);
                    break;

                case LayoutChangePlayerAction.Name:

                    LayoutChangePlayerAction changeLayout = JsonConvert.DeserializeObject<LayoutChangePlayerAction>(opened);

                    if (OnAction != null)
                        OnAction(changeLayout);

                    break;

                case OverlayLayoutPlayerAction.Name:
                    OverlayLayoutPlayerAction overlayLayout = JsonConvert.DeserializeObject<OverlayLayoutPlayerAction>(opened);

                    if (OnAction != null)
                        OnAction(overlayLayout);
                    break;

                case "screenShot":
                    ScreenShot.TakeAndSend();
                    _clientInfoForm.notifyStatusToXmds();
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
                // Stop the poller
                if (_poller != null)
                {
                    _poller.Stop();
                }

                // Wakeup
                _manualReset.Set();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - Restart", "Unable to Restart XMR: " + e.Message), LogType.Info.ToString());
            }
        }

        /// <summary>
        /// Stop the agent
        /// </summary>
        public void Stop()
        {
            try
            {
                // Stop the poller
                if (_poller != null)
                {
                    _poller.Stop();
                }

                // Stop the thread at the next loop
                _forceStop = true;

                // Wakeup
                _manualReset.Set();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - Stop", "Unable to Stop XMR: " + e.Message), LogType.Info.ToString());
            }
        }
    }
}
