/**
 * Copyright (C) 2020 Xibo Signage Ltd
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
using XiboClient.Action;
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
        public delegate void ScheduleChangeDelegate(ScheduleItem scheduleItem, string mode);
        public event ScheduleChangeDelegate ScheduleChangeEvent;

        public delegate void OverlayChangeDelegate(List<ScheduleItem> overlays);
        public event OverlayChangeDelegate OverlayChangeEvent;

        /// <summary>
        /// Current Schedule of Normal Layouts
        /// </summary>
        private List<ScheduleItem> _layoutSchedule;
        private int _currentLayout = 0;
        private int _currentInterruptLayout = 0;

        /// <summary>
        /// Current Schedule of Overlay Layouts
        /// </summary>
        private List<ScheduleItem> _overlaySchedule;

        /// <summary>
        /// Has stop been called?
        /// </summary>
        private bool _stopCalled = false;

        /// <summary>
        /// Are we currently interrupting?
        /// </summary>
        private bool _interrupting = false;

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
            _registerAgentThread = new Thread(new ThreadStart(_registerAgent.Run));
            _registerAgentThread.Name = "RegisterAgentThread";

            // Create a schedule manager
            _scheduleManager = new ScheduleManager(scheduleLocation);
            _scheduleManager.OnNewScheduleAvailable += new ScheduleManager.OnNewScheduleAvailableDelegate(_scheduleManager_OnNewScheduleAvailable);
            _scheduleManager.OnRefreshSchedule += new ScheduleManager.OnRefreshScheduleDelegate(_scheduleManager_OnRefreshSchedule);
            _scheduleManager.OnScheduleManagerCheckComplete += _scheduleManager_OnScheduleManagerCheckComplete;
            _scheduleManager.OnInterruptNow += _scheduleManager_OnInterruptNow;
            _scheduleManager.OnInterruptPausePending += _scheduleManager_OnInterruptPausePending;
            _scheduleManager.OnInterruptEnd += _scheduleManager_OnInterruptEnd;

            // Create a schedule manager thread
            _scheduleManagerThread = new Thread(new ThreadStart(_scheduleManager.Run));
            _scheduleManagerThread.Name = "ScheduleManagerThread";

            // Create a RequiredFilesAgent
            _scheduleAndRfAgent = new ScheduleAndFilesAgent();
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
            _serverThread = new Thread(new ThreadStart(_server.Run));
            _serverThread.Name = "EmbeddedServer";
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

            // Start the ScheduleManager thread
            _scheduleManagerThread.Start();

            // Start the LibraryAgent thread
            _libraryAgentThread.Start();

            // Start the LogAgent thread
            _logAgentThread.Start();

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

            // If we are not interrupting, then update the current schedule
            if (!this._interrupting)
            {
                // Raise a schedule change event
                ScheduleChangeEvent(_layoutSchedule[0], "next");

                // Pass a new set of overlay's to subscribers
                OverlayChangeEvent?.Invoke(_overlaySchedule);
            }
            else
            {
                Debug.WriteLine("_scheduleManager_OnNewScheduleAvailable: Skipping Next Layout Change due to Interrupt", "Schedule");
            }
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
            _registerAgent.WakeUp();
            _scheduleAndRfAgent.WakeUp();
            _logAgent.WakeUp();
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
        /// Moves the layout on
        /// </summary>
        public void NextLayout()
        {
            Debug.WriteLine("NextLayout: called. Interrupting: " + this._interrupting, "Schedule");

            // Get the previous layout
            ScheduleItem previousLayout = (this._interrupting)
                ? _scheduleManager.CurrentInterruptSchedule[_currentInterruptLayout]
                : _layoutSchedule[_currentLayout];

            // See if the current layout is an action that can be removed.
            // If it CAN be removed then this will almost certainly result in a change in the current _layoutSchedule
            // therefore we should return out of this and kick off a schedule manager cycle, which will set the new layout.
            try
            {
                if (_scheduleManager.removeLayoutChangeActionIfComplete(previousLayout))
                {
                    _scheduleManager.RunNow();
                    return;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Schedule", "NextLayout: Unable to check layout change actions. E = " + e.Message), LogType.Error.ToString());
            }

            // Are currently interrupting?
            ScheduleItem nextLayout;
            if (this._interrupting)
            {
                // We might have fulifilled items in the schedule.
                List<ScheduleItem> notFulfilled = new List<ScheduleItem>();
                foreach (ScheduleItem item in _scheduleManager.CurrentInterruptSchedule)
                {
                    if (!item.IsFulfilled)
                    {
                        notFulfilled.Add(item);
                    }
                }

                // What if we don't have any?
                // pick the least worst option
                if (notFulfilled.Count <= 0)
                {
                    Debug.WriteLine("NextLayout: Interrupting and have run out of not-fulfilled schedules, using the first one.", "Schedule");

                    nextLayout = _scheduleManager.CurrentInterruptSchedule[0];
                }
                else
                {
                    // increment the current layout
                    _currentInterruptLayout++;

                    // if the current layout is greater than the count of layouts, then reset to 0
                    if (_currentInterruptLayout >= notFulfilled.Count)
                    {
                        _currentInterruptLayout = 0;
                    }

                    // Pull out the next Layout
                    nextLayout = notFulfilled[_currentInterruptLayout];
                }
            }
            else
            {
                // increment the current layout
                _currentLayout++;

                // if the current layout is greater than the count of layouts, then reset to 0
                if (_currentLayout >= _layoutSchedule.Count)
                {
                    _currentLayout = 0;
                }

                nextLayout = _layoutSchedule[_currentLayout];
            }

            Debug.WriteLine(string.Format("NextLayout: {0}, Interrupt: {1}", nextLayout.layoutFile, nextLayout.IsInterrupt()), "Schedule");

            // Raise the event
            ScheduleChangeEvent?.Invoke(nextLayout, (this._interrupting ? "interrupt-next" : "next"));
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
        /// The number of active layouts in the current schedule
        /// </summary>
        public int ActiveInterruptLayouts
        {
            get
            {
                return _scheduleManager.CurrentInterruptSchedule.Count;
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
            // We never force change an interrupt layout
            if (!ApplicationSettings.Default.ExpireModifiedLayouts || this._interrupting)
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
            _registerAgent.Stop();

            // Stop the requiredfiles agent
            _scheduleAndRfAgent.Stop();

            // Stop the Schedule Manager Thread
            _scheduleManager.Stop();
            _scheduleManager.OnInterruptNow -= _scheduleManager_OnInterruptNow;
            _scheduleManager.OnInterruptPausePending -= _scheduleManager_OnInterruptPausePending;
            _scheduleManager.OnInterruptEnd -= _scheduleManager_OnInterruptEnd;

            // Stop the LibraryAgent Thread
            _libraryAgent.Stop();

            // Stop the LogAgent Thread
            _logAgent.Stop();

            // Stop the Proof of Play Thread
            StatManager.Instance.Stop();

            // Stop the subsriber thread
            _xmrSubscriber.Stop();

            // Clean up any NetMQ sockets, etc (false means don't block).
            NetMQ.NetMQConfig.Cleanup(false);

            // Stop the embedded server
            _server.Stop();
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

        #region Interrupt Layouts

        /// <summary>
        /// Indicate we are interrupting
        /// </summary>
        public void SetInterrupting()
        {
            this._interrupting = true;

            // Inform the schedule manager that we have interrupted.
            this._scheduleManager.InterruptSetActive();
        }

        /// <summary>
        /// Interrupt Media has been Played
        /// </summary>
        public void SetInterruptMediaPlayed()
        {
            // Call interrupt end to switch back to the normal schedule
            this._scheduleManager_OnInterruptEnd();
        }

        /// <summary>
        /// Indicate there is an error with the Interrupt
        /// </summary>
        public void SetInterruptUnableToPlayAndEnd()
        {
            this._scheduleManager_OnInterruptEnd();
        }

        /// <summary>
        /// Interrupt Ended
        /// </summary>
        private void _scheduleManager_OnInterruptEnd()
        {
            Debug.WriteLine("Interrupt End Event", "Schedule");

            if (this._interrupting)
            {
                // Assume we will stop
                this._interrupting = false;

                // Stop interrupting forthwith
                ScheduleChangeEvent?.Invoke(null, "interrupt-end");

                // Bring back overlays
                OverlayChangeEvent?.Invoke(_overlaySchedule);
            }
        }

        /// <summary>
        /// Interrupt should pause after playback
        /// </summary>
        private void _scheduleManager_OnInterruptPausePending()
        {
            Debug.WriteLine("Interrupt Pause Pending Event", "Schedule");

            if (this._interrupting)
            {
                // Set Pause Pending on the current Interrupt Layout
                ScheduleChangeEvent?.Invoke(null, "pause-pending");
            }
        }

        /// <summary>
        /// Interrupt should happen now
        /// </summary>
        private void _scheduleManager_OnInterruptNow()
        {
            Debug.WriteLine("Interrupt Now Event", "Schedule");

            if (!this._interrupting && this._scheduleManager.CurrentInterruptSchedule.Count > 0)
            {
                // Remove overlays
                if (_overlaySchedule != null && _overlaySchedule.Count > 0)
                {
                    OverlayChangeEvent?.Invoke(new List<ScheduleItem>());
                }

                // Choose the interrupt in position 0
                ScheduleChangeEvent?.Invoke(this._scheduleManager.CurrentInterruptSchedule[0], "interrupt");
            }
        }

        /// <summary>
        /// Report the Play durarion of the current layout.
        /// </summary>
        /// <param name="scheduleId"></param>
        /// <param name="layoutId"></param>
        /// <param name="duration"></param>
        public void CurrentLayout_OnReportLayoutPlayDurationEvent(int scheduleId, int layoutId, double duration)
        {
            this._scheduleManager.InterruptRecordSecondsPlayed(scheduleId, duration);
        }

        #endregion
    }
}
