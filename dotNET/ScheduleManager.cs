/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2012 Daniel Garner
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
using XiboClient.Log;
using System.Threading;

/// 17/02/12 Dan Added a static method to get the schedule XML from disk into a string and to write it to the disk
/// 20/02/12 Dan Tweaked log types on a few trace messages
/// 24/03/12 Dan Move onto its own thread

namespace XiboClient
{
    /// <summary>
    /// Schedule manager controls the currently running schedule
    /// </summary>
    class ScheduleManager
    {
        #region "Constructor"

        // Thread Logic
        public static object _locker = new object();
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        // Event for new schedule
        public delegate void OnNewScheduleAvailableDelegate();
        public event OnNewScheduleAvailableDelegate OnNewScheduleAvailable;

        public delegate void OnRefreshScheduleDelegate();
        public event OnRefreshScheduleDelegate OnRefreshSchedule;

        // Member Varialbes
        private string _location;
        private Collection<LayoutSchedule> _layoutSchedule;
        private Collection<LayoutSchedule> _currentSchedule;
        private bool _refreshSchedule;
        private CacheManager _cacheManager;

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
        /// Creates a new schedule Manager
        /// </summary>
        /// <param name="scheduleLocation"></param>
        public ScheduleManager(CacheManager cacheManager, string scheduleLocation)
        {
            _cacheManager = cacheManager;
            _location = scheduleLocation;

            // Create an empty layout schedule
            _layoutSchedule = new Collection<LayoutSchedule>();
            _currentSchedule = new Collection<LayoutSchedule>();
        }

        #endregion

        #region "Properties"

        /// <summary>
        /// Tell the schedule manager to Refresh the Schedule
        /// </summary>
        public bool RefreshSchedule
        {
            get
            {
                return _refreshSchedule;
            }
            set
            {
                lock (_locker)
                    _refreshSchedule = value;
            }
        }

        /// <summary>
        /// The current layout schedule
        /// </summary>
        public Collection<LayoutSchedule> CurrentSchedule
        {
            get
            {
                return _currentSchedule;
            }
        }

        #endregion

        #region "Methods"

        /// <summary>
        /// Stops the thread
        /// </summary>
        public void Stop()
        {
            _forceStop = true;
            _manualReset.Set();
        }

        /// <summary>
        /// Runs the schedule manager now
        /// </summary>
        public void RunNow()
        {
            _manualReset.Set();
        }

        /// <summary>
        /// Runs the Schedule Manager
        /// </summary>
        public void Run()
        {
            Trace.WriteLine(new LogMessage("ScheduleManager - Run", "Thread Started"), LogType.Info.ToString());

            while (!_forceStop)
            {
                lock (_locker)
                {
                    try
                    {
                        // If we are restarting, reset
                        _manualReset.Reset();

                        Trace.WriteLine(new LogMessage("ScheduleManager - Run", "Schedule Timer Ticked"), LogType.Audit.ToString());

                        // Work out if there is a new schedule available, if so - raise the event
                        if (IsNewScheduleAvailable())
                            OnNewScheduleAvailable();
                        else
                            OnRefreshSchedule();

                        // Update the client info form
                        _clientInfoForm.ScheduleManagerStatus = LayoutsInSchedule();
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("ScheduleManager - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                        _clientInfoForm.ScheduleStatus = "Error. " + ex.Message;
                    }
                }

                // Sleep this thread for 10 seconds
                _manualReset.WaitOne(10 * 1000);
            }

            Trace.WriteLine(new LogMessage("ScheduleManager - Run", "Thread Stopped"), LogType.Info.ToString());
        }

        /// <summary>
        /// Determine if there is a new schedule available
        /// </summary>
        /// <returns></returns>
        private bool IsNewScheduleAvailable()
        {
            // If we dont currently have a cached schedule load one from the scheduleLocation
            // also do this if we have been told to Refresh the schedule
            if (_layoutSchedule.Count == 0 || RefreshSchedule)
            {
                // Try to load the schedule from disk
                try
                {
                    LoadScheduleFromFile();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("IsNewScheduleAvailable", string.Format("Unable to load schedule from disk: {0}", ex.Message)), 
                        LogType.Error.ToString());

                    // If we cant load the schedule from disk then use an empty schedule.
                    SetEmptySchedule();
                }

                // Set RefreshSchedule to be false (this means we will not need to load the file constantly)
                RefreshSchedule = false;
            }

            // Load the new Schedule
            Collection<LayoutSchedule> newSchedule = LoadNewSchdule();

            bool forceChange = false;

            // If the current schedule is empty, always overwrite
            if (_currentSchedule.Count == 0)
                forceChange = true;

            // Are all the items that were in the _currentSchedule still there?
            foreach (LayoutSchedule layout in _currentSchedule)
            {
                if (!newSchedule.Contains(layout))
                    forceChange = true;
            }

            // Set the new schedule
            _currentSchedule = newSchedule;

            // Clear up
            newSchedule = null;

            // Return True if we want to refresh the schedule OR false if we are OK to leave the current one.
            // We can update the current schedule and still return false - this will not trigger a schedule change event.
            // We do this if ALL the current layouts are still in the schedule
            return forceChange;
        }

