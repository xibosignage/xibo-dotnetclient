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

        // Schedule Agent
        private ScheduleAgent _scheduleAgent;
        Thread _scheduleAgentThread;

        // Required Files Agent
        private RequiredFilesAgent _requiredFilesAgent;
        Thread _requiredFilesAgentThread;

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
        }

        /// <summary>
        /// Initialize the Schedule components
        /// </summary>
        public void InitializeComponents() 
        {
            // The Timer for the Schedule Polling
            System.Windows.Forms.Timer scheduleTimer = new System.Windows.Forms.Timer();
            scheduleTimer.Interval = 10000; // 10 Seconds
            scheduleTimer.Tick += new EventHandler(scheduleTimer_Tick);
            scheduleTimer.Start();

            // Start the ScheduleAgent thread
            _scheduleAgentThread.Start();

            // Start the RequiredFilesAgent thread
            _requiredFilesAgentThread.Start();

            // We must have a schedule by now.
            UpdateLayoutSchedule(true);
        }

        /// <summary>
        /// Event handler for every schedule update timer tick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void scheduleTimer_Tick(object sender, EventArgs e)
        {
            Debug.WriteLine(string.Format("Schedule Timer Ticked at {0}. There are {1} items in the schedule.", DateTime.Now.ToString(), _layoutSchedule.Count.ToString()));

            // Ask the schedule manager if we need to clear the layoutSchedule collection
            UpdateLayoutSchedule(_scheduleManager.NewScheduleAvailable);
        }

        /// <summary>
        /// Updates the layout schedule
        /// Forces a new layout to load
        /// </summary>
        private void UpdateLayoutSchedule(bool forceChange)
        {
            _layoutSchedule = _scheduleManager.CurrentSchedule;

            // Do we need to force a change to the schedule?
            if (forceChange)
            {
                Debug.WriteLine("Forcing a change to the current schedule");

                // Set the current pointer to 0
                _currentLayout = 0;

                // Raise a schedule change event
                ScheduleChangeEvent(_layoutSchedule[0].layoutFile, _layoutSchedule[0].scheduleid, _layoutSchedule[0].id);
            }
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
            Debug.WriteLine("Layout file changed");

            // Are we set to expire modified layouts? If not then just return as if
            // nothing had happened.
            if (!Settings.Default.expireModifiedLayouts)
                return;

            // If the layout that got changed is the current layout, move on
            try
            {
                if (_layoutSchedule[_currentLayout].layoutFile == Properties.Settings.Default.LibraryPath + @"\" + layoutPath)
                {
                    _forceChange = true;
                    NextLayout();
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
            _scheduleAgent.forceStop = true;

            // Stop the requiredfiles agent
            _requiredFilesAgent.forceStop = true;
        }
    }
}
