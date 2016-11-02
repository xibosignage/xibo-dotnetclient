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
        /// Runs the agent
        /// </summary>
        public void Run()
        {
            try
            {
                // Check we have an address to connect to.
                if (string.IsNullOrEmpty(ApplicationSettings.Default.XmrNetworkAddress))
                    throw new Exception("Empty XMR Network Address");

                // Get the Private Key
                AsymmetricCipherKeyPair rsaKey = _hardwareKey.getXmrKey();

                // Connect to XMR
                using (NetMQContext context = NetMQContext.Create())
                {
                    using (SubscriberSocket socket = context.CreateSubscriberSocket())
                    {
                        // Bind
                        socket.Connect(ApplicationSettings.Default.XmrNetworkAddress);
                        socket.Subscribe("H");
                        socket.Subscribe(_hardwareKey.Channel);

                        // Notify
                        _clientInfoForm.XmrSubscriberStatus = "Connected to " + ApplicationSettings.Default.XmrNetworkAddress;

                        while (!_forceStop)
                        {
                            lock (_locker)
                            {
                                try
                                {
                                    NetMQMessage message = socket.ReceiveMultipartMessage();
                                    
                                    // Update status
                                    _clientInfoForm.XmrSubscriberStatus = "Connected (" + ApplicationSettings.Default.XmrNetworkAddress + "), last activity: " + DateTime.Now.ToString();

                                    // Deal with heart beat
                                    if (message[0].ConvertToString() == "H")
                                    {
                                        LastHeartBeat = DateTime.Now;
                                        continue;
                                    }

                                    // Decrypt the message
                                    string opened = OpenSslInterop.decrypt(message[2].ConvertToString(), message[1].ConvertToString(), rsaKey.Private);

                                    // Decode into a JSON string
                                    PlayerAction action = JsonConvert.DeserializeObject<PlayerAction>(opened);

                                    // Make sure the TTL hasn't expired
                                    if (DateTime.Now > action.createdDt.AddSeconds(action.ttl))
                                    {
                                        Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Expired Message: " + action.action), LogType.Info.ToString());
                                        continue;
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
                                            break;

                                        default:
                                            Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Unknown Message: " + action.action), LogType.Info.ToString());
                                            break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Log this message, but dont abort the thread
                                    Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                                    _clientInfoForm.XmrSubscriberStatus = "Error. " + ex.Message;
                                }
                            }
                        }
                    }
                }

                // Update status
                _clientInfoForm.XmrSubscriberStatus = "Not Running, last activity: " + LastHeartBeat.ToString();

                Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Subscriber Stopped"), LogType.Info.ToString());
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Unable to Subscribe to XMR: " + e.Message), LogType.Info.ToString());
                _clientInfoForm.XmrSubscriberStatus = e.Message;
            }
        }
    }
}
