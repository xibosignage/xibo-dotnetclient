/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2016 Daniel Garner
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
using XiboClient.Logic;
using System.Globalization;
using XiboClient.Action;

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

        // Event for Subscriber inactive
        public delegate void OnScheduleManagerCheckCompleteDelegate();
        public event OnScheduleManagerCheckCompleteDelegate OnScheduleManagerCheckComplete;

        // Member Varialbes
        private string _location;
        private Collection<LayoutChangePlayerAction> _layoutChangeActions;
        private Collection<OverlayLayoutPlayerAction> _overlayLayoutActions;
        private Collection<ScheduleItem> _layoutSchedule;
        private Collection<ScheduleItem> _currentSchedule;
        private Collection<ScheduleCommand> _commands;
        private Collection<ScheduleItem> _overlaySchedule;
        private Collection<ScheduleItem> _currentOverlaySchedule;

        private bool _refreshSchedule;
        private CacheManager _cacheManager;
        private DateTime _lastScreenShotDate;
        
        /// <summary>
        /// The currently playing layout Id
        /// </summary>
        private int _currenctLayoutId;

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
            _layoutSchedule = new Collection<ScheduleItem>();
            _currentSchedule = new Collection<ScheduleItem>();
            _layoutChangeActions = new Collection<LayoutChangePlayerAction>();
            _commands = new Collection<ScheduleCommand>();

            // Overlay schedules
            _currentOverlaySchedule = new Collection<ScheduleItem>();
            _overlaySchedule = new Collection<ScheduleItem>();
            _overlayLayoutActions = new Collection<OverlayLayoutPlayerAction>();

            _lastScreenShotDate = DateTime.MinValue;
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
        public Collection<ScheduleItem> CurrentSchedule
        {
            get
            {
                return _currentSchedule;
            }
        }

        /// <summary>
        /// Get the current overlay schedule
        /// </summary>
        public Collection<ScheduleItem> CurrentOverlaySchedule
        {
            get
            {
                return _currentOverlaySchedule;
            }
        }
        
        /// <summary>
        /// Get or Set the current layout id
        /// </summary>
        public int CurrentLayoutId
        {
            get
            {
                return _currenctLayoutId;
            }
            set
            {
                lock (_locker)
                    _currenctLayoutId = value;
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

                        // Do we need to take a screenshot?
                        if (ApplicationSettings.Default.ScreenShotRequestInterval > 0 && DateTime.Now > _lastScreenShotDate.AddMinutes(ApplicationSettings.Default.ScreenShotRequestInterval))
                        {
                            // Take a screen shot and send it
                            ScreenShot.TakeAndSend();

                            // Store the date
                            _lastScreenShotDate = DateTime.Now;

                            // Notify status to XMDS
                            _clientInfoForm.notifyStatusToXmds();
                        }

                        // Run any commands that occur in the next 10 seconds.
                        DateTime now = DateTime.Now;
                        DateTime tenSecondsTime = now.AddSeconds(10);

                        foreach (ScheduleCommand command in _commands)
                        {
                            if (command.Date >= now && command.Date < tenSecondsTime && !command.HasRun)
                            {
                                try
                                {
                                    // We need to run this command
                                    new Thread(new ThreadStart(command.Run)).Start();

                                    // Mark run
                                    command.HasRun = true;
                                }
                                catch (Exception e)
                                {
                                    Trace.WriteLine(new LogMessage("ScheduleManager - Run", "Cannot start Thread to Run Command: " + e.Message), LogType.Error.ToString());
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("ScheduleManager - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                        _clientInfoForm.ScheduleStatus = "Error. " + ex.Message;
                    }
                }

                // Completed this check
                if (OnScheduleManagerCheckComplete != null)
                    OnScheduleManagerCheckComplete();

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
            // Remove completed change actions
            removeLayoutChangeActionIfComplete();

            // Remove completed overlay actions
            removeOverlayLayoutActionIfComplete();

            // If we dont currently have a cached schedule load one from the scheduleLocation
            // also do this if we have been told to Refresh the schedule
            if (_layoutSchedule.Count == 0 || RefreshSchedule)
            {
                // Try to load the schedule from disk
                try
                {
                    // Empty the current schedule collection
                    _layoutSchedule.Clear();

                    // Clear the list of commands
                    _commands.Clear();

                    // Clear the list of overlays
                    _overlaySchedule.Clear();

                    // Load in the schedule
                    LoadScheduleFromFile();

                    // Load in the layout change actions
                    LoadScheduleFromLayoutChangeActions();

                    // Load in the overlay actions
                    LoadScheduleFromOverlayLayoutActions();
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
            Collection<ScheduleItem> newSchedule = LoadNewSchedule();

            // Load a new overlay schedule
            Collection<ScheduleItem> overlaySchedule = LoadNewOverlaySchedule();

            bool forceChange = false;

            // If the current schedule is empty, always overwrite
            if (_currentSchedule.Count == 0)
                forceChange = true;

            // Log
            List<string> currentScheduleString = new List<string>();
            List<string> newScheduleString = new List<string>();
            List<string> newOverlaysString = new List<string>();

            // Are all the items that were in the _currentSchedule still there?
            foreach (ScheduleItem layout in _currentSchedule)
            {
                if (!newSchedule.Contains(layout))
                {
                    Trace.WriteLine(new LogMessage("ScheduleManager - IsNewScheduleAvailable", "New Schedule does not contain " + layout.id), LogType.Audit.ToString());
                    forceChange = true;
                }
                currentScheduleString.Add(layout.ToString());
            }
            
            foreach (ScheduleItem layout in newSchedule)
            {
                newScheduleString.Add(layout.ToString());
            }

            Trace.WriteLine(new LogMessage("ScheduleManager - IsNewScheduleAvailable", "Layouts in Current Schedule: " + string.Join(Environment.NewLine, currentScheduleString)), LogType.Audit.ToString());
            Trace.WriteLine(new LogMessage("ScheduleManager - IsNewScheduleAvailable", "Layouts in New Schedule: " + string.Join(Environment.NewLine, newScheduleString)), LogType.Audit.ToString());

            // Old layout overlays
            foreach (ScheduleItem layout in overlaySchedule)
            {
                newOverlaysString.Add(layout.ToString());
            }

            // Try to work out whether the overlay schedule has changed or not.
            // easiest way to do this is to see if the sizes have changed
            if (_currentOverlaySchedule.Count != overlaySchedule.Count)
            {
                forceChange = true;
            }
            else
            {
                // Compare them on an object by object level.
                // Are all the items that were in the _currentOverlaySchedule still there?
                foreach (ScheduleItem layout in _currentOverlaySchedule)
                {
                    // New overlay schedule doesn't contain the layout?
                    if (!overlaySchedule.Contains(layout))
                        forceChange = true;
                }
            }

            Trace.WriteLine(new LogMessage("ScheduleManager - IsNewScheduleAvailable", "Overlay Layouts: " + string.Join(Environment.NewLine, newOverlaysString)), LogType.Audit.ToString());

            // Set the new schedule
            _currentSchedule = newSchedule;

            // Set the new Overlay schedule
            _currentOverlaySchedule = overlaySchedule;

            // Clear up
            newSchedule = null;
            overlaySchedule = null;

            // Return True if we want to refresh the schedule OR false if we are OK to leave the current one.
            // We can update the current schedule and still return false - this will not trigger a schedule change event.
            // We do this if ALL the current layouts are still in the schedule
            return forceChange;
        }

        /// <summary>
        /// Loads a new schedule from _layoutSchedules
        /// </summary>
        /// <returns></returns>
        private Collection<ScheduleItem> LoadNewSchedule()
        {
            // We need to build the current schedule from the layout schedule (obeying date/time)
            Collection<ScheduleItem> newSchedule = new Collection<ScheduleItem>();
            Collection<ScheduleItem> prioritySchedule = new Collection<ScheduleItem>();
            Collection<ScheduleItem> layoutChangeSchedule = new Collection<ScheduleItem>();
            
            // Temporary default Layout incase we have no layout nodes.
            ScheduleItem defaultLayout = new ScheduleItem();

            // Store the valid layout id's
            List<int> validLayoutIds = new List<int>();
            List<int> invalidLayouts = new List<int>();

            // Store the highest priority
            int highestPriority = 0;

            // For each layout in the schedule determine if it is currently inside the _currentSchedule, and whether it should be
            foreach (ScheduleItem layout in _layoutSchedule)
            {
                // Is this already invalid
                if (invalidLayouts.Contains(layout.id))
                    continue;

                // If we haven't already assessed this layout before, then check that it is valid
                if (!validLayoutIds.Contains(layout.id))
                {
                if (!ApplicationSettings.Default.ExpireModifiedLayouts && layout.id == CurrentLayoutId)
                {
                    Trace.WriteLine(new LogMessage("ScheduleManager - LoadNewSchedule", "Skipping validity test for current layout."), LogType.Audit.ToString());
                }
                else
                {
                    // Is the layout valid in the cachemanager?
                    try
                    {
                        if (!_cacheManager.IsValidPath(layout.id + ".xlf"))
                        {
                            invalidLayouts.Add(layout.id);
                            Trace.WriteLine(new LogMessage("ScheduleManager - LoadNewSchedule", "Layout invalid: " + layout.id), LogType.Info.ToString());
                            continue;
                        }
                    }
                    catch
                    {
                        // Ignore this layout.. raise an error?
                        invalidLayouts.Add(layout.id);
                        Trace.WriteLine(new LogMessage("ScheduleManager - LoadNewSchedule", "Unable to determine if layout is valid or not"), LogType.Error.ToString());
                        continue;
                    }

                    // Check dependents
                    bool validDependents = true;
                    foreach (string dependent in layout.Dependents)
                    {
                        if (!string.IsNullOrEmpty(dependent) && !_cacheManager.IsValidPath(dependent))
                        {
                            invalidLayouts.Add(layout.id);
                            Trace.WriteLine(new LogMessage("ScheduleManager - LoadNewSchedule", "Layout has invalid dependent: " + dependent), LogType.Info.ToString());

                            validDependents = false;
                            break;
                        }
                    }

                    if (!validDependents)
                        continue;
                    }
                }

                // Add to the valid layout ids
                validLayoutIds.Add(layout.id);

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
                    // Change Action and Priority layouts should generate their own list
                    if (layout.Override)
                    {
                        layoutChangeSchedule.Add(layout);
                    }
                    else if (layout.Priority >= 1)
                    {
                        // Is this higher than our priority already?
                        if (layout.Priority > highestPriority)
                        {
                            prioritySchedule.Clear();
                            prioritySchedule.Add(layout);

                            // Store the new highest priority
                            highestPriority = layout.Priority;
                        }
                        else if (layout.Priority == highestPriority)
                        {
                            prioritySchedule.Add(layout);
                        }
                        // Layouts with a priority lower than the current highest are discarded.
                    }
                    else
                    {
                        newSchedule.Add(layout);
                    }
                }
            }

            // If we have any layout change scheduled then we return those instead
            if (layoutChangeSchedule.Count > 0)
                return layoutChangeSchedule;

            // If we have any priority schedules then we need to return those instead
            if (prioritySchedule.Count > 0)
                return prioritySchedule;

            // If the current schedule is empty by the end of all this, then slip the default in
            if (newSchedule.Count == 0)
                newSchedule.Add(defaultLayout);

            return newSchedule;
        }

        /// <summary>
        /// Loads a new schedule from _overlaySchedules
        /// </summary>
        /// <returns></returns>
        private Collection<ScheduleItem> LoadNewOverlaySchedule()
        {
            // We need to build the current schedule from the layout schedule (obeying date/time)
            Collection<ScheduleItem> newSchedule = new Collection<ScheduleItem>();
            Collection<ScheduleItem> prioritySchedule = new Collection<ScheduleItem>();
            Collection<ScheduleItem> overlayActionSchedule = new Collection<ScheduleItem>();
            
            // Store the valid layout id's
            List<int> validLayoutIds = new List<int>();
            List<int> invalidLayouts = new List<int>();

            // Store the highest priority
            int highestPriority = 1;

            // For each layout in the schedule determine if it is currently inside the _currentSchedule, and whether it should be
            foreach (ScheduleItem layout in _overlaySchedule)
            {
                // Is this already invalid
                if (invalidLayouts.Contains(layout.id))
                    continue;

                // If we haven't already assessed this layout before, then check that it is valid
                if (!validLayoutIds.Contains(layout.id))
                {
                    // Is the layout valid in the cachemanager?
                    try
                    {
                        if (!_cacheManager.IsValidPath(layout.id + ".xlf"))
                        {
                            invalidLayouts.Add(layout.id);
                            Trace.WriteLine(new LogMessage("ScheduleManager - LoadNewOverlaySchedule", "Layout invalid: " + layout.id), LogType.Info.ToString());
                            continue;
                        }
                    }
                    catch
                    {
                        // Ignore this layout.. raise an error?
                        invalidLayouts.Add(layout.id);
                        Trace.WriteLine(new LogMessage("ScheduleManager - LoadNewOverlaySchedule", "Unable to determine if layout is valid or not"), LogType.Error.ToString());
                        continue;
                    }

                    // Check dependents
                    foreach (string dependent in layout.Dependents)
                    {
                        if (!_cacheManager.IsValidPath(dependent))
                        {
                            invalidLayouts.Add(layout.id);
                            Trace.WriteLine(new LogMessage("ScheduleManager - LoadNewOverlaySchedule", "Layout has invalid dependent: " + dependent), LogType.Info.ToString());
                            continue;
                        }
                    }
                }

                // Add to the valid layout ids
                validLayoutIds.Add(layout.id);

                // Look at the Date/Time to see if it should be on the schedule or not
                if (layout.FromDt <= DateTime.Now && layout.ToDt >= DateTime.Now)
                {
                    // Change Action and Priority layouts should generate their own list
                    if (layout.Override)
                    {
                        overlayActionSchedule.Add(layout);
                    }
                    else if (layout.Priority >= 1)
                    {
                        // Is this higher than our priority already?
                        if (layout.Priority > highestPriority)
                        {
                            prioritySchedule.Clear();
                            prioritySchedule.Add(layout);

                            // Store the new highest priority
                            highestPriority = layout.Priority;
                        }
                        else if (layout.Priority == highestPriority)
                        {
                            prioritySchedule.Add(layout);
                        }
                    }
                    else
                    {
                        newSchedule.Add(layout);
                    }
                }
            }

            // Have we got any overlay actions
            if (overlayActionSchedule.Count > 0)
                return overlayActionSchedule;

            // If we have any priority schedules then we need to return those instead
            if (prioritySchedule.Count > 0)
                return prioritySchedule;

            return newSchedule;
        }

        /// <summary>
        /// Loads the schedule from file.
        /// </summary>
        /// <returns></returns>
        private void LoadScheduleFromFile()
        {
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
                // Node name
                if (node.Name == "dependants")
                {
                    // Do nothing for now
                }
                else if (node.Name == "command")
                {
                    // Try to get the command using the code
                    try
                    {
                        // Pull attributes from layout nodes
                        XmlAttributeCollection attributes = node.Attributes;

                        ScheduleCommand command = new ScheduleCommand();
                        command.Date = DateTime.Parse(attributes["date"].Value, CultureInfo.InvariantCulture);
                        command.Code = attributes["code"].Value;
                        command.ScheduleId = int.Parse(attributes["scheduleid"].Value);

                        // Add to the collection
                        _commands.Add(command);
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(new LogMessage("ScheduleManager - LoadScheduleFromFile", e.Message), LogType.Error.ToString());
                    }
                }
                else if (node.Name == "overlays")
                {
                    // Parse out overlays and load them into their own schedule
                    foreach (XmlNode overlayNode in node.ChildNodes)
                    {
                        _overlaySchedule.Add(ParseNodeIntoScheduleItem(overlayNode));
                    }
                }
                else
                {
                    _layoutSchedule.Add(ParseNodeIntoScheduleItem(node));
                }
            }

            // Clean up
            nodes = null;
            scheduleXml = null;

            // We now have the saved XML contained in the _layoutSchedule object
        }

        /// <summary>
        /// Parse an XML node from XMDS into a Schedule Item
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private ScheduleItem ParseNodeIntoScheduleItem(XmlNode node)
        {
            ScheduleItem temp = new ScheduleItem();
            temp.NodeName = node.Name;

            // Pull attributes from layout nodes
            XmlAttributeCollection attributes = node.Attributes;

            // All nodes have file properties
            temp.layoutFile = attributes["file"].Value;

            // Replace the .xml extension with nothing
            string replace = ".xml";
            string layoutFile = temp.layoutFile.TrimEnd(replace.ToCharArray());

            // Set these on the temp layoutschedule
            temp.layoutFile = ApplicationSettings.Default.LibraryPath + @"\" + layoutFile + @".xlf";
            temp.id = int.Parse(layoutFile);

                    // Dependents
                    if (attributes["dependents"] != null && !string.IsNullOrEmpty(attributes["dependents"].Value))
                    {
                        foreach (string dependent in attributes["dependents"].Value.Split(','))
                        {
                            temp.Dependents.Add(dependent);
                        }
                    }

            // Get attributes that only exist on the default
            if (temp.NodeName != "default")
            {
                // Priority flag
                try
                {
                    temp.Priority = int.Parse(attributes["priority"].Value);
                }
                catch
                {
                    temp.Priority = 0;
                }

                // Get the fromdt,todt
                temp.FromDt = DateTime.Parse(attributes["fromdt"].Value, CultureInfo.InvariantCulture);
                temp.ToDt = DateTime.Parse(attributes["todt"].Value, CultureInfo.InvariantCulture);

                // Pull out the scheduleid if there is one
                string scheduleId = "";
                if (attributes["scheduleid"] != null) scheduleId = attributes["scheduleid"].Value;

                // Add it to the layout schedule
                if (scheduleId != "") temp.scheduleid = int.Parse(scheduleId);
                // Dependents
                if (attributes["dependents"] != null)
                {
                    foreach (string dependent in attributes["dependents"].Value.Split(','))
                    {
                        temp.Dependents.Add(dependent);
                    }
                }
            }

            // Look for dependents nodes
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.Name == "dependents")
                {
                    foreach (XmlNode dependent in childNode.ChildNodes)
                    {
                        if (dependent.Name == "file")
                        {
                            temp.Dependents.Add(dependent.InnerText);
                        }
                    }
                }
            }

            return temp;
        }

        /// <summary>
        /// Load schedule from layout change actions
        /// </summary>
        private void LoadScheduleFromLayoutChangeActions()
        {
            if (_layoutChangeActions.Count <= 0)
                return;

            // Loop through the layout change actions and create schedule items for them
            foreach (LayoutChangePlayerAction action in _layoutChangeActions)
            {
                if (action.downloadRequired)
                    continue;

                DateTime actionCreateDt = DateTime.Parse(action.createdDt);
                
                ScheduleItem item = new ScheduleItem();
                item.FromDt = actionCreateDt.AddSeconds(-1);
                item.ToDt = DateTime.MaxValue;
                item.id = action.layoutId;
                item.scheduleid = 0;
                item.actionId = action.GetId();
                item.Priority = 0;
                item.Override = true;
                item.NodeName = "layout";
                item.layoutFile = ApplicationSettings.Default.LibraryPath + @"\" + item.id + @".xlf";

                _layoutSchedule.Add(item);
            }
        }

        /// <summary>
        /// Load schedule from layout change actions
        /// </summary>
        private void LoadScheduleFromOverlayLayoutActions()
        {
            if (_overlayLayoutActions.Count <= 0)
                return;

            // Loop through the layout change actions and create schedule items for them
            foreach (OverlayLayoutPlayerAction action in _overlayLayoutActions)
            {
                removeOverlayLayoutActionIfComplete();

                if (action.downloadRequired)
                    continue;

                ScheduleItem item = new ScheduleItem();
                item.FromDt = DateTime.MinValue;
                item.ToDt = DateTime.MaxValue;
                item.id = action.layoutId;
                item.scheduleid = action.layoutId;
                item.actionId = action.GetId();
                item.Priority = 0;
                item.Override = true;
                item.NodeName = "layout";
                item.layoutFile = ApplicationSettings.Default.LibraryPath + @"\" + item.id + @".xlf";

                _overlaySchedule.Add(item);
            }
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
            ScheduleItem temp = new ScheduleItem();
            temp.layoutFile = ApplicationSettings.Default.LibraryPath + @"\Default.xml";
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
                    using (FileStream fileStream = File.Open(scheduleLocation, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                    {
                        using (StreamReader sr = new StreamReader(fileStream))
                        {
                            scheduleXml = sr.ReadToEnd();
                        }
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

            foreach (ScheduleItem layoutSchedule in CurrentSchedule)
            {
                if (layoutSchedule.Override)
                    layoutsInSchedule += "API Action ";

                layoutsInSchedule += "LayoutId: " + layoutSchedule.id + ". Runs from " + layoutSchedule.FromDt.ToString() + Environment.NewLine;
            }

            foreach (ScheduleItem layoutSchedule in CurrentOverlaySchedule)
            {
                layoutsInSchedule += "Overlay LayoutId: " + layoutSchedule.id + ". Runs from " + layoutSchedule.FromDt.ToString() + Environment.NewLine;
            }

            return layoutsInSchedule;
        }

        /// <summary>
        /// Add a layout change action
        /// </summary>
        /// <param name="action"></param>
        public void AddLayoutChangeAction(LayoutChangePlayerAction action)
        {
            _layoutChangeActions.Add(action);
            RefreshSchedule = true;
        }

        /// <summary>
        /// Replace Layout Change Action
        /// </summary>
        /// <param name="action"></param>
        public void ReplaceLayoutChangeActions(LayoutChangePlayerAction action)
        {
            ClearLayoutChangeActions();
            AddLayoutChangeAction(action);
        }

        /// <summary>
        /// Clear Layout Change Actions
        /// </summary>
        public void ClearLayoutChangeActions()
        {
            _layoutChangeActions.Clear();
            RefreshSchedule = true;
        }

        /// <summary>
        /// Assess and Remove the Layout Change Action if completed
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool removeLayoutChangeActionIfComplete(ScheduleItem item)
        {
            // Check each Layout Change Action we own and compare to the current item
            foreach (LayoutChangePlayerAction action in _layoutChangeActions)
            {
                if (item.id == action.layoutId && item.actionId == action.GetId())
                {
                    // we've played
                    action.SetPlayed();

                    // Does this conclude this change action?
                    if (action.IsServiced())
                    {
                        _layoutChangeActions.Remove(action);
                        RefreshSchedule = true;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Remove Layout Change actions if they have completed
        /// </summary>
        public void removeLayoutChangeActionIfComplete()
        {
            // Check every action to see if complete
            foreach (LayoutChangePlayerAction action in _layoutChangeActions)
            {
                if (action.IsServiced())
                {
                    _layoutChangeActions.Remove(action);
                    RefreshSchedule = true;
                }
            }
        }

        /// <summary>
        /// Add an overlay layout action
        /// </summary>
        /// <param name="action"></param>
        public void AddOverlayLayoutAction(OverlayLayoutPlayerAction action)
        {
            _overlayLayoutActions.Add(action);
            RefreshSchedule = true;
        }

        /// <summary>
        /// Remove Overlay Layout Actions if they are complete
        /// </summary>
        /// <param name="item"></param>
        public void removeOverlayLayoutActionIfComplete()
        {
            // Check each Layout Change Action we own and compare to the current item
            foreach (OverlayLayoutPlayerAction action in _overlayLayoutActions)
            {
                if (action.IsServiced())
                {
                    _overlayLayoutActions.Remove(action);
                    RefreshSchedule = true;
                }
            }
        }

        /// <summary>
        /// Set all Layout Change Actions to be downloaded
        /// </summary>
        public void setAllActionsDownloaded()
        {
            foreach (LayoutChangePlayerAction action in _layoutChangeActions)
            {
                if (action.downloadRequired)
                {
                    action.downloadRequired = false;
                    RefreshSchedule = true;
                }
            }

            foreach (OverlayLayoutPlayerAction action in _overlayLayoutActions)
            {
                if (action.downloadRequired)
                {
                    action.downloadRequired = false;
                    RefreshSchedule = true;
                }
            }
        }

        #endregion
    }
}
