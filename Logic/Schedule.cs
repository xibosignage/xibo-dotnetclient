/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006 - 2014 Daniel Garner
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
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Diagnostics;
using XiboClient.XmdsAgents;
using System.Threading;
using XiboClient.Properties;
using XiboClient.Log;
using XiboClient.Logic;
using XiboClient.Action;
using XiboClient.Control;

/// 17/02/12 Dan Removed Schedule call, introduced ScheduleAgent
/// 21/02/12 Dan Named the threads

namespace XiboClient
{
    /// <summary>
    /// Reads the schedule
    /// </summary>
    class Schedule
    {
        public delegate void ScheduleChangeDelegate(string layoutPath, int scheduleId, int layoutId);
        public event ScheduleChangeDelegate ScheduleChangeEvent;

        public delegate void OverlayChangeDelegate(Collection<ScheduleItem> overlays);
        public event OverlayChangeDelegate OverlayChangeEvent;

        /// <summary>
        /// Current Schedule of Normal Layouts
        /// </summary>
        private Collection<ScheduleItem> _layoutSchedule;
        private int _currentLayout = 0;

        /// <summary>
        /// Current Schedule of Overlay Layouts
        /// </summary>
        private Collection<ScheduleItem> _overlaySchedule;

        private string _scheduleLocation;

        /// <summary>
        /// The current layout id
        /// </summary>
        public int CurrentLayoutId
        {
            get
            {
                return _currentLayoutId;
            }
            set
            {
                _currentLayoutId = value;

                if (_scheduleManager != null)
                    _scheduleManager.CurrentLayoutId = _currentLayoutId;
            }
        }
        private int _currentLayoutId;

        private bool _forceChange = false;

        /// <summary>
        /// Has stop been called?
        /// </summary>
        private bool _stopCalled = false;

        // Key
        private HardwareKey _hardwareKey;

        // Cache Manager
        private CacheManager _cacheManager;

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

        /// <summary>
        /// Client Info Form
        /// </summary>
        private ClientInfo _clientInfoForm;

        /// <summary>
        /// Create a schedule
        /// </summary>
        /// <param name="scheduleLocation"></param>
        public Schedule(string scheduleLocation, ref CacheManager cacheManager, ref ClientInfo clientInfoForm)
        {
            Trace.WriteLine(string.Format("XMDS Location: {0}", ApplicationSettings.Default.XiboClient_xmds_xmds));

            // Get the key for this display
            _hardwareKey = new HardwareKey();

            // Save the schedule location
            _scheduleLocation = scheduleLocation;

            // Create a new collection for the layouts in the schedule
            _layoutSchedule = new Collection<ScheduleItem>();
            
            // Set cachemanager
            _cacheManager = cacheManager;

            // Set client info form
            _clientInfoForm = clientInfoForm;

            // Create a Register Agent
            _registerAgent = new RegisterAgent();
            _registerAgent.OnXmrReconfigure += _registerAgent_OnXmrReconfigure;
            _registerAgentThread = new Thread(new ThreadStart(_registerAgent.Run));
            _registerAgentThread.Name = "RegisterAgentThread";

            // Create a schedule manager
            _scheduleManager = new ScheduleManager(_cacheManager, scheduleLocation);
            _scheduleManager.OnNewScheduleAvailable += new ScheduleManager.OnNewScheduleAvailableDelegate(_scheduleManager_OnNewScheduleAvailable);
            _scheduleManager.OnRefreshSchedule += new ScheduleManager.OnRefreshScheduleDelegate(_scheduleManager_OnRefreshSchedule);
            _scheduleManager.OnScheduleManagerCheckComplete += _scheduleManager_OnScheduleManagerCheckComplete;
            _scheduleManager.ClientInfoForm = _clientInfoForm;

            // Create a schedule manager thread
            _scheduleManagerThread = new Thread(new ThreadStart(_scheduleManager.Run));
            _scheduleManagerThread.Name = "ScheduleManagerThread";

            // Create a RequiredFilesAgent
            _scheduleAndRfAgent = new ScheduleAndFilesAgent();
            _scheduleAndRfAgent.CurrentCacheManager = cacheManager;
            _scheduleAndRfAgent.CurrentScheduleManager = _scheduleManager;
            _scheduleAndRfAgent.ScheduleLocation = scheduleLocation;
            _scheduleAndRfAgent.HardwareKey = _hardwareKey.Key;
            _scheduleAndRfAgent.OnFullyProvisioned += _requiredFilesAgent_OnFullyProvisioned;
            _scheduleAndRfAgent.ClientInfoForm = _clientInfoForm;
            _scheduleAndRfAgent.OnComplete += new ScheduleAndFilesAgent.OnCompleteDelegate(LayoutFileModified);

            // Create a thread for the RequiredFiles Agent to run in - but dont start it up yet.
            _scheduleAndRfAgentThread = new Thread(new ThreadStart(_scheduleAndRfAgent.Run));
            _scheduleAndRfAgentThread.Name = "RequiredFilesAgentThread";

            // Library Agent
            _libraryAgent = new LibraryAgent();
            _libraryAgent.CurrentCacheManager = _cacheManager;
            
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
            _xmrSubscriber.ClientInfoForm = _clientInfoForm;
            _xmrSubscriber.OnAction += _xmrSubscriber_OnAction;

            // Thread start
            _xmrSubscriberThread = new Thread(new ThreadStart(_xmrSubscriber.Run));
            _xmrSubscriberThread.Name = "XmrSubscriber";

            // Embedded Server
            _server = new EmbeddedServer();
            _server.ClientInfoForm = _clientInfoForm;
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
            Debug.WriteLine("New Schedule Available", "Schedule");
            Debug.WriteLine(_scheduleManager.CurrentOverlaySchedule.Count + " overlays", "Schedule");
            Debug.WriteLine(_scheduleManager.CurrentSchedule.Count + " normal schedules", "Schedule");

            _overlaySchedule = new Collection<ScheduleItem>(_scheduleManager.CurrentOverlaySchedule);
            _layoutSchedule = _scheduleManager.CurrentSchedule;

            // Set the current pointer to 0
            _currentLayout = 0;

            // Raise a schedule change event
            ScheduleChangeEvent(_layoutSchedule[0].layoutFile, _layoutSchedule[0].scheduleid, _layoutSchedule[0].id);

            // Pass a new set of overlay's to subscribers
            if (OverlayChangeEvent != null)
                OverlayChangeEvent(_overlaySchedule);
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
                _clientInfoForm.UpdateStatusMarkerFile();
            }
            else
            {
                Trace.WriteLine(new LogMessage("Schedule - OnScheduleManagerCheckComplete", "Agent threads/XMR is dead, not updating status.json"), LogType.Error.ToString());
            }

