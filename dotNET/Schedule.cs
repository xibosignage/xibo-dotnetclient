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

        // FileCollector
        private XiboClient.xmds.xmds _xmds2;
        private bool _xmdsProcessing;
        private bool _forceChange = false;

        // Key
        private HardwareKey _hardwareKey;

        // Cache Manager
        private CacheManager _cacheManager;

        // Schedule Manager
        private ScheduleManager _scheduleManager;

        /// <summary>
        /// Create a schedule
        /// </summary>
        /// <param name="scheduleLocation"></param>
        public Schedule(string scheduleLocation, ref CacheManager cacheManager)
        {
            Debug.WriteLine(string.Format("XMDS Location: {0}", Properties.Settings.Default.XiboClient_xmds_xmds));

            // Save the schedule location
            _scheduleLocation = scheduleLocation;

            // Create a new collection for the layouts in the schedule
            _layoutSchedule = new Collection<LayoutSchedule>();
            
            // Set cachemanager
            _cacheManager = cacheManager;

            // Create a schedule manager
            _scheduleManager = new ScheduleManager(_cacheManager, scheduleLocation);

            // Create a new Xmds service object
            _xmds2 = new XiboClient.xmds.xmds();
        }

        /// <summary>
        /// Initialize the Schedule components
        /// </summary>
        public void InitializeComponents() 
        {
            // Get the key for this display
            _hardwareKey = new HardwareKey();

            // Start up the Xmds Service Object
            _xmds2.Credentials = null;
            _xmds2.Url = Properties.Settings.Default.XiboClient_xmds_xmds;
            _xmds2.UseDefaultCredentials = false;

            _xmdsProcessing = false;
            _xmds2.RequiredFilesCompleted += new XiboClient.xmds.RequiredFilesCompletedEventHandler(xmds2_RequiredFilesCompleted);
            _xmds2.ScheduleCompleted += new XiboClient.xmds.ScheduleCompletedEventHandler(xmds2_ScheduleCompleted);

            Trace.WriteLine(String.Format("Collection Interval: {0}", Properties.Settings.Default.collectInterval), "Schedule - InitializeComponents");

            // The Timer for the Service call
            Timer xmdsTimer = new Timer();
            xmdsTimer.Interval = (int) Properties.Settings.Default.collectInterval * 1000;
            xmdsTimer.Tick += new EventHandler(xmdsTimer_Tick);
            xmdsTimer.Start();

            // The Timer for the Schedule Polling
            Timer scheduleTimer = new Timer();
            scheduleTimer.Interval = 10000; // 10 Seconds
            scheduleTimer.Tick += new EventHandler(scheduleTimer_Tick);
            scheduleTimer.Start();

            // Manual first tick
            _xmdsProcessing = true;

            // We must have a schedule by now.
            UpdateLayoutSchedule(true);

            // Fire off a get required files event - async
            _xmds2.RequiredFilesAsync(Properties.Settings.Default.ServerKey, _hardwareKey.Key, Properties.Settings.Default.Version);
        }

        /// <summary>
        /// Event handler for every schedule update timer tick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void scheduleTimer_Tick(object sender, EventArgs e)
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
        /// XMDS timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void xmdsTimer_Tick(object sender, EventArgs e)
        {
            // The Date/time of last XMDS is recorded - this cannot be longer that the xmdsProcessingTimeout flag
            DateTime lastXmdsSuccess = Properties.Settings.Default.XmdsLastConnection;
            int xmdsResetTimeout = Properties.Settings.Default.xmdsResetTimeout;

            // Work out if XMDS has been active for longer than the reset period (means its crashed)
            if (lastXmdsSuccess < (DateTime.Now.AddSeconds(-1 * xmdsResetTimeout)))
            {
                Trace.WriteLine(new LogMessage("xmdsTimer_Tick", String.Format("XMDS reset, last connection was at {0}", lastXmdsSuccess.ToString())), LogType.Error.ToString());
                _xmdsProcessing = false;
            }

            // Ticks every "collectInterval"
            if (_xmdsProcessing)
            {
                System.Diagnostics.Debug.WriteLine("Collection Timer Ticked, but previous request still active", "XmdsTicker");
                return;
            }
            else
            {
                Application.DoEvents(); // Make sure everything that is queued to render does

                _xmdsProcessing = true;

                System.Diagnostics.Debug.WriteLine("Collection Timer Ticked, Firing RequiredFilesAsync");

                // Fire off a get required files event - async
                _xmds2.RequiredFilesAsync(Properties.Settings.Default.ServerKey, _hardwareKey.Key, Properties.Settings.Default.Version);
            }

            // Flush the log
            System.Diagnostics.Trace.Flush();
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

        #region "Event Handers"

        /// <summary>
        /// Event Handler for when the FileCollector has changed a media file
        /// </summary>
        /// <param name="path"></param>
        void fileCollector_MediaFileChanged(string path)
        {
            System.Diagnostics.Debug.WriteLine("Media file changed");
            return;
        }

        /// <summary>
        /// Event Handler for when the FileCollector has finished file collection cycle
        /// </summary>
        void fileCollector_CollectionComplete()
        {
            System.Diagnostics.Debug.WriteLine("File Collector Complete - getting Schedule.");

            // All the files have been collected, so we want to update the schedule (do we want to send off a MD5 of the schedule?)
            _xmds2.ScheduleAsync(Properties.Settings.Default.ServerKey, _hardwareKey.Key, Properties.Settings.Default.Version);
        }

        /// <summary>
        /// Schedule XMDS call completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void xmds2_ScheduleCompleted(object sender, XiboClient.xmds.ScheduleCompletedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Schedule Retrival Complete.");

            // Set XMDS to no longer be processing
            _xmdsProcessing = false;

            // Expect new schedule XML
            if (e.Error != null)
            {
                // There was an error - what do we do?
                System.Diagnostics.Trace.WriteLine(e.Error.Message);
            }
            else
            {
                // Only update the schedule if its changed.
                String md5CurrentSchedule = "";

                // Set the flag to indicate we have a connection to XMDS
                Properties.Settings.Default.XmdsLastConnection = DateTime.Now;

                // Hash of the result
                String md5NewSchedule = Hashes.MD5(e.Result);

                try
                {
                    StreamReader sr = new StreamReader(File.Open(_scheduleLocation, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));

                    // Yes - get the MD5 of it, and compare to the MD5 of the file in the XML
                    md5CurrentSchedule = Hashes.MD5(sr.ReadToEnd());

                    sr.Close();

                    // Compare the existing to the new
                    if (md5CurrentSchedule == md5NewSchedule) return;
                }
                catch (Exception ex)
                {
                    // Failed to get the MD5 of the existing schedule - just continue and overwrite it
                    Debug.WriteLine(ex.Message);
                }

                System.Diagnostics.Debug.WriteLine("Different Schedules Detected, writing new schedule.", "Schedule - ScheduleCompleted");

                // Write the result to the schedule xml location
                StreamWriter sw = new StreamWriter(_scheduleLocation, false, Encoding.UTF8);
                sw.Write(e.Result);

                sw.Close();

                Debug.WriteLine("New Schedule Recieved", "xmds_ScheduleCompleted");

                // Indicate to the schedule manager that it should read the XML file
                _scheduleManager.RefreshSchedule = true;
            }
        }

        /// <summary>
        /// A layout file has changed
        /// </summary>
        /// <param name="layoutPath"></param>
        void fileCollector_LayoutFileChanged(string layoutPath)
        {
            System.Diagnostics.Debug.WriteLine("Layout file changed");

            // Are we set to expire modified layouts? If not then just return as if
            // nothing had happened.
            if (!Properties.Settings.Default.expireModifiedLayouts)
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
        /// Event Handler for required files being complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void xmds2_RequiredFilesCompleted(object sender, XiboClient.xmds.RequiredFilesCompletedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("RequiredFilesAsync complete.", "Schedule - RequiredFilesCompleted");

            //Dont let this effect the rendering
            //Application.DoEvents();

            if (e.Error != null)
            {
                // There was an error - what do we do?
                System.Diagnostics.Trace.WriteLine(new LogMessage("Schedule - RequiredFilesCompleted", e.Error.Message), LogType.Error.ToString());

                // Is it a "not licensed" error
                if (e.Error.Message == "This display client is not licensed")
                {
                    Properties.Settings.Default.licensed = 0;
                }

                _xmdsProcessing = false;
            }
            else
            {
                // Set the flag to indicate we have a connection to XMDS
                Properties.Settings.Default.XmdsLastConnection = DateTime.Now;

                // Firstly we know we are licensed if we get this far
                if (Properties.Settings.Default.licensed == 0)
                {
                    Properties.Settings.Default.licensed = 1;
                }

                try
                {
                    // Load the result into XML
                    FileCollector fileCollector = new FileCollector(_cacheManager, e.Result);

                    // Bind some events that the fileCollector will raise
                    fileCollector.LayoutFileChanged += new FileCollector.LayoutFileChangedDelegate(fileCollector_LayoutFileChanged);
                    fileCollector.CollectionComplete += new FileCollector.CollectionCompleteDelegate(fileCollector_CollectionComplete);
                    fileCollector.MediaFileChanged += new FileCollector.MediaFileChangedDelegate(fileCollector_MediaFileChanged);

                    fileCollector.CompareAndCollect();
                }
                catch (Exception ex)
                {
                    _xmdsProcessing = false;

                    // Log and move on
                    System.Diagnostics.Trace.WriteLine(new LogMessage("Schedule - RequiredFilesCompleted", "Error Comparing and Collecting: " + ex.Message), LogType.Error.ToString());
                }

                _cacheManager.WriteCacheManager();
            }
        }

        #endregion
    }
}
