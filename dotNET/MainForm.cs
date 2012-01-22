/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-10 Daniel Garner and James Packer
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
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Net;
using System.IO;
using System.Drawing.Imaging;
using System.Xml.Serialization;
using System.Diagnostics;

namespace XiboClient
{
    public partial class MainForm : Form
    {
        private Schedule _schedule;
        private Collection<Region> _regions;
        private bool _isExpired = false;
        private int _scheduleId;
        private int _layoutId;

        double _layoutWidth;
        double _layoutHeight;
        double _scaleFactor;
        private Size _clientSize;

        private StatLog _statLog;
        private Stat _stat;
        private CacheManager _cacheManager;

        public MainForm()
        {
            InitializeComponent();

            // Override the default size if necessary
            if (Properties.Settings.Default.sizeX != 0)
            {
                _clientSize = new Size((int)Properties.Settings.Default.sizeX, (int)Properties.Settings.Default.sizeY);
                Size = _clientSize;
                WindowState = FormWindowState.Normal;
                Location = new Point((int)Properties.Settings.Default.offsetX, (int)Properties.Settings.Default.offsetY);
                StartPosition = FormStartPosition.Manual;
            }
            else
            {
                _clientSize = SystemInformation.PrimaryMonitorSize;
            }

            // Setup the proxy information
            OptionForm.SetGlobalProxy();

            _statLog = new StatLog();

            this.FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
            this.Shown += new EventHandler(MainForm_Shown);
        }

        /// <summary>
        /// Called after the form has been shown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MainForm_Shown(object sender, EventArgs e)
        {
            // Process any stuff that has happened during the loading process
            Application.DoEvents();

            // Create a cachemanager
            SetCacheManager();

            try
            {
                // Create the Schedule
                _schedule = new Schedule(Application.UserAppDataPath + "\\" + Properties.Settings.Default.ScheduleFile, ref _cacheManager);

                // Bind to the schedule change event - notifys of changes to the schedule
                _schedule.ScheduleChangeEvent += new Schedule.ScheduleChangeDelegate(schedule_ScheduleChangeEvent);

                // Initialize the other schedule components
                _schedule.InitializeComponents();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message, LogType.Error.ToString());
                MessageBox.Show("Fatal Error initialising the application", "Fatal Error");
                Close();
                Dispose();
            }
        }

