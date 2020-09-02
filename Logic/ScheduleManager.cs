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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using XiboClient.Action;
using XiboClient.Log;
using XiboClient.Logic;

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
        private List<LayoutChangePlayerAction> _layoutChangeActions;
        private List<OverlayLayoutPlayerAction> _overlayLayoutActions;
        private List<ScheduleItem> _layoutSchedule;
        private List<ScheduleCommand> _commands;
        private List<ScheduleItem> _overlaySchedule;
        private InterruptState _interruptState;

        public delegate void OnInterruptNowDelegate();
        public event OnInterruptNowDelegate OnInterruptNow;

        public delegate void OnInterruptPausePendingDelegate();
        public event OnInterruptPausePendingDelegate OnInterruptPausePending;

        public delegate void OnInterruptEndDelegate();
        public event OnInterruptEndDelegate OnInterruptEnd;

        // State
        private bool _refreshSchedule;
        private DateTime _lastScreenShotDate;

        /// <summary>
        /// The currently playing layout Id
        /// </summary>
        private int _currenctLayoutId;

        /// <summary>
        /// Creates a new schedule Manager
        /// </summary>
        /// <param name="scheduleLocation"></param>
        public ScheduleManager(string scheduleLocation)
        {
            _location = scheduleLocation;

            // Create an empty layout schedule
            _layoutSchedule = new List<ScheduleItem>();
            CurrentSchedule = new List<ScheduleItem>();
            _layoutChangeActions = new List<LayoutChangePlayerAction>();
            _commands = new List<ScheduleCommand>();

            // Overlay schedules
            CurrentOverlaySchedule = new List<ScheduleItem>();
            _overlaySchedule = new List<ScheduleItem>();
            _overlayLayoutActions = new List<OverlayLayoutPlayerAction>();

            // Interrupts
            CurrentInterruptSchedule = new List<ScheduleItem>();

            // Screenshot
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
        public List<ScheduleItem> CurrentSchedule { get; private set; }

        /// <summary>
        /// Get the current overlay schedule
        /// </summary>
        public List<ScheduleItem> CurrentOverlaySchedule { get; private set; }


        /// <summary>
        /// Get the current interrupt schedule
        /// </summary>
        public List<ScheduleItem> CurrentInterruptSchedule { get; private set; }

        #endregion

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

            // Create a GeoCoordinateWatcher
            GeoCoordinateWatcher watcher = null;
            try
            {
                watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.High)
                {
                    MovementThreshold = 10
                };
                watcher.PositionChanged += Watcher_PositionChanged;
                watcher.StatusChanged += Watcher_StatusChanged;
                watcher.Start();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("ScheduleManager", "Run: GeoCoordinateWatcher failed to start. E = " + e.Message), LogType.Error.ToString());
            }

            // Load the interrupt state
            InterruptInitState();

            // Run loop
            // --------
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
                        bool isNewScheduleAvailable = IsNewScheduleAvailable();

                        // Interrupts
                        // ----------
                        // Handle interrupts to keep the list in order and fresh
                        // this effectively sets the order of our interrupt layouts before they get updated on the main
                        // thread.
                        try
                        {
                            InterruptAssessAndUpdate();
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine(new LogMessage("ScheduleManager", "Run: Problem assessing interrupt schedule. E = " + e.Message), LogType.Error.ToString());
                        }

                        // Events
                        // ------
                        if (isNewScheduleAvailable)
                        {
                            OnNewScheduleAvailable();
                        }
                        else
                        {
                            OnRefreshSchedule();
                        }

                        // Update the client info form
                        ClientInfo.Instance.ScheduleManagerStatus = LayoutsInSchedule();

                        // Do we need to take a screenshot?
                        if (ApplicationSettings.Default.ScreenShotRequestInterval > 0 && DateTime.Now > _lastScreenShotDate.AddMinutes(ApplicationSettings.Default.ScreenShotRequestInterval))
                        {
                            // Take a screen shot and send it
                            ScreenShot.TakeAndSend();

                            // Store the date
                            _lastScreenShotDate = DateTime.Now;

                            // Notify status to XMDS
                            ClientInfo.Instance.NotifyStatusToXmds();
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
                        ClientInfo.Instance.ScheduleStatus = "Error. " + ex.Message;
                    }
                }

                // Completed this check
                OnScheduleManagerCheckComplete?.Invoke();

                // Sleep this thread for 10 seconds
                _manualReset.WaitOne(10 * 1000);
            }

            // Stop the watcher
            if (watcher != null)
            {
                watcher.PositionChanged -= Watcher_PositionChanged;
                watcher.StatusChanged -= Watcher_StatusChanged;
                watcher.Stop();
                watcher.Dispose();
            }

            // Save the interrupt state
            InterruptPersistState();

            Trace.WriteLine(new LogMessage("ScheduleManager - Run", "Thread Stopped"), LogType.Info.ToString());
        }

        #region Methods

        /// <summary>
        /// Watcher status changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Watcher_StatusChanged(object sender, GeoPositionStatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case GeoPositionStatus.Initializing:
                    Trace.WriteLine(new LogMessage("ScheduleManager", "Watcher_StatusChanged: Working on location fix"), LogType.Info.ToString());
                    break;

                case GeoPositionStatus.Ready:
                    Trace.WriteLine(new LogMessage("ScheduleManager", "Watcher_StatusChanged: Have location"), LogType.Info.ToString());
                    break;

                case GeoPositionStatus.NoData:
                    Trace.WriteLine(new LogMessage("ScheduleManager", "Watcher_StatusChanged: No data"), LogType.Info.ToString());
                    break;

                case GeoPositionStatus.Disabled:
                    Trace.WriteLine(new LogMessage("ScheduleManager", "Watcher_StatusChanged: Disabled"), LogType.Info.ToString());
                    // Restart
                    try
                    {
                        ((GeoCoordinateWatcher)sender).Start();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(new LogMessage("ScheduleManager", "Watcher_StatusChanged: Disabled and can't restart, e = " + ex.Message), LogType.Error.ToString());
                    }
                    break;
            }
        }

        /// <summary>
        /// Watcher position has changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Watcher_PositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            GeoCoordinate coordinate = e.Position.Location;

            if (coordinate.IsUnknown)
            {
                Trace.WriteLine(new LogMessage("ScheduleManager", "Watcher_PositionChanged: Position Unknown"), LogType.Audit.ToString());
            }
            else
            {
                // Is this more or less accurate than the one we have already?
                Trace.WriteLine(new LogMessage("ScheduleManager", "Watcher_PositionChanged: H.Accuracy = " + coordinate.HorizontalAccuracy
                    + ", V.Accuracy = " + coordinate.VerticalAccuracy
                    + ". Lat = " + coordinate.Latitude
                    + ", Long = " + coordinate.Longitude
                    + ", Course = " + coordinate.Course
                    + ", Altitude = " + coordinate.Altitude
                    + ", Speed = " + coordinate.Speed), LogType.Info.ToString());

                // Has it changed?
                if (ClientInfo.Instance.CurrentGeoLocation == null
                    || ClientInfo.Instance.CurrentGeoLocation.IsUnknown
                    || coordinate.Latitude != ClientInfo.Instance.CurrentGeoLocation.Latitude
                    || coordinate.Longitude != ClientInfo.Instance.CurrentGeoLocation.Longitude)
                {
                    // Have we moved more that 100 meters?
                    double distanceTo = 1000;
                    if (ClientInfo.Instance.CurrentGeoLocation != null && !ClientInfo.Instance.CurrentGeoLocation.IsUnknown)
                    {
                        // Grab the distance from original position
                        distanceTo = coordinate.GetDistanceTo(ClientInfo.Instance.CurrentGeoLocation);
                    }                    

                    // Take the new one.
                    ClientInfo.Instance.CurrentGeoLocation = coordinate;

                    // Wake up the schedule manager for another pass
                    if (distanceTo >= 100)
                    {
                        RefreshSchedule = true;
                    }
                }
            }
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
            List<ScheduleItem> newSchedule = LoadNewSchedule();

            // Load a new overlay schedule
            List<ScheduleItem> overlaySchedule = LoadNewOverlaySchedule();

            // Load a new interrupt schedule
            List<ScheduleItem> newInterruptSchedule = LoadNewSchedule(true);

            // Should we force a change 
            // (broadly this depends on whether or not the schedule has changed.)
            bool forceChange = false;

            // If the current schedule is empty, always overwrite
            if (CurrentSchedule.Count == 0)
            {
                forceChange = true;
            }

            // Log
            List<string> currentScheduleString = new List<string>();
            List<string> newScheduleString = new List<string>();
            List<string> newOverlaysString = new List<string>();
            List<string> newInterruptString = new List<string>();

            // Are all the items that were in the _currentSchedule still there?
            foreach (ScheduleItem layout in CurrentSchedule)
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

            // Overlays
            // --------
            // Logging first
            foreach (ScheduleItem layout in overlaySchedule)
            {
                newOverlaysString.Add(layout.ToString());
            }

            Trace.WriteLine(new LogMessage("ScheduleManager - IsNewScheduleAvailable", "Overlay Layouts: " + string.Join(Environment.NewLine, newOverlaysString)), LogType.Audit.ToString());

            // Try to work out whether the overlay schedule has changed or not.
            // easiest way to do this is to see if the sizes have changed
            if (CurrentOverlaySchedule.Count != overlaySchedule.Count)
            {
                forceChange = true;
            }
            else
            {
                // Compare them on an object by object level.
                // Are all the items that were in the _currentOverlaySchedule still there?
                foreach (ScheduleItem layout in CurrentOverlaySchedule)
                {
                    // New overlay schedule doesn't contain the layout?
                    if (!overlaySchedule.Contains(layout))
                        forceChange = true;
                }
            }

            // Interrupts
            // ----------
            // We don't want a change in interrupt schedule to forceChange, because we don't want to impact the usual running schedule.
            // But we do want to know if its happened
            foreach (ScheduleItem layout in CurrentInterruptSchedule)
            {
                if (!newInterruptSchedule.Contains(layout))
                {
                    this._interruptState.LastInterruptScheduleChange = DateTime.Now;
                    Trace.WriteLine(new LogMessage("ScheduleManager - IsNewScheduleAvailable", "Interrupt Schedule Change"), LogType.Audit.ToString());
                }
            }

            // Logging
            foreach (ScheduleItem layout in newInterruptSchedule)
            {
                newInterruptString.Add(layout.ToString());
            }

            Trace.WriteLine(new LogMessage("ScheduleManager - IsNewScheduleAvailable", "Interrupt Layouts: " + string.Join(Environment.NewLine, newInterruptString)), LogType.Audit.ToString());

            // Finalise
            // --------
            // Set the new schedule
            CurrentSchedule = newSchedule;

            // Set the new Overlay schedule
            CurrentOverlaySchedule = overlaySchedule;

            // Set the new interrupt schedule
            this.CurrentInterruptSchedule = newInterruptSchedule;

            // Clear up
            newSchedule = null;
            overlaySchedule = null;
            newInterruptSchedule = null;

            // Return True if we want to refresh the schedule OR false if we are OK to leave the current one.
            // We can update the current schedule and still return false - this will not trigger a schedule change event.
            // We do this if ALL the current layouts are still in the schedule
            return forceChange;
        }

        /// <summary>
        /// Loads a new schedule from _layoutSchedules
        /// </summary>
        /// <returns></returns>
        private List<ScheduleItem> LoadNewSchedule()
        {
            return LoadNewSchedule(false);
        }

        /// <summary>
        /// Loads a new schedule from _layoutSchedules
        /// </summary>
        /// <param name="isForInterrupt">Is this schedule for interrupt or normal</param>
        /// <returns></returns>
        private List<ScheduleItem> LoadNewSchedule(bool isForInterrupt)
        {
            // We need to build the current schedule from the layout schedule (obeying date/time)
            List<ScheduleItem> newSchedule = new List<ScheduleItem>();
            List<ScheduleItem> prioritySchedule = new List<ScheduleItem>();
            List<ScheduleItem> layoutChangeSchedule = new List<ScheduleItem>();

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
                // Pick only the ones we're interested in
                if ((isForInterrupt && !layout.IsInterrupt())
                    || (!isForInterrupt && layout.IsInterrupt()))
                {
                    // Skip
                    continue;
                }

                // Is this already invalid
                if (invalidLayouts.Contains(layout.id))
                    continue;

                // If we haven't already assessed this layout before, then check that it is valid
                if (!validLayoutIds.Contains(layout.id))
                {
                    if (!ApplicationSettings.Default.ExpireModifiedLayouts && layout.id == ClientInfo.Instance.CurrentLayoutId)
                    {
                        Trace.WriteLine(new LogMessage("ScheduleManager - LoadNewSchedule", "Skipping validity test for current layout."), LogType.Audit.ToString());
                    }
                    else
                    {
                        // Is the layout valid in the cachemanager?
                        try
                        {
                            if (!CacheManager.Instance.IsValidPath(layout.id + ".xlf"))
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
                            if (!string.IsNullOrEmpty(dependent) && !CacheManager.Instance.IsValidPath(dependent))
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
                    // Is it GeoAware?
                    if (layout.IsGeoAware)
                    {
                        // Check that it is inside the current location.
                        if (!layout.SetIsGeoActive(ClientInfo.Instance.CurrentGeoLocation))
                        {
                            continue;
                        }
                    }

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
            if (newSchedule.Count == 0 && !isForInterrupt)
                newSchedule.Add(defaultLayout);

            return newSchedule;
        }

        /// <summary>
        /// Loads a new schedule from _overlaySchedules
        /// </summary>
        /// <returns></returns>
        private List<ScheduleItem> LoadNewOverlaySchedule()
        {
            // We need to build the current schedule from the layout schedule (obeying date/time)
            List<ScheduleItem> newSchedule = new List<ScheduleItem>();
            List<ScheduleItem> prioritySchedule = new List<ScheduleItem>();
            List<ScheduleItem> overlayActionSchedule = new List<ScheduleItem>();

            // Store the valid layout id's
            List<int> validLayoutIds = new List<int>();
            List<int> invalidLayouts = new List<int>();

            // Store the highest priority
            int highestPriority = 1;

            // For each layout in the schedule determine if it is currently inside the _currentSchedule, and whether it should be
            foreach (ScheduleItem layout in _overlaySchedule)
            {
                // Set to overlay
                layout.IsOverlay = true;

                // Is this already invalid
                if (invalidLayouts.Contains(layout.id))
                    continue;

                // If we haven't already assessed this layout before, then check that it is valid
                if (!validLayoutIds.Contains(layout.id))
                {
                    // Is the layout valid in the cachemanager?
                    try
                    {
                        if (!CacheManager.Instance.IsValidPath(layout.id + ".xlf"))
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
                        if (!CacheManager.Instance.IsValidPath(dependent))
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
                if (attributes["scheduleid"] != null)
                {
                    scheduleId = attributes["scheduleid"].Value;
                }

                // Add it to the layout schedule
                if (scheduleId != "")
                {
                    temp.scheduleid = int.Parse(scheduleId);
                }

                // Dependents
                if (attributes["dependents"] != null)
                {
                    foreach (string dependent in attributes["dependents"].Value.Split(','))
                    {
                        temp.Dependents.Add(dependent);
                    }
                }

                // Geo Schedule
                if (attributes["isGeoAware"] != null)
                {
                    temp.IsGeoAware = (attributes["isGeoAware"].Value == "1");
                    temp.GeoLocation = attributes["geoLocation"] != null ? attributes["geoLocation"].Value : "";
                }

                // Share of Voice
                if (attributes["shareOfVoice"] != null)
                {
                    try
                    {
                        temp.ShareOfVoice = int.Parse(attributes["shareOfVoice"].Value);
                    }
                    catch
                    {
                        temp.ShareOfVoice = 0;
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

            foreach (ScheduleItem layoutSchedule in CurrentInterruptSchedule)
            {
                layoutsInSchedule += "Interrupt LayoutId: " + layoutSchedule.id + ", shareOfVoice: " + layoutSchedule.ShareOfVoice + ". Runs from " + layoutSchedule.FromDt.ToString() + Environment.NewLine;
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

        #region Interrupt Management

        /// <summary>
        /// Update our schedule according to current standings
        /// </summary>
        private void InterruptAssessAndUpdate()
        {
            if (this.CurrentInterruptSchedule.Count <= 0)
            {
                // Drop all current hashes
                this._interruptState.InterruptTracking.Clear();

                // Fire an end event
                OnInterruptEnd?.Invoke();
            }
            else
            {
                // Recalculate the target hourly interruption
                int targetHourlyInterruption = 0;
                foreach (ScheduleItem item in CurrentInterruptSchedule)
                {
                    targetHourlyInterruption += item.ShareOfVoice;
                }

                // Did our schedule change last hour?
                if (this._interruptState.LastInterruptScheduleChange < TopOfHour())
                {
                    // Just take the new figure
                    this._interruptState.TargetHourlyInterruption = targetHourlyInterruption;
                }
                else
                {
                    this._interruptState.TargetHourlyInterruption = Math.Max(targetHourlyInterruption, this._interruptState.TargetHourlyInterruption);
                }

                // Order the schedule and determine if we need to interrupt
                InterruptResetSecondsIfNecessary();

                // How far through the hour are we?
                int secondsIntoHour = (int)(DateTime.Now - TopOfHour()).TotalSeconds;

                // Assess each Layout and update the item with current understanding of seconds played and rank
                foreach (ScheduleItem item in CurrentInterruptSchedule)
                {
                    try
                    {
                        if (this._interruptState.InterruptTracking.ContainsKey(item.scheduleid))
                        {
                            // Annotate this item with the existing seconds played
                            this._interruptState.InterruptTracking.TryGetValue(item.scheduleid, out double secondsPlayed);

                            item.SecondsPlayed = secondsPlayed;

                            // Is this item fulfilled
                            item.IsFulfilled = (item.SecondsPlayed >= item.ShareOfVoice);
                        }
                        else
                        {
                            item.SecondsPlayed = 0;
                        }

                        Debug.WriteLine("InterruptAssessAndUpdate: Updating scheduleId " + item.scheduleid + " with seconds played " + item.SecondsPlayed, "ScheduleManager");
                    }
                    catch
                    {
                        // If we have trouble getting it, then assume 0 to be safe
                        item.SecondsPlayed = 0;
                    }
                }

                // Sort the interrupt layouts
                CurrentInterruptSchedule.Sort(new ScheduleItemComparer(3600 - secondsIntoHour));
                CurrentInterruptSchedule.Reverse();

                // Do we need to interrupt at this moment, or not
                double percentageThroughHour = secondsIntoHour / 3600.0;
                int secondsShouldHaveInterrupted = Convert.ToInt32(Math.Floor(this._interruptState.TargetHourlyInterruption * percentageThroughHour));
                int secondsSinceLastInterrupt = Convert.ToInt32((DateTime.Now - this._interruptState.LastInterruption).TotalSeconds);

                Debug.WriteLine("InterruptAssessAndUpdate: Target = " + this._interruptState.TargetHourlyInterruption
                    + ", Required = " + secondsShouldHaveInterrupted
                    + ", Interrupted = " + this._interruptState.SecondsInterrutedThisHour
                    + ", Last Interrupt = " + secondsSinceLastInterrupt
                    , "ScheduleManager");

                // Interrupt if the seconds we've interrupted this hour so far is less than the seconds we
                // should have interrupted.
                if (Math.Floor(this._interruptState.SecondsInterrutedThisHour) < secondsShouldHaveInterrupted)
                {
                    OnInterruptNow?.Invoke();
                }
                else
                {
                    OnInterruptPausePending?.Invoke();
                }
            }
        }

        /// <summary>
        /// Reset interrupt seconds if we've gone into a new hour
        /// </summary>
        private void InterruptResetSecondsIfNecessary()
        {
            if (this._interruptState.LastPlaytimeUpdate < TopOfHour())
            {
                Debug.WriteLine("InterruptResetSecondsIfNecessary: LastPlaytimeUpdate in prior hour, resetting play time.", "ScheduleManager");

                this._interruptState.SecondsInterrutedThisHour = 0;
                this._interruptState.InterruptTracking.Clear();
            }
        }

        /// <summary>
        /// Mark an interrupt as having happened
        /// </summary>
        public void InterruptSetActive()
        {
            this._interruptState.LastInterruption = DateTime.Now;

            Debug.WriteLine("InterruptSetActive", "ScheduleManager");
        }

        /// <summary>
        /// Record how many seconds we've just played from an event.
        /// </summary>
        /// <param name="scheduleId"></param>
        /// <param name="seconds"></param>
        public void InterruptRecordSecondsPlayed(int scheduleId, double seconds)
        {
            Debug.WriteLine("InterruptRecordSecondsPlayed: scheduleId = " + scheduleId + ", seconds = " + seconds, "ScheduleManager");

            InterruptResetSecondsIfNecessary();

            // Add to our overall interrupted seconds
            this._interruptState.SecondsInterrutedThisHour += seconds;

            // Record the last play time as not
            this._interruptState.LastPlaytimeUpdate = DateTime.Now;

            // Update our tracker with these details.
            if (!this._interruptState.InterruptTracking.ContainsKey(scheduleId))
            {
                this._interruptState.InterruptTracking.Add(scheduleId, 0);
            }

            // Add new seconds to tracked seconds
            this._interruptState.InterruptTracking[scheduleId] += seconds;

            // Log
            Debug.WriteLine("InterruptRecordSecondsPlayed: Added " + seconds
                + " seconds to eventId " + scheduleId + ", new total is " + this._interruptState.InterruptTracking[scheduleId], "ScheduleManager");
        }

        /// <summary>
        /// Initialise interrupt state from disk
        /// </summary>
        private void InterruptInitState()
        {
            lock (_locker)
            {
                try
                {
                    if (File.Exists(ApplicationSettings.Default.LibraryPath + @"\interrupt.json"))
                    {
                        this._interruptState = JsonConvert.DeserializeObject<InterruptState>(File.ReadAllText(ApplicationSettings.Default.LibraryPath + @"\interrupt.json"));
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("ScheduleManager", "InterruptInitState: Failed to read interrupt file. e = " + e.Message), LogType.Error.ToString());
                }

                // If we are still empty after loading, we should create an empty object
                if (this._interruptState == null)
                {
                    // Create a new empty object
                    this._interruptState = InterruptState.EmptyState();
                }
            }            
        }

        /// <summary>
        /// Persist state to disk
        /// </summary>
        private void InterruptPersistState()
        {
            // If the interrupt state is null for whatever reason, don't persist it to file
            if (this._interruptState == null)
            {
                return;
            }

            try
            {
                lock (_locker)
                {
                    using (StreamWriter sw = new StreamWriter(ApplicationSettings.Default.LibraryPath + @"\interrupt.json", false, Encoding.UTF8))
                    {
                        sw.Write(JsonConvert.SerializeObject(this._interruptState, Newtonsoft.Json.Formatting.Indented));
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("ScheduleManager", "InterruptPersistState: Failed to update interrupt file. e = " + e.Message), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// Return the top of the Hour
        /// </summary>
        /// <returns></returns>
        private DateTime TopOfHour()
        {
            return new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);
        }

        #endregion
    }
}
