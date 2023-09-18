/**
 * Copyright (C) 2022 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - http://www.xibo.org.uk
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using XiboClient.Action;
using XiboClient.Adspace;
using XiboClient.Control;
using XiboClient.Log;
using XiboClient.Logic;
using XiboClient.Stats;
using XiboClient.XmdsAgents;

namespace XiboClient
{
    /// <summary>
    /// Reads the schedule
    /// </summary>
    public class Schedule
    {
        public delegate void ScheduleChangeDelegate(ScheduleItem scheduleItem);
        public event ScheduleChangeDelegate ScheduleChangeEvent;

        public delegate void OverlayChangeDelegate(List<ScheduleItem> overlays);
        public event OverlayChangeDelegate OverlayChangeEvent;

        public delegate void OnTriggerReceivedDelegate(string triggerType, string triggerCode, int sourceId, int duration);
        public event OnTriggerReceivedDelegate OnTriggerReceived;

        /// <summary>
        /// Current Schedule of Normal Layouts
        /// </summary>
        private List<ScheduleItem> _layoutSchedule;
        private int _currentLayout = 0;

        /// <summary>
        /// Current Schedule of Overlay Layouts
        /// </summary>
        private List<ScheduleItem> _overlaySchedule;

        /// <summary>
        /// Has stop been called?
        /// </summary>
        private bool _stopCalled = false;

        /// <summary>
        /// Should we trigger a schedule call on register complete?
        /// </summary>
        private bool _triggerScheduleOnRegisterComplete = false;

        #region Threads and Agents
        // Key
        private HardwareKey _hardwareKey;

        // Schedule Manager
        private ScheduleManager _scheduleManager;
        Thread _scheduleManagerThread;

        // Register Agent
        private RegisterAgent _registerAgent;
        Thread _registerAgentThread;

        // Required Files Agent
        private ScheduleAndFilesAgent _scheduleAndRfAgent;
        Thread _scheduleAndRfAgentThread;

        // Library Agent
        private LibraryAgent _libraryAgent;
        Thread _libraryAgentThread;

        // Log Agent
        private LogAgent _logAgent;
        Thread _logAgentThread;

        // Faults Agent
        private FaultsAgent _faultsAgent;
        Thread _faultsAgentThread;

        // Data Agent
        private DataAgent _dataAgent;

        Thread _dataAgentThread;

        // XMR Subscriber
        private XmrSubscriber _xmrSubscriber;
        Thread _xmrSubscriberThread;

        // Local Web Server
        private EmbeddedServer _server;
        Thread _serverThread;
        #endregion

        /// <summary>
        /// Create a schedule
        /// </summary>
        /// <param name="scheduleLocation"></param>
        public Schedule(string scheduleLocation)
        {
            Trace.WriteLine(string.Format("XMDS Location: {0}", ApplicationSettings.Default.XiboClient_xmds_xmds));

            // Get the key for this display
            _hardwareKey = new HardwareKey();

            // Create a new collection for the layouts in the schedule
            _layoutSchedule = new List<ScheduleItem>();

            // Create a Register Agent
            _registerAgent = new RegisterAgent();
            _registerAgent.OnXmrReconfigure += _registerAgent_OnXmrReconfigure;
            _registerAgent.OnRegisterComplete += _registerAgent_OnRegisterComplete;
            _registerAgentThread = new Thread(new ThreadStart(_registerAgent.Run));
            _registerAgentThread.Name = "RegisterAgentThread";

            // Create a schedule manager
            _scheduleManager = new ScheduleManager(scheduleLocation);
            _scheduleManager.OnNewScheduleAvailable += new ScheduleManager.OnNewScheduleAvailableDelegate(_scheduleManager_OnNewScheduleAvailable);
            _scheduleManager.OnRefreshSchedule += new ScheduleManager.OnRefreshScheduleDelegate(_scheduleManager_OnRefreshSchedule);
            _scheduleManager.OnScheduleManagerCheckComplete += _scheduleManager_OnScheduleManagerCheckComplete;

            // Create a schedule manager thread
            _scheduleManagerThread = new Thread(new ThreadStart(_scheduleManager.Run));
            _scheduleManagerThread.Name = "ScheduleManagerThread";

            // Data Agent
            _dataAgent = new DataAgent();
            _dataAgentThread = new Thread(new ThreadStart(_dataAgent.Run))
            {
                Name = "DataAgent"
            };

            // Create a RequiredFilesAgent
            _scheduleAndRfAgent = new ScheduleAndFilesAgent(_dataAgent);
            _scheduleAndRfAgent.CurrentScheduleManager = _scheduleManager;
            _scheduleAndRfAgent.ScheduleLocation = scheduleLocation;
            _scheduleAndRfAgent.HardwareKey = _hardwareKey.Key;
            _scheduleAndRfAgent.OnFullyProvisioned += _requiredFilesAgent_OnFullyProvisioned;
            _scheduleAndRfAgent.OnComplete += new ScheduleAndFilesAgent.OnCompleteDelegate(LayoutFileModified);

            // Create a thread for the RequiredFiles Agent to run in - but dont start it up yet.
            _scheduleAndRfAgentThread = new Thread(new ThreadStart(_scheduleAndRfAgent.Run));
            _scheduleAndRfAgentThread.Name = "RequiredFilesAgentThread";

            // Library Agent
            _libraryAgent = new LibraryAgent();
            _libraryAgent.CurrentCacheManager = CacheManager.Instance;

            // Create a thread for the Library Agent to run in - but dont start it up yet.
            _libraryAgentThread = new Thread(new ThreadStart(_libraryAgent.Run));
            _libraryAgentThread.Name = "LibraryAgent";

            // Log Agent
            _logAgent = new LogAgent();
            _logAgentThread = new Thread(new ThreadStart(_logAgent.Run));
            _logAgentThread.Name = "LogAgent";

            // Faults Agent
            _faultsAgent = new FaultsAgent
            {
                HardwareKey = _hardwareKey.Key
            };
            _faultsAgentThread = new Thread(new ThreadStart(_faultsAgent.Run))
            {
                Name = "FaultsAgent"
            };

            // XMR Subscriber
            _xmrSubscriber = new XmrSubscriber();
            _xmrSubscriber.HardwareKey = _hardwareKey;
            _xmrSubscriber.OnAction += _xmrSubscriber_OnAction;

            // Thread start
            _xmrSubscriberThread = new Thread(new ThreadStart(_xmrSubscriber.Run));
            _xmrSubscriberThread.Name = "XmrSubscriber";

            // Embedded Server
            _server = new EmbeddedServer();
            _server.OnServerClosed += _server_OnServerClosed;
            _server.OnTriggerReceived += EmbeddedServerOnTriggerReceived;
            _server.OnDurationReceived += EmbeddedServerOnDurationReceived;
            _serverThread = new Thread(new ThreadStart(_server.Run))
            {
                Name = "EmbeddedServer"
            };
        }

        /// <summary>
        /// Initialize the Schedule components
        /// </summary>
        public void InitializeComponents()
        {
            // Start the RegisterAgent thread
            _registerAgentThread.Start();

            // Start the RequiredFilesAgent thread
            _scheduleAndRfAgentThread.Start();

            // Start the data agent thread
            _dataAgentThread.Start();

            // Start the ScheduleManager thread
            _scheduleManagerThread.Start();

            // Start the LibraryAgent thread
            _libraryAgentThread.Start();

            // Start the LogAgent thread
            _logAgentThread.Start();

            // Start the Faults thread
            _faultsAgentThread.Start();

            // Start the Proof of Play thread
            StatManager.Instance.Start();

            // Start the subscriber thread
            _xmrSubscriberThread.Start();

            // Start the embedded server thread
            _serverThread.Start();
        }

        /// <summary>
        /// New Schedule Available
        /// </summary>
        private void _scheduleManager_OnNewScheduleAvailable()
        {
            Debug.WriteLine("_scheduleManager_OnNewScheduleAvailable: New Schedule Available", "Schedule");
            Debug.WriteLine("_scheduleManager_OnNewScheduleAvailable: " + _scheduleManager.CurrentOverlaySchedule.Count + " overlays", "Schedule");
            Debug.WriteLine("_scheduleManager_OnNewScheduleAvailable: " + _scheduleManager.CurrentSchedule.Count + " normal schedules", "Schedule");

            _overlaySchedule = new List<ScheduleItem>(_scheduleManager.CurrentOverlaySchedule);
            _layoutSchedule = _scheduleManager.CurrentSchedule;

            // Set the current pointer to 0
            _currentLayout = 0;

            // Record the playback if this is a cycle playback item
            if (_layoutSchedule[0].IsCyclePlayback)
            {
                ClientInfo.Instance.IncrementCampaignGroupPlaycount(_layoutSchedule[0].CycleGroupKey);
            }

            // Raise a schedule change event
            ScheduleChangeEvent(_layoutSchedule[0]);

            // Pass a new set of overlay's to subscribers
            OverlayChangeEvent?.Invoke(_overlaySchedule);
        }

        /// <summary>
        /// Schedule has been refreshed
        /// </summary>
        void _scheduleManager_OnRefreshSchedule()
        {
            _layoutSchedule = _scheduleManager.CurrentSchedule;
        }

        /// <summary>
        /// Schedule Manager has completed a cycle
        /// </summary>
        void _scheduleManager_OnScheduleManagerCheckComplete()
        {
            // XMR address is present and has received at least 1 heart beat
            bool xmrShouldBeRunning = (!string.IsNullOrEmpty(ApplicationSettings.Default.XmrNetworkAddress) && _xmrSubscriber.LastHeartBeat != DateTime.MinValue);

            // If the agent threads are all alive, and either XMR shouldn't be running OR the subscriber thread is alive.
            if (agentThreadsAlive())
            {
                // Update status marker on the main thread.
                ClientInfo.Instance.UpdateStatusMarkerFile();
            }
            else
            {
                Trace.WriteLine(new LogMessage("Schedule - OnScheduleManagerCheckComplete", "Agent threads/XMR is dead, not updating status.json"), LogType.Error.ToString());
            }

            // Log for overdue XMR
            if (xmrShouldBeRunning && _xmrSubscriber.LastHeartBeat < DateTime.Now.AddHours(-1))
            {
                ClientInfo.Instance.XmrSubscriberStatus = "Long term Inactive (" + ApplicationSettings.Default.XmrNetworkAddress + "), last activity: " + _xmrSubscriber.LastHeartBeat.ToString();
                Trace.WriteLine(new LogMessage("Schedule - OnScheduleManagerCheckComplete", "XMR heart beat last received over an hour ago."));

                // Issue an XMR restart if we've gone this long without connecting
                // we do this because we suspect that the TCP socket has died without notifying the poller
                restartXmr();
            }
            else if (xmrShouldBeRunning && _xmrSubscriber.LastHeartBeat < DateTime.Now.AddMinutes(-5))
            {
                ClientInfo.Instance.XmrSubscriberStatus = "Inactive (" + ApplicationSettings.Default.XmrNetworkAddress + "), last activity: " + _xmrSubscriber.LastHeartBeat.ToString();
                Trace.WriteLine(new LogMessage("Schedule - OnScheduleManagerCheckComplete", "XMR heart beat last received over 5 minutes ago."), LogType.Audit.ToString());
            }
        }

        /// <summary>
        /// Are all the required agent threads alive?
        /// </summary>
        /// <returns></returns>
        private bool agentThreadsAlive()
        {
            return _registerAgentThread.IsAlive &&
                _scheduleAndRfAgentThread.IsAlive &&
                _logAgentThread.IsAlive &&
                _faultsAgentThread.IsAlive &&
                _libraryAgentThread.IsAlive &&
                _xmrSubscriberThread.IsAlive;
        }

        /// <summary>
        /// XMR Reconfigure
        /// </summary>
        void _registerAgent_OnXmrReconfigure()
        {
            restartXmr();
        }

        /// <summary>
        /// Regiser call has completed.
        /// </summary>
        /// <param name="error"></param>
        private void _registerAgent_OnRegisterComplete(bool error)
        {
            if (!error && _triggerScheduleOnRegisterComplete)
            {
                _scheduleAndRfAgent.WakeUp();
            }

            // Reset
            _triggerScheduleOnRegisterComplete = false;
        }

        /// <summary>
        /// XMR Subscriber Action
        /// </summary>
        void _xmrSubscriber_OnAction(Action.PlayerActionInterface action)
        {
            switch (action.GetActionName())
            {
                case RevertToSchedulePlayerAction.Name:
                    _scheduleManager.ClearLayoutChangeActions();
                    _scheduleManager.RunNow();
                    break;

                case "collectNow":
                    // Run all of the various agents
                    wakeUpXmds();
                    break;

                case "dataUpdate":
                    // Wakeup the data agent and mark the widget to be force updated.
                    _dataAgent.ForceUpdateWidget(((DataUpdatePlayerAction)action).widgetId);
                    _dataAgent.WakeUp();
                    break;

                case LayoutChangePlayerAction.Name:
                    // Add to a collection of Layout Change events 
                    if (((LayoutChangePlayerAction)action).changeMode == "replace")
                    {
                        _scheduleManager.ReplaceLayoutChangeActions(((LayoutChangePlayerAction)action));
                    }
                    else
                    {
                        _scheduleManager.AddLayoutChangeAction(((LayoutChangePlayerAction)action));
                    }

                    // Assess the schedule now, or later?
                    if (((LayoutChangePlayerAction)action).IsDownloadRequired())
                    {
                        // Run XMDS to download the required layouts
                        // need to notify again once a complete download has occurred.
                        wakeUpXmds();
                    }
                    else
                    {
                        // Reassess the schedule
                        _scheduleManager.RunNow();
                    }

                    break;

                case OverlayLayoutPlayerAction.Name:
                    // Add to a collection of Layout Change events 
                    _scheduleManager.AddOverlayLayoutAction(((OverlayLayoutPlayerAction)action));

                    // Assess the schedule now, or later?
                    if (((OverlayLayoutPlayerAction)action).IsDownloadRequired())
                    {
                        // Run XMDS to download the required layouts
                        // need to notify again once a complete download has occurred.
                        wakeUpXmds();
                    }
                    else
                    {
                        // Reassess the schedule
                        _scheduleManager.RunNow();
                    }

                    break;

                case TriggerWebhookAction.Name:
                    TriggerWebhookAction webhookAction = (TriggerWebhookAction)action;
                    EmbeddedServerOnTriggerReceived(webhookAction.triggerCode, 0);
                    break;

                case "purgeAll":
                    _libraryAgent.PurgeAll();
                    break;
            }
        }

        /// <summary>
        /// Required files fully provisioned
        /// </summary>
        private void _requiredFilesAgent_OnFullyProvisioned()
        {
            // Mark all layout change actions as downloaded and assess the schedule
            _scheduleManager.setAllActionsDownloaded();
            _scheduleManager.RunNow();
        }

        /// <summary>
        /// Wake up all XMDS services
        /// </summary>
        public void wakeUpXmds()
        {
            // Wake up schedule/rf after the register call, which will update our CRC's.
            _triggerScheduleOnRegisterComplete = true;
            _registerAgent.WakeUp();

            // Wake up other calls in a little while (give the rest time to complete so we send the latest info)
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            timer.Tick += (timerSender, args) =>
            {
                // You only tick once
                timer.Stop();

                // Wake
                _logAgent.WakeUp();
                _faultsAgent.WakeUp();
            };
            timer.Start();
        }

        /// <summary>
        /// Restart XMR
        /// </summary>
        public void restartXmr()
        {
            try
            {
                // Stop and start the XMR thread
                _xmrSubscriber.Restart();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Schedule - restartXmr", "Unable to restart XMR: " + e.Message), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// Moves to the previous Layout
        /// </summary>
        public void PreviousLayout()
        {
            Debug.WriteLine("PreviousLayout: called.", "Schedule");

            // Capture the active layout
            ScheduleItem activeSchedule = _layoutSchedule[_currentLayout];

            // Move the current layout back
            // next layout moves it forward, so we actually need to move it back 2.
            _currentLayout -= 2;

            // If we have dropped below the first layout in the list, we should go to the end of the list instead.
            if (_currentLayout < 0)
            {
                _currentLayout = _layoutSchedule.Count - 2;
            }

            // Call Next Layout
            NextLayout();
        }

        /// <summary>
        /// Moves the layout on
        /// </summary>
        public void NextLayout()
        {
            Debug.WriteLine("NextLayout: called", "Schedule");

            // increment the current layout
            _currentLayout++;

            // if the current layout is greater than the count of layouts, then reset to 0
            if (_currentLayout >= _layoutSchedule.Count)
            {
                _currentLayout = 0;
            }

            ScheduleItem nextLayout = _layoutSchedule[_currentLayout];

            Debug.WriteLine(string.Format("NextLayout: {0}, Interrupt: {1}, Cycle based: {2}", 
                nextLayout.layoutFile, 
                nextLayout.IsInterrupt(), 
                nextLayout.IsCyclePlayback
                ), "Schedule");

            // If we are cycle playback, then resolve the actual layout we want to play out of the group we have.
            if (nextLayout.IsCyclePlayback)
            {
                // The main layout is sequence 0
                int sequence = ClientInfo.Instance.GetCampaignGroupSequence(nextLayout.CycleGroupKey);

                // Pull out the layout (schedule item) at this group sequence.
                if (ClientInfo.Instance.GetCampaignGroupPlaycount(nextLayout.CycleGroupKey) >= nextLayout.CyclePlayCount)
                {
                    // Next sequence
                    sequence++;

                    // Make sure we can get this sequence
                    if (sequence >= nextLayout.CycleScheduleItems.Count)
                    {
                        sequence = 0;
                    }

                    // Set the sequence and increment the playcount
                    ClientInfo.Instance.SetCampaignGroupSequence(nextLayout.CycleGroupKey, sequence);
                }
                else
                {
                    // We are playing the same one again, so increment the playcount.
                    ClientInfo.Instance.IncrementCampaignGroupPlaycount(nextLayout.CycleGroupKey);
                }

                // Set the next layout
                if (sequence > 0)
                {
                    nextLayout = nextLayout.CycleScheduleItems[sequence];
                }
            }

            // Raise the event
            ScheduleChangeEvent?.Invoke(nextLayout);
        }

        /// <summary>
        /// Get the current default layout.
        /// </summary>
        /// <returns></returns>
        public ScheduleItem GetDefaultLayout()
        {
            return _scheduleManager.CurrentDefaultLayout;
        }

        /// <summary>
        /// The number of active layouts in the current schedule
        /// </summary>
        public int ActiveLayouts
        {
            get
            {
                return _layoutSchedule.Count;
            }
        }

        /// <summary>
        /// The number of active adspace exchange events
        /// </summary>
        public int ActiveAdspaceExchangeEvents
        {
            get
            {
                return _layoutSchedule.FindAll(item => item.IsAdspaceExchange).Count;
            }
        }

        /// <summary>
        /// A layout file has changed
        /// </summary>
        /// <param name="layoutPath"></param>
        private void LayoutFileModified(string layoutPath)
        {
            Trace.WriteLine(new LogMessage("Schedule", "LayoutFileModified: Layout file changed: " + layoutPath), LogType.Info.ToString());

            // Are we set to expire modified layouts? If not then just return as if
            // nothing had happened.
            if (!ApplicationSettings.Default.ExpireModifiedLayouts)
            {
                return;
            }

            // Determine if we need to reassess the overlays
            bool changeRequired = false;

            foreach (ScheduleItem item in _overlaySchedule)
            {
                if (item.layoutFile == ApplicationSettings.Default.LibraryPath + @"\" + layoutPath)
                {
                    // We should mark this item as being one to remove and re-add.
                    item.Refresh = true;
                    changeRequired = true;
                }
            }

            if (changeRequired)
            {
                OverlayChangeEvent?.Invoke(_overlaySchedule);
            }

            // If the layout that got changed is the current layout, move on
            try
            {
                if (_layoutSchedule[_currentLayout].layoutFile == ApplicationSettings.Default.LibraryPath + @"\" + layoutPath)
                {
                    // What happens if the action of downloading actually invalidates this layout?
                    bool valid = CacheManager.Instance.IsValidPath(layoutPath);

                    if (valid)
                    {
                        // Check dependents
                        foreach (string dependent in _layoutSchedule[_currentLayout].Dependents)
                        {
                            if (!string.IsNullOrEmpty(dependent) && !CacheManager.Instance.IsValidPath(dependent))
                            {
                                valid = false;
                                break;
                            }
                        }
                    }

                    if (!valid)
                    {
                        Trace.WriteLine(new LogMessage("Schedule - LayoutFileModified", "The current layout is now invalid, refreshing the current schedule."), LogType.Audit.ToString());

                        // We should not force a change and we should tell the schedule manager to run now
                        _scheduleManager.RefreshSchedule = true;
                        _scheduleManager.RunNow();
                    }
                    else
                    {
                        Trace.WriteLine(new LogMessage("Schedule - LayoutFileModified", "Forcing the current layout to change: " + layoutPath), LogType.Audit.ToString());

                        // Run the next layout
                        NextLayout();
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("fileCollector_LayoutFileChanged", String.Format("Unable to determine current layout with exception {0}", ex.Message)), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// On Server Closed Event
        /// </summary>
        void _server_OnServerClosed()
        {
            if (!_stopCalled && _serverThread != null && !_serverThread.IsAlive)
            {
                // We've stopped and we shouldn't have
                _serverThread.Start();
            }
        }

        /// <summary>
        /// Stops the Schedule Object
        /// </summary>
        public void Stop()
        {
            // Stop has been called
            _stopCalled = true;

            // Stop the register agent
            _registerAgent.OnRegisterComplete -= _registerAgent_OnRegisterComplete;
            _registerAgent.Stop();

            // Stop the requiredfiles agent
            _scheduleAndRfAgent.Stop();

            // Stop the Schedule Manager Thread
            _scheduleManager.Stop();

            // Stop the LibraryAgent Thread
            _libraryAgent.Stop();

            // Stop the LogAgent Thread
            _logAgent.Stop();

            // Stop the Faults Agent Thread
            _faultsAgent.Stop();

            // Stop the Proof of Play Thread
            StatManager.Instance.Stop();

            // Stop the subsriber thread
            _xmrSubscriber.Stop();

            // Clean up any NetMQ sockets, etc (false means don't block).
            NetMQ.NetMQConfig.Cleanup(false);

            // Stop the embedded server
            _server.Stop();
            _server.OnTriggerReceived -= EmbeddedServerOnTriggerReceived;
            _server.OnDurationReceived -= EmbeddedServerOnDurationReceived;
            _server.OnServerClosed -= _server_OnServerClosed;
        }

        /// <summary>
        /// Remove this Layout from the Schedule.
        /// </summary>
        /// <param name="item"></param>
        public void RemoveLayout(ScheduleItem item)
        {
            _layoutSchedule.Remove(item);

            if (_layoutSchedule.Count <= 0)
            {
                _layoutSchedule.Add(ScheduleItem.Splash());
            }
        }

        /// <summary>
        /// Get a Schedule Item for the given LayoutId
        /// </summary>
        /// <param name="layoutCode"></param>
        /// <returns></returns>
        public ScheduleItem GetScheduleItemForLayoutCode(string layoutCode)
        {
            // Find the layoutId we want.
            int layoutId = CacheManager.Instance.GetLayoutId(layoutCode);

            // Check that this Layout is valid
            if (!CacheManager.Instance.IsValidPath(layoutId + ".xlf") || CacheManager.Instance.IsUnsafeLayout(layoutId))
            {
                throw new Exception("Layout Invalid. Id = " + layoutId);
            }

            return new ScheduleItem()
            {
                id = layoutId,
                layoutFile = ApplicationSettings.Default.LibraryPath + @"\" + layoutId + @".xlf"
            };
        }

        /// <summary>
        /// Trigger received form an embedded server
        /// </summary>
        /// <param name="triggerCode"></param>
        /// <param name="sourceId"></param>
        public void EmbeddedServerOnTriggerReceived(string triggerCode, int sourceId)
        {
            OnTriggerReceived?.Invoke("webhook", triggerCode, sourceId, 0);
        }

        /// <summary>
        /// Trigger received form an embedded server
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="sourceId"></param>
        /// <param name="duration"></param>
        private void EmbeddedServerOnDurationReceived(string operation, int sourceId, int duration)
        {
            OnTriggerReceived?.Invoke("duration", operation, sourceId, duration);
        }

        /// <summary>
        /// A change layout action has finished
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool NotifyLayoutActionFinished(ScheduleItem item)
        {
            // See if the current layout is an action that can be removed.
            // If it CAN be removed then this will almost certainly result in a change in the current _layoutSchedule
            // therefore we should return out of this and kick off a schedule manager cycle, which will set the new layout.
            try
            {
                if (_scheduleManager.removeLayoutChangeActionIfComplete(item))
                {
                    _scheduleManager.RunNow();
                    return true;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Schedule", "NotifyLayoutActionFinished: Unable to check layout change actions. E = " + e.Message), LogType.Error.ToString());
            }

            return false;
        }

        /// <summary>
        /// Get an Ad
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="isUseWidget"></param>
        /// <param name="partner"></param>
        /// <returns></returns>
        public Ad GetAd(double width, double height, bool isUseWidget, string partner)
        {
            return _scheduleManager.GetAd(width, height, isUseWidget, partner);
        }

        /// <summary>
        /// Get the current actions schedule
        /// </summary>
        /// <returns></returns>
        public List<Action.Action> GetActions()
        {
            return _scheduleManager.CurrentActionsSchedule;
        }

        public void WakeUpScheduleManager()
        {
            _scheduleManager.RunNow();
        }
    }
}