        /// <summary>
        /// Loads a new schedule from _layoutSchedules
        /// </summary>
        /// <returns></returns>
        private Collection<LayoutSchedule> LoadNewSchdule()
        {
            // We need to build the current schedule from the layout schedule (obeying date/time)
            Collection<LayoutSchedule> newSchedule = new Collection<LayoutSchedule>();
            Collection<LayoutSchedule> prioritySchedule = new Collection<LayoutSchedule>();
            
            // Temporary default Layout incase we have no layout nodes.
            LayoutSchedule defaultLayout = new LayoutSchedule();

            // For each layout in the schedule determine if it is currently inside the _currentSchedule, and whether it should be
            foreach (LayoutSchedule layout in _layoutSchedule)
            {
                // Is the layout valid in the cachemanager?
                try
                {
                    if (!_cacheManager.IsValidLayout(layout.id + ".xlf"))
                    {
                        Trace.WriteLine(new LogMessage("ScheduleManager - LoadNewSchedule", "Layout invalid: " + layout.id), LogType.Error.ToString());
                        continue;
                    }
                }
                catch
                {
                    // Ignore this layout.. raise an error?
                    Trace.WriteLine(new LogMessage("ScheduleManager - LoadNewSchedule", "Unable to determine if layout is valid or not"), LogType.Error.ToString());
                    continue;
                }

                // If this is the default, skip it
                if (layout.NodeName == "default")
                {
                    // Store it before skipping it
                    defaultLayout = layout;
                    continue;
                }

                // Look at the Date/Time to see if it should be on the schedule or not
                if (layout.FromDt <= DateTime.Now && layout.ToDt >= DateTime.Now)
                {
                    // Priority layouts should generate their own list
                    if (layout.Priority)
                    {
                        prioritySchedule.Add(layout);
                    }
                    else
                    {
                        newSchedule.Add(layout);
                    }
                }
            }

            // If we have any priority schedules then we need to return those instead
            if (prioritySchedule.Count > 0)
                return prioritySchedule;

            // If the current schedule is empty by the end of all this, then slip the default in
            if (newSchedule.Count == 0)
                newSchedule.Add(defaultLayout);

            return newSchedule;
        }

        /// <summary>
        /// Loads the schedule from file.
        /// </summary>
        /// <returns></returns>
        private void LoadScheduleFromFile()
        {
            // Empty the current schedule collection
            _layoutSchedule.Clear();

            // Get the schedule XML
            XmlDocument scheduleXml = GetScheduleXml();

            // Parse the schedule xml
            XmlNodeList nodes = scheduleXml["schedule"].ChildNodes;

            // Are there any nodes in the document
            if (nodes.Count == 0)
            {
                SetEmptySchedule();
                return;
            }

            // We have nodes, go through each one and add them to the layoutschedule collection
            foreach (XmlNode node in nodes)
            {
                LayoutSchedule temp = new LayoutSchedule();

                // Node name
                temp.NodeName = node.Name;

                // Pull attributes from layout nodes
                XmlAttributeCollection attributes = node.Attributes;

                // All nodes have file properties
                temp.layoutFile = attributes["file"].Value;
                
                // Replace the .xml extension with nothing
                string replace = ".xml";
                string layoutFile = temp.layoutFile.TrimEnd(replace.ToCharArray());

                // Set these on the temp layoutschedule
                temp.layoutFile = Properties.Settings.Default.LibraryPath + @"\" + layoutFile + @".xlf";
                temp.id = int.Parse(layoutFile);

                // Get attributes that only exist on the default
                if (temp.NodeName != "default")
                {
                    // Priority flag
                    temp.Priority = (attributes["priority"].Value == "1") ? true : false;

                    // Get the fromdt,todt
                    temp.FromDt = DateTime.Parse(attributes["fromdt"].Value);
                    temp.ToDt = DateTime.Parse(attributes["todt"].Value);

                    // Pull out the scheduleid if there is one
                    string scheduleId = "";
                    if (attributes["scheduleid"] != null) scheduleId = attributes["scheduleid"].Value;

                    // Add it to the layout schedule
                    if (scheduleId != "") temp.scheduleid = int.Parse(scheduleId);
                }

                _layoutSchedule.Add(temp);
            }

            // Clean up
            nodes = null;
            scheduleXml = null;

            // We now have the saved XML contained in the _layoutSchedule object
        }