        /// <summary>
        /// Called before the form has loaded for the first time
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Check the directories exist
            if (!Directory.Exists(Properties.Settings.Default.LibraryPath) || !Directory.Exists(Properties.Settings.Default.LibraryPath + @"\backgrounds\"))
            {
                // Will handle the create of everything here
                Directory.CreateDirectory(Properties.Settings.Default.LibraryPath + @"\backgrounds");
            }

            // Hide the cursor
            Cursor.Position = new Point(_clientSize.Width, _clientSize.Height);
            
            if (!Properties.Settings.Default.EnableMouse)
                Cursor.Hide();

            ShowSplashScreen();

            // Change the default Proxy class
            OptionForm.SetGlobalProxy();

            // UserApp data
            Debug.WriteLine(new LogMessage("MainForm_Load", "User AppData Path: " + Application.UserAppDataPath), LogType.Info.ToString());
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // We want to tidy up some stuff as this form closes.

            // Flush the stats
            _statLog.Flush();

            // Write the CacheManager to disk
            _cacheManager.WriteCacheManager();

            // Flush the logs
            System.Diagnostics.Trace.Flush();
        }

        /// <summary>
        /// Sets the CacheManager
        /// </summary>
        private void SetCacheManager()
        {
            try
            {
                using (FileStream fileStream = File.Open(Application.UserAppDataPath + "\\" + Properties.Settings.Default.CacheManagerFile, FileMode.Open))
                {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(CacheManager));

                    _cacheManager = (CacheManager)xmlSerializer.Deserialize(fileStream);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("MainForm - SetCacheManager", "Unable to reuse the Cache Manager because: " + ex.Message));

                // Create a new cache manager
                _cacheManager = new CacheManager();
            }

            try
            {
                _cacheManager.Regenerate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("MainForm - SetCacheManager", "Regenerate failed because: " + ex.Message));
            }
        }

        /// <summary>
        /// Handles the ScheduleChange event
        /// </summary>
        /// <param name="layoutPath"></param>
        void schedule_ScheduleChangeEvent(string layoutPath, int scheduleId, int layoutId)
        {
            System.Diagnostics.Debug.WriteLine(String.Format("Schedule Changing to {0}", layoutPath), "MainForm - ScheduleChangeEvent");

            _scheduleId = scheduleId;
            _layoutId = layoutId;

            if (_stat != null)
            {
                // Log the end of the currently running layout.
                _stat.toDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Record this stat event in the statLog object
                _statLog.RecordStat(_stat);
            }

            try
            {
                DestroyLayout();

                _isExpired = false;

                PrepareLayout(layoutPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                _isExpired = true;

                ShowSplashScreen();

                // In 10 seconds fire the next layout?
                Timer timer = new Timer();
                timer.Interval = 10000;
                timer.Tick += new EventHandler(splashScreenTimer_Tick);

                // Start the timer
                timer.Start();
            }
        }

        void splashScreenTimer_Tick(object sender, EventArgs e)
        {
            Debug.WriteLine(new LogMessage("timer_Tick", "Loading next layout after splashscreen"));

            Timer timer = (Timer)sender;
            timer.Stop();
            timer.Dispose();

            _schedule.NextLayout();
        }

        /// <summary>
        /// Prepares the Layout.. rendering all the necessary controls
        /// </summary>
        private void PrepareLayout(string layoutPath)
        {
            // Create a start record for this layout
            _stat = new Stat();
            _stat.type = StatType.Layout;
            _stat.scheduleID = _scheduleId;
            _stat.layoutID = _layoutId;
            _stat.fromDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Get this layouts XML
            XmlDocument layoutXml = new XmlDocument();

            // Default or not
            if (layoutPath == Properties.Settings.Default.LibraryPath + @"\Default.xml" || String.IsNullOrEmpty(layoutPath))
            {
                throw new Exception("Default layout");
            }
            else
            {
                try
                {
                    // try to open the layout file
                    FileStream fs = File.Open(layoutPath, FileMode.Open, FileAccess.Read, FileShare.Write);

                    XmlReader reader = XmlReader.Create(fs);

                    layoutXml.Load(reader);

                    reader.Close();
                    fs.Close();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(string.Format("Could not find the layout file {0}: {1}", layoutPath, ex.Message));
                    throw;
                }
            }

            // Attributes of the main layout node
            XmlNode layoutNode = layoutXml.SelectSingleNode("/layout");

            XmlAttributeCollection layoutAttributes = layoutNode.Attributes;

            // Set the background and size of the form
            _layoutWidth = int.Parse(layoutAttributes["width"].Value);
            _layoutHeight = int.Parse(layoutAttributes["height"].Value);


            // Scaling factor, will be applied to all regions
            _scaleFactor = Math.Min(_clientSize.Width / _layoutWidth, _clientSize.Height / _layoutHeight);

            // Want to be able to center this shiv - therefore work out which one of these is going to have left overs
            int backgroundWidth = (int)(_layoutWidth * _scaleFactor);
            int backgroundHeight = (int)(_layoutHeight * _scaleFactor);

            double leftOverX;
            double leftOverY;

            try
            {
                leftOverX = Math.Abs(_clientSize.Width - backgroundWidth);
                leftOverY = Math.Abs(_clientSize.Height - backgroundHeight);

                if (leftOverX != 0) leftOverX = leftOverX / 2;
                if (leftOverY != 0) leftOverY = leftOverY / 2;
            }
            catch
            {
                leftOverX = 0;
                leftOverY = 0;
            }

            // New region and region options objects
            _regions = new Collection<Region>();
            RegionOptions options = new RegionOptions();

            // Deal with the color
            try
            {
                if (layoutAttributes["bgcolor"].Value != "")
                {
                    this.BackColor = ColorTranslator.FromHtml(layoutAttributes["bgcolor"].Value);
                    options.backgroundColor = layoutAttributes["bgcolor"].Value;
                }
            }
            catch
            {
                this.BackColor = Color.Black; // Default black
                options.backgroundColor = "#000000";
            }

            // Get the background
            try
            {
                if (layoutAttributes["background"] == null)
                {
                    // Assume there is no background image
                    BackgroundImage = null;
                    options.backgroundImage = "";
                }
                else
                {
                    string bgFilePath = Properties.Settings.Default.LibraryPath + @"\backgrounds\" + backgroundWidth + "x" + backgroundHeight + "_" + layoutAttributes["background"].Value;

                    // Create a correctly sized background image in the temp folder
                    if (!File.Exists(bgFilePath))
                    {
                        Image img = Image.FromFile(Properties.Settings.Default.LibraryPath + @"\" + layoutAttributes["background"].Value);

                        Bitmap bmp = new Bitmap(img, backgroundWidth, backgroundHeight);
                        EncoderParameters encoderParameters = new EncoderParameters(1);
                        EncoderParameter qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                        encoderParameters.Param[0] = qualityParam;

                        ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");

                        bmp.Save(bgFilePath, jpegCodec, encoderParameters);

                        img.Dispose();
                        bmp.Dispose();
                    }

                    BackgroundImage = new Bitmap(bgFilePath);
                    options.backgroundImage = bgFilePath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unable to set background: " + ex.Message);

                // Assume there is no background image
                this.BackgroundImage = null;
                options.backgroundImage = "";
            }

            // Get it to paint the background now
            Application.DoEvents();

            // Get the regions
            XmlNodeList listRegions = layoutXml.SelectNodes("/layout/region");
            XmlNodeList listMedia = layoutXml.SelectNodes("/layout/region/media");

            // Check to see if there are any regions on this layout.
            if (listRegions.Count == 0 || listMedia.Count == 0)
            {
                Trace.WriteLine(new LogMessage("PrepareLayout",
                    string.Format("A layout with {0} regions and {1} media has been detected.", listRegions.Count.ToString(), listMedia.Count.ToString())),
                    LogType.Info.ToString());

                if (_schedule.ActiveLayouts == 1)
                {
                    Trace.WriteLine(new LogMessage("PrepareLayout", "Only 1 layout scheduled and it has nothing to show."), LogType.Info.ToString());

                    throw new Exception("Only 1 layout schduled and it has nothing to show");
                }
                else
                {
                    Trace.WriteLine(new LogMessage("PrepareLayout",
                        string.Format(string.Format("An empty layout detected, will show for {0} seconds.", Properties.Settings.Default.emptyLayoutDuration.ToString()))), LogType.Info.ToString());

                    // Put a small dummy region in place, with a small dummy media node - which expires in 10 seconds.
                    XmlDocument dummyXml = new XmlDocument();
                    dummyXml.LoadXml(string.Format("<region id='blah' width='1' height='1' top='1' left='1'><media id='blah' type='text' duration='{0}'><raw><text></text></raw></media></region>",
                        Properties.Settings.Default.emptyLayoutDuration.ToString()));

                    // Replace the list of regions (they mean nothing as they are empty)
                    listRegions = dummyXml.SelectNodes("/region");
                }
            }

            foreach (XmlNode region in listRegions)
            {
                // Is there any media
                if (region.ChildNodes.Count == 0)
                {
                    Debug.WriteLine("A region with no media detected");
                    continue;
                }

                //each region
                XmlAttributeCollection nodeAttibutes = region.Attributes;

                options.scheduleId = _scheduleId;
                options.layoutId = _layoutId;
                options.regionId = nodeAttibutes["id"].Value.ToString();
                options.width = (int)(double.Parse(nodeAttibutes["width"].Value) * _scaleFactor);
                options.height = (int)(double.Parse(nodeAttibutes["height"].Value) * _scaleFactor);
                options.left = (int)(double.Parse(nodeAttibutes["left"].Value) * _scaleFactor);
                options.top = (int)(double.Parse(nodeAttibutes["top"].Value) * _scaleFactor);
                options.scaleFactor = _scaleFactor;

                // Set the backgrounds (used for Web content offsets)
                options.backgroundLeft = options.left * -1;
                options.backgroundTop = options.top * -1;

                //Account for scaling
                options.left = options.left + (int)leftOverX;
                options.top = options.top + (int)leftOverY;

                // All the media nodes for this region / layout combination
                options.mediaNodes = region.ChildNodes;

                Region temp = new Region(ref _statLog, ref _cacheManager);
                temp.DurationElapsedEvent += new Region.DurationElapsedDelegate(temp_DurationElapsedEvent);

                Debug.WriteLine("Created new region", "MainForm - Prepare Layout");

                // Dont be fooled, this innocent little statement kicks everything off
                temp.regionOptions = options;

                _regions.Add(temp);
                Controls.Add(temp);

                Debug.WriteLine("Adding region", "MainForm - Prepare Layout");

                Application.DoEvents();
            }

            // Null stuff
            listRegions = null;
            listMedia = null;
        }

        /// <summary>
        /// Shows the splash screen (set the background to the embedded resource)
        /// </summary>
        private void ShowSplashScreen()
        {
            // We are running with the Default.xml - meaning the schedule doesnt exist
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Stream resourceStream = assembly.GetManifestResourceStream("XiboClient.Resources.splash.jpg");

            Debug.WriteLine("Showing Splash Screen");

            // Load into a stream and then into an Image
            try
            {
                Image bgSplash = Image.FromStream(resourceStream);

                Bitmap bmpSplash = new Bitmap(bgSplash, _clientSize);
                this.BackgroundImage = bmpSplash;

                bgSplash.Dispose();
            }
            catch (Exception ex)
            {
                // Log
                Debug.WriteLine("Failed Showing Splash Screen: " + ex.Message);
            }
        }

        /// <summary> 
        /// Returns the image codec with the given mime type 
        /// </summary> 
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            // Get image codecs for all image formats 
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

            // Find the correct image codec 
            for (int i = 0; i < codecs.Length; i++)
                if (codecs[i].MimeType == mimeType)
                    return codecs[i];
            return null;
        }

        /// <summary>
        /// The duration of a Region has been reached
        /// </summary>
        void temp_DurationElapsedEvent()
        {
            Debug.WriteLine("Region Elapsed", "MainForm - DurationElapsedEvent");

            _isExpired = true;
            
            // Check the other regions to see if they are also expired.
            foreach (Region temp in _regions)
            {
                if (!temp.hasExpired)
                {
                    _isExpired = false;
                }
            }

            if (_isExpired)
            {
                // Inform each region that the layout containing it has expired
                foreach (Region temp in _regions)
                {
                    temp.layoutExpired = true;
                }

                System.Diagnostics.Debug.WriteLine("Region Expired - Next Region.", "MainForm - DurationElapsedEvent");
                _schedule.NextLayout();
            }

            Application.DoEvents();
        }

        /// <summary>
        /// Disposes Layout - removes the controls
        /// </summary>
        private void DestroyLayout() 
        {
            System.Diagnostics.Debug.WriteLine("Destroying Layout", "MainForm - DestoryLayout");

            Application.DoEvents();

            if (_regions == null) return;

            foreach (Region region in _regions)
            {
                region.Clear();

                this.Controls.Remove(region);

                try
                {
                    System.Diagnostics.Debug.WriteLine("Calling Dispose Region", "MainForm - DestoryLayout");
                    region.Dispose();
                }
                catch (Exception e)
                {
                    //do nothing (perhaps write to some error xml somewhere?)
                    System.Diagnostics.Debug.WriteLine(e.Message);
                }
            }

            _regions.Clear();
            _regions = null;
        }

        /// <summary>
        /// Force a flush of the stats log
        /// </summary>
        public void FlushStats()
        {
            try
            {
                _statLog.Flush();
            }
            catch
            {
                System.Diagnostics.Trace.WriteLine(new LogMessage("MainForm - FlushStats", "Unable to Flush Stats"), LogType.Error.ToString());
            }
        }
    }
}