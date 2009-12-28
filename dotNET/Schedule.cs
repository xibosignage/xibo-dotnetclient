/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006,2007,2008 Daniel Garner and James Packer
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

namespace XiboClient
{
    /// <summary>
    /// Reads the schedule
    /// </summary>
    class Schedule
    {
        public delegate void ScheduleChangeDelegate(string layoutPath, int scheduleId, int layoutId);
        public event ScheduleChangeDelegate ScheduleChangeEvent;

        private Collection<LayoutSchedule> layoutSchedule;
        private int currentLayout = 0;
        private string scheduleLocation;

        //FileCollector
        private XiboClient.xmds.xmds xmds2;
        private bool xmdsProcessing;
        private bool forceChange = false;

        // Key
        private HardwareKey hardwareKey;

        // Cache Manager
        private CacheManager _cacheManager;

        /// <summary>
        /// Create a schedule
        /// </summary>
        /// <param name="scheduleLocation"></param>
        public Schedule(string scheduleLocation)
        {
            // Save the schedule location
            this.scheduleLocation = scheduleLocation;

            // Create a new collection for the layouts in the schedule
            layoutSchedule = new Collection<LayoutSchedule>();
            
            // Create a new cache manager
            _cacheManager = new CacheManager();

            // Create a new Xmds service object
            xmds2 = new XiboClient.xmds.xmds();
        }

        /// <summary>
        /// Initialize the Schedule components
        /// </summary>
        public void InitializeComponents() 
        {
            //
            // Parse and Load the Schedule into the Collection
            //
            this.GetSchedule();

            // Get the key for this display
            hardwareKey = new HardwareKey();

            //
            // Start up the Xmds Service Object
            //
            this.xmds2.Credentials = null;
            this.xmds2.Url = Properties.Settings.Default.XiboClient_xmds_xmds;
            this.xmds2.UseDefaultCredentials = false;

            xmdsProcessing = false;
            xmds2.RequiredFilesCompleted += new XiboClient.xmds.RequiredFilesCompletedEventHandler(xmds2_RequiredFilesCompleted);
            xmds2.ScheduleCompleted += new XiboClient.xmds.ScheduleCompletedEventHandler(xmds2_ScheduleCompleted);

            System.Diagnostics.Trace.WriteLine(String.Format("Collection Interval: {0}", Properties.Settings.Default.collectInterval), "Schedule - InitializeComponents");
            //
            // The Timer for the Service call
            //
            Timer xmdsTimer = new Timer();
            xmdsTimer.Interval = (int) Properties.Settings.Default.collectInterval * 1000;
            xmdsTimer.Tick += new EventHandler(xmdsTimer_Tick);
            xmdsTimer.Start();

            // Manual first tick
            xmdsProcessing = true;

            // Fire off a get required files event - async
            xmds2.RequiredFilesAsync(Properties.Settings.Default.ServerKey, hardwareKey.Key, Properties.Settings.Default.Version);
        }

        void xmds2_RequiredFilesCompleted(object sender, XiboClient.xmds.RequiredFilesCompletedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("RequiredFilesAsync complete.", "Schedule - RequiredFilesCompleted");

            //Dont let this effect the rendering
            Application.DoEvents();

            if (e.Error != null)
            {
                //There was an error - what do we do?
                System.Diagnostics.Trace.WriteLine(e.Error.Message);

                // Is it a "not licensed" error
                if (e.Error.Message == "This display client is not licensed")
                {
                    Properties.Settings.Default.licensed = 0;
                }

                xmdsProcessing = false;
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
                    xmdsProcessing = false;

                    // Log and move on
                    System.Diagnostics.Debug.WriteLine("Error Comparing and Collecting", "Schedule - RequiredFilesCompleted");
                    System.Diagnostics.Debug.WriteLine(ex.Message, "Schedule - RequiredFilesCompleted");
                }
            }
        }

        void fileCollector_MediaFileChanged(string path)
        {
            System.Diagnostics.Debug.WriteLine("Media file changed");
            return;
        }

        void fileCollector_CollectionComplete()
        {
            System.Diagnostics.Debug.WriteLine("File Collector Complete - getting Schedule.");
            
            // All the files have been collected, so we want to update the schedule (do we want to send off a MD5 of the schedule?)
            xmds2.ScheduleAsync(Properties.Settings.Default.ServerKey, hardwareKey.Key, Properties.Settings.Default.Version);
        }

        void xmds2_ScheduleCompleted(object sender, XiboClient.xmds.ScheduleCompletedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Schedule Retrival Complete.");

            // Set XMDS to no longer be processing
            xmdsProcessing = false;

            // Expect new schedule XML
            if (e.Error != null)
            {
                //There was an error - what do we do?
                System.Diagnostics.Trace.WriteLine(e.Error.Message);
            }
            else
            {
                // Only update the schedule if its changed.
                String md5CurrentSchedule = "";

                // Set the flag to indicate we have a connection to XMDS
                Properties.Settings.Default.XmdsLastConnection = DateTime.Now;

                try
                {
                    StreamReader sr = new StreamReader(File.Open(this.scheduleLocation, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));

                    // Yes - get the MD5 of it, and compare to the MD5 of the file in the XML
                    md5CurrentSchedule = Hashes.MD5(sr.ReadToEnd());

                    sr.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }

                // Hash of the result
                String md5NewSchedule = Hashes.MD5(e.Result);

                if (md5CurrentSchedule == md5NewSchedule) return;

                System.Diagnostics.Debug.WriteLine("Different Schedules Detected, writing new schedule.", "Schedule - ScheduleCompleted");

                // Write the result to the schedule xml location
                StreamWriter sw = new StreamWriter(this.scheduleLocation, false, Encoding.UTF8);
                sw.Write(e.Result);

                sw.Close();

                System.Diagnostics.Debug.WriteLine("New Schedule Recieved", "xmds_ScheduleCompleted");

                // The schedule has been updated with new information.
                // We could improve the logic here, perhaps generating a new layoutSchedule collection and comparing the two before we destroy this one..
                layoutSchedule.Clear();

                this.GetSchedule();
            }
        }