        /// <summary>
        /// Sets an empty schedule into the _layoutSchedule Collection
        /// </summary>
        private void SetEmptySchedule()
        {
            Debug.WriteLine("Setting an empty schedule", LogType.Info.ToString());

            // Remove the existing schedule
            _layoutSchedule.Clear();

            // Schedule up the default
            LayoutSchedule temp = new LayoutSchedule();
            temp.layoutFile = Properties.Settings.Default.LibraryPath + @"\Default.xml";
            temp.id = 0;
            temp.scheduleid = 0;

            _layoutSchedule.Add(temp);
        }

        /// <summary>
        /// Gets the Schedule XML
        /// </summary>
        /// <returns></returns>
        private XmlDocument GetScheduleXml()
        {
            Debug.WriteLine("Getting the Schedule XML", LogType.Info.ToString());
            
            XmlDocument scheduleXml;
            
            // Check the schedule file exists
            if (File.Exists(_location))
            {
                // Read the schedule file
                XmlReader reader = XmlReader.Create(_location);

                scheduleXml = new XmlDocument();
                scheduleXml.Load(reader);

                reader.Close();
            }
            else
            {
                // Use the default XML
                scheduleXml = new XmlDocument();
                scheduleXml.LoadXml("<schedule></schedule>");
            }

            return scheduleXml;
        }

        /// <summary>
        /// Get the schedule XML from Disk into a string
        /// </summary>
        /// <param name="scheduleLocation"></param>
        /// <returns></returns>
        public static string GetScheduleXmlString(string scheduleLocation)
        {
            lock (_locker)
            {
                Trace.WriteLine(new LogMessage("ScheduleManager - GetScheduleXmlString", "Getting the Schedule XML"), LogType.Audit.ToString());

                string scheduleXml;

                // Check the schedule file exists
                try
                {
                    // Read the schedule file
                    using (StreamReader sr = new StreamReader(File.Open(scheduleLocation, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)))
                    {
                        scheduleXml = sr.ReadToEnd();
                    }
                }
                catch (FileNotFoundException)
                {
                    // Use the default XML
                    scheduleXml = "<schedule></schedule>";
                }

                return scheduleXml;
            }
        }

        /// <summary>
        /// Write the Schedule XML to disk from a String
        /// </summary>
        /// <param name="scheduleLocation"></param>
        /// <param name="scheduleXml"></param>
        public static void WriteScheduleXmlToDisk(string scheduleLocation, string scheduleXml)
        {
            lock (_locker)
            {
                using (StreamWriter sw = new StreamWriter(scheduleLocation, false, Encoding.UTF8))
                {
                    sw.Write(scheduleXml);
                }
            }
        }

        /// <summary>
        /// List of Layouts in the Schedule
        /// </summary>
        /// <returns></returns>
        private string LayoutsInSchedule()
        {
            string layoutsInSchedule = "";

            foreach (LayoutSchedule layoutSchedule in CurrentSchedule)
            {
                layoutsInSchedule += "LayoutId: " + layoutSchedule.id + ". Runs from " + layoutSchedule.FromDt.ToString() + Environment.NewLine;
            }

            return layoutsInSchedule;
        }

        #endregion
    }

    /// <summary>
    /// A LayoutSchedule
    /// </summary>
    [Serializable]
    public struct LayoutSchedule
    {
        public string NodeName;
        public string layoutFile;
        public int id;
        public int scheduleid;

        public bool Priority;

        public DateTime FromDt;
        public DateTime ToDt;
    }
}
