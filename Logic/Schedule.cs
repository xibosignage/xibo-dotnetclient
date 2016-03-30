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

        private Collection<ScheduleItem> _layoutSchedule;
        private int _currentLayout = 0;
        private string _scheduleLocation;

        private bool _forceChange = false;

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

        // Schedule Agent
        private ScheduleAgent _scheduleAgent;
        Thread _scheduleAgentThread;

        // Required Files Agent
        private RequiredFilesAgent _requiredFilesAgent;
        Thread _requiredFilesAgentThread;

        // Library Agent
        private LibraryAgent _libraryAgent;
        Thread _libraryAgentThread;

        // Log Agent
        private LogAgent _logAgent;
        Thread _logAgentThread;

        // XMR Subscriber
        private XmrSubscriber _xmrSubscriber;
        Thread _xmrSubscriberThread;

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

            // Create a Schedule Agent
            _scheduleAgent = new ScheduleAgent();
            _scheduleAgent.CurrentScheduleManager = _scheduleManager;
            _scheduleAgent.ScheduleLocation = scheduleLocation;
            _scheduleAgent.HardwareKey = _hardwareKey.Key;
            _scheduleAgent.ClientInfoForm = _clientInfoForm;

            // Create a thread for the Schedule Agent to run in - but dont start it up yet.
            _scheduleAgentThread = new Thread(new ThreadStart(_scheduleAgent.Run));
            _scheduleAgentThread.Name = "ScheduleAgentThread";

            // Create a RequiredFilesAgent
            _requiredFilesAgent = new RequiredFilesAgent();
            _requiredFilesAgent.CurrentCacheManager = cacheManager;
            _requiredFilesAgent.HardwareKey = _hardwareKey.Key;
            _requiredFilesAgent.ClientInfoForm = _clientInfoForm;
            _requiredFilesAgent.OnComplete += new RequiredFilesAgent.OnCompleteDelegate(LayoutFileModified);
            _requiredFilesAgent.OnFullyProvisioned += _requiredFilesAgent_OnFullyProvisioned;

            // Create a thread for the RequiredFiles Agent to run in - but dont start it up yet.
            _requiredFilesAgentThread = new Thread(new ThreadStart(_requiredFilesAgent.Run));
            _requiredFilesAgentThread.Name = "RequiredFilesAgentThread";

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
        }

        /// <summary>
        /// Initialize the Schedule components
        /// </summary>
        public void InitializeComponents() 
        {
            // Start the RegisterAgent thread
            _registerAgentThread.Start();

            // Start the ScheduleAgent thread
            _scheduleAgentThread.Start();

            // Start the RequiredFilesAgent thread
            _requiredFilesAgentThread.Start();

            // Start the ScheduleManager thread
            _scheduleManagerThread.Start();

            // Start the LibraryAgent thread
            _libraryAgentThread.Start();

            // Start the LogAgent thread
            _logAgentThread.Start();

            // Start the subscriber thread
            _xmrSubscriberThread.Start();
        }

        /// <summary>
        /// New Schedule Available
        /// </summary>
        private void _scheduleManager_OnNewScheduleAvailable()
        {
            _layoutSchedule = _scheduleManager.CurrentSchedule;

            // Set the current pointer to 0
            _currentLayout = 0;

            // Raise a schedule change event
            ScheduleChangeEvent(_layoutSchedule[0].layoutFile, _layoutSchedule[0].scheduleid, _layoutSchedule[0].id);
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
            try
            {
                // See if XMR should be running
                if (!string.IsNullOrEmpty(ApplicationSettings.Default.XmrNetworkAddress) && _xmrSubscriber.LastHeartBeat != DateTime.MinValue)
                {
                    // Check to see if the last update date was over 5 minutes ago
                    if (_xmrSubscriber.LastHeartBeat < DateTime.Now.AddSeconds(-90))
                    {
                        // Reconfigure it
                        _registerAgent_OnXmrReconfigure();   
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Schedule - OnScheduleManagerCheckComplete", "Error = " + e.Message), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// XMR Reconfigure
        /// </summary>
        void _registerAgent_OnXmrReconfigure()
        {
            try
            {
                // Stop and start the XMR thread
                if (_xmrSubscriberThread != null && _xmrSubscriberThread.IsAlive)
                {
                    _xmrSubscriberThread.Abort();
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Schedule - OnXmrReconfigure", "Unable to abort Subscriber. " + e.Message), LogType.Error.ToString());
            }

            try
            {
                // Reassert the hardware key, incase its changed at all
                _xmrSubscriber.HardwareKey = _hardwareKey;
                
                // Start the thread again
                _xmrSubscriberThread = new Thread(new ThreadStart(_xmrSubscriber.Run));
                _xmrSubscriberThread.Name = "XmrSubscriber";

                _xmrSubscriberThread.Start();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Schedule - OnXmrReconfigure", "Unable to start Subscriber. " + e.Message), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// XMR Subscriber Action
        /// </summary>
        void _xmrSubscriber_OnAction(Action.PlayerActionInterface action)
        {
            switch (action.GetActionName())
            {
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
            }
        }

        /// <summary>
        /// Required files fully provisioned
        /// </summary>
        private void _requiredFilesAgent_OnFullyProvisioned()
        {
            // Mark all layout change actions as downloaded and assess the schedule
            _scheduleManager.setAllLayoutChangeActionsDownloaded();
            _scheduleManager.RunNow();
        }

        /// <summary>
        /// Wake up all XMDS services
        /// </summary>
        public void wakeUpXmds()
        {
            _registerAgent.WakeUp();
            _scheduleAgent.WakeUp();
            _requiredFilesAgent.WakeUp();
            _logAgent.WakeUp();
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
                if (_scheduleManager.isLayoutChangeActionComplete(_layoutSchedule[previousLayout]))
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

            // Tell the schedule to refresh
            _scheduleManager.RefreshSchedule = true;

            // Are we set to expire modified layouts? If not then just return as if
            // nothing had happened.
            if (!ApplicationSettings.Default.ExpireModifiedLayouts)
                return;

            // If the layout that got changed is the current layout, move on
            try
            {
                if (_layoutSchedule[_currentLayout].layoutFile == ApplicationSettings.Default.LibraryPath + @"\" + layoutPath)
                {
                    // What happens if the action of downloading actually invalidates this layout?
                    if (!_cacheManager.IsValidLayout(layoutPath))
                    {
                        Trace.WriteLine(new LogMessage("Schedule - LayoutFileModified", "The current layout is now invalid, refreshing the current schedule."), LogType.Audit.ToString());

                        // We should not force a change and we should tell the schedule manager to run now
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
        /// Stops the Schedule Object
        /// </summary>
        public void Stop()
        {
            // Stop the register agent
            _registerAgent.Stop();

            // Stop the schedule agent
            _scheduleAgent.Stop();

            // Stop the requiredfiles agent
            _requiredFilesAgent.Stop();

            // Stop the Schedule Manager Thread
            _scheduleManager.Stop();

            // Stop the LibraryAgent Thread
            _libraryAgent.Stop();

            // Stop the LogAgent Thread
            _logAgent.Stop();

            // Stop the subsriber thread
            _xmrSubscriberThread.Abort();
        }
    }
}