            // Log for overdue XMR
            if (xmrShouldBeRunning && _xmrSubscriber.LastHeartBeat < DateTime.Now.AddHours(-1))
            {
                _clientInfoForm.XmrSubscriberStatus = "Long term Inactive (" + ApplicationSettings.Default.XmrNetworkAddress + "), last activity: " + _xmrSubscriber.LastHeartBeat.ToString();
                Trace.WriteLine(new LogMessage("Schedule - OnScheduleManagerCheckComplete", "XMR heart beat last received over an hour ago."));

                // Issue an XMR restart if we've gone this long without connecting
                // we do this because we suspect that the TCP socket has died without notifying the poller
                restartXmr();
            }
            else if (xmrShouldBeRunning && _xmrSubscriber.LastHeartBeat < DateTime.Now.AddMinutes(-5))
            {
                _clientInfoForm.XmrSubscriberStatus = "Inactive (" + ApplicationSettings.Default.XmrNetworkAddress + "), last activity: " + _xmrSubscriber.LastHeartBeat.ToString();
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
                    else {
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
            int previousLayout = _currentLayout;

            // See if the current layout is an action that can be removed.
            // If it CAN be removed then this will almost certainly result in a change in the current _layoutSchedule
            // therefore we should return out of this and kick off a schedule manager cycle, which will set the new layout.
            try
            {
                if (_scheduleManager.removeLayoutChangeActionIfComplete(_layoutSchedule[previousLayout]))
                {
                    _scheduleManager.RunNow();
                    return;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Schedule - NextLayout", "Unable to check layout change actions. E = " + e.Message), LogType.Error.ToString());
            }

            // increment the current layout
            _currentLayout++;

            // if the current layout is greater than the count of layouts, then reset to 0
            if (_currentLayout >= _layoutSchedule.Count)
            {
                _currentLayout = 0;
            }

            if (_layoutSchedule.Count == 1 && !_forceChange)
            {
                Debug.WriteLine(new LogMessage("Schedule - NextLayout", "Only 1 layout showing, refreshing it"), LogType.Info.ToString());
            }

            Debug.WriteLine(String.Format("Next layout: {0}", _layoutSchedule[_currentLayout].layoutFile), "Schedule - Next Layout");

            _forceChange = false;

            // Raise the event
            ScheduleChangeEvent(_layoutSchedule[_currentLayout].layoutFile, _layoutSchedule[_currentLayout].scheduleid, _layoutSchedule[_currentLayout].id);
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
        /// A layout file has changed
        /// </summary>
        /// <param name="layoutPath"></param>
        private void LayoutFileModified(string layoutPath)
        {
            Trace.WriteLine(new LogMessage("Schedule - LayoutFileModified", "Layout file changed: " + layoutPath), LogType.Info.ToString());

            // Are we set to expire modified layouts? If not then just return as if
            // nothing had happened.
            if (!ApplicationSettings.Default.ExpireModifiedLayouts)
                return;

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

            if (OverlayChangeEvent != null && changeRequired)
                OverlayChangeEvent(_overlaySchedule);

            // If the layout that got changed is the current layout, move on
            try
            {
                if (_layoutSchedule[_currentLayout].layoutFile == ApplicationSettings.Default.LibraryPath + @"\" + layoutPath)
                {
                    // What happens if the action of downloading actually invalidates this layout?
                    bool valid = _cacheManager.IsValidPath(layoutPath);

                    if (valid)
                    {
                        // Check dependents
                        foreach (string dependent in _layoutSchedule[_currentLayout].Dependents)
                        {
                            if (!string.IsNullOrEmpty(dependent) && !_cacheManager.IsValidPath(dependent))
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

                        // Force a change
                        _forceChange = true;

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

            // Stop the LibraryAgent Thread
            _libraryAgent.Stop();

            // Stop the LogAgent Thread
            _logAgent.Stop();

            // Stop the subsriber thread
            _xmrSubscriber.Stop();

            // Clean up any NetMQ sockets, etc (false means don't block).
            NetMQ.NetMQConfig.Cleanup(false);

            // Stop the embedded server
            _server.Stop();
        }
    }
}
