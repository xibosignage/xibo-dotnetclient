/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006 - 2010 Daniel Garner and James Packer
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

        private Collection<LayoutSchedule> _layoutSchedule;
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

        // Schedule Agent
        private ScheduleAgent _scheduleAgent;
        Thread _scheduleAgentThread;

        // Required Files Agent
        private RequiredFilesAgent _requiredFilesAgent;
        Thread _requiredFilesAgentThread;

        // Library Agent
        private LibraryAgent _libraryAgent;
        Thread _libraryAgentThread;

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
            Trace.WriteLine(string.Format("XMDS Location: {0}", Properties.Settings.Default.XiboClient_xmds_xmds));

            // Get the key for this display
            _hardwareKey = new HardwareKey();

            // Save the schedule location
            _scheduleLocation = scheduleLocation;

            // Create a new collection for the layouts in the schedule
            _layoutSchedule = new Collection<LayoutSchedule>();
            
            // Set cachemanager
            _cacheManager = cacheManager;

            // Set client info form
            _clientInfoForm = clientInfoForm;

            // Create a schedule manager
            _scheduleManager = new ScheduleManager(_cacheManager, scheduleLocation);
            _scheduleManager.OnNewScheduleAvailable += new ScheduleManager.OnNewScheduleAvailableDelegate(_scheduleManager_OnNewScheduleAvailable);
            _scheduleManager.OnRefreshSchedule += new ScheduleManager.OnRefreshScheduleDelegate(_scheduleManager_OnRefreshSchedule);
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

            // Create a thread for the RequiredFiles Agent to run in - but dont start it up yet.
            _requiredFilesAgentThread = new Thread(new ThreadStart(_requiredFilesAgent.Run));
            _requiredFilesAgentThread.Name = "RequiredFilesAgentThread";

            // Library Agent
            _libraryAgent = new LibraryAgent();
            _libraryAgent.CurrentCacheManager = _cacheManager;
            
            // Create a thread for the Library Agent to run in - but dont start it up yet.
            _libraryAgentThread = new Thread(new ThreadStart(_libraryAgent.Run));
            _libraryAgentThread.Name = "LibraryAgent";
        }

        /// <summary>
        /// Initialize the Schedule components
        /// </summary>
        public void InitializeComponents() 
        {
            // Start the ScheduleAgent thread
            _scheduleAgentThread.Start();

            // Start the RequiredFilesAgent thread
            _requiredFilesAgentThread.Start();

            // Start the ScheduleManager thread
            _scheduleManagerThread.Start();

            // Start the LibraryAgent thread
            _libraryAgentThread.Start();
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
        /// Moves the layout on
        /// </summary>
        public void NextLayout()
        {
            int previousLayout = _currentLayout;

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
            if (!Settings.Default.expireModifiedLayouts)
                return;

            // If the layout that got changed is the current layout, move on
            try
            {
                if (_layoutSchedule[_currentLayout].layoutFile == Settings.Default.LibraryPath + @"\" + layoutPath)
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
            // Stop the schedule agent
            _scheduleAgent.Stop();

            // Stop the requiredfiles agent
            _requiredFilesAgent.Stop();

            // Stop the Schedule Manager Thread
            _scheduleManager.Stop();

            // Stop the LibraryAgent Thread
            _libraryAgent.Stop();
        }
    }
}