        void fileCollector_LayoutFileChanged(string layoutPath)
        {
            System.Diagnostics.Debug.WriteLine("Layout file changed");

            // If the layout that got changed is the current layout, move on
            try
            {
                if (layoutSchedule[currentLayout].layoutFile == Properties.Settings.Default.LibraryPath + @"\" + layoutPath)
                {
                    forceChange = true;
                    NextLayout();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(new LogMessage("fileCollector_LayoutFileChanged", String.Format("Unable to determine current layout with exception {0}", ex.Message)), LogType.Error.ToString());
            }
        }

        void xmdsTimer_Tick(object sender, EventArgs e)
        {

            // Ticks every "collectInterval"
            if (xmdsProcessing)
            {
                System.Diagnostics.Debug.WriteLine("Collection Timer Ticked, but previous request still active", "XmdsTicker");
                return;
            }
            else
            {
                Application.DoEvents(); // Make sure everything that is queued to render does

                xmdsProcessing = true;

                System.Diagnostics.Debug.WriteLine("Collection Timer Ticked, Firing RequiredFilesAsync");

                // Fire off a get required files event - async
                xmds2.RequiredFilesAsync(Properties.Settings.Default.ServerKey, hardwareKey.Key, Properties.Settings.Default.Version);
            }

            // Flush the log
            System.Diagnostics.Trace.Flush();
        }

        /// <summary>
        /// Moves the layout on
        /// </summary>
        public void NextLayout()
        {
            int previousLayout = currentLayout;

            //increment the current layout
            currentLayout++;

            //if the current layout is greater than the count of layouts, then reset to 0
            if (currentLayout >= layoutSchedule.Count)
            {
                currentLayout = 0;
            }

            if (layoutSchedule.Count == 1 && !forceChange)
            {
                //dont bother raising the event, just keep on this until the schedule gets changed
                return;
            }

            System.Diagnostics.Debug.WriteLine(String.Format("Next layout: {0}", layoutSchedule[currentLayout].layoutFile), "Schedule - Next Layout");

            forceChange = false;

            //Raise the event
            ScheduleChangeEvent(layoutSchedule[currentLayout].layoutFile, layoutSchedule[currentLayout].scheduleid, layoutSchedule[currentLayout].id);
        }

        /// <summary>
        /// Gets the schedule from the schedule location
        /// If the schedule location doesn't exist - use the default.xml (how to we guarentee that the default.xml exists?)
        /// If layouts in the schedule file dont exist, then ignore them - if none of them exist then add a default one?
        /// </summary>
        void GetSchedule()
        {
            XmlDocument scheduleXml;

            // Check the schedule file exists
            if (File.Exists(scheduleLocation))
            {
                // Read the schedule file
                XmlReader reader = XmlReader.Create(scheduleLocation);

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

            // Get the layouts in this schedule
            XmlNodeList listLayouts = scheduleXml.SelectNodes("/schedule/layout");

            // Have we got some?
            if (listLayouts.Count == 0)
            {
                // Schedule up the default
                LayoutSchedule temp = new LayoutSchedule();
                temp.layoutFile = Properties.Settings.Default.LibraryPath + @"\Default.xml";
                temp.id = 0;
                temp.scheduleid = 0;

                layoutSchedule.Add(temp);
            }
            else
            {
                // Get them and add to collection
                foreach (XmlNode layout in listLayouts)
                {
                    XmlAttributeCollection attributes = layout.Attributes;

                    string layoutFile = attributes["file"].Value;
                    string replace = ".xml";
                    layoutFile = layoutFile.TrimEnd(replace.ToCharArray());
                    
                    string scheduleId = "";
                    if (attributes["scheduleid"] != null) scheduleId = attributes["scheduleid"].Value;

                    LayoutSchedule temp = new LayoutSchedule();
                    temp.layoutFile = Properties.Settings.Default.LibraryPath + @"\" + layoutFile + @".xlf";
                    temp.id = int.Parse(layoutFile);
                    
                    if (scheduleId != "") temp.scheduleid = int.Parse(scheduleId);

                    layoutSchedule.Add(temp);
                }
            }

            //clean up
            listLayouts = null;
            scheduleXml = null;

            //raise an event
            ScheduleChangeEvent(layoutSchedule[0].layoutFile, layoutSchedule[0].scheduleid, layoutSchedule[0].id);

        }

        /// <summary>
        /// The number of active layouts in the current schedule
        /// </summary>
        public int ActiveLayouts
        {
            get
            {
                return layoutSchedule.Count;
            }
        }

        /// <summary>
        /// A LayoutSchedule
        /// </summary>
        [Serializable]
        public struct LayoutSchedule
        {
            public string layoutFile;
            public int id;
            public int scheduleid;
        }
    }
}
