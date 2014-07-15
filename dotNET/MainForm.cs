/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-14 Daniel Garner
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
using XiboClient.Log;
using System.Threading;
using XiboClient.Properties;
using System.Runtime.InteropServices;
using System.Globalization;

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

        private ClientInfo _clientInfoForm;

        private delegate void ChangeToNextLayoutDelegate(string layoutPath);

        [FlagsAttribute]
        enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        public MainForm()
        {
            InitializeComponent();

            Thread.CurrentThread.Name = "UI Thread";

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

            // Show in taskbar
            ShowInTaskbar = Settings.Default.ShowInTaskbar;

            // Setup the proxy information
            OptionForm.SetGlobalProxy();

            _statLog = new StatLog();

            this.FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
            this.Shown += new EventHandler(MainForm_Shown);

            // Create the info form
            _clientInfoForm = new ClientInfo();
            _clientInfoForm.Hide();

            // Add a message filter to listen for the i key
            Application.AddMessageFilter(KeyStore.Instance);

            // Define the hotkey
            Keys key;
            try
            {
                key = (Keys)Enum.Parse(typeof(Keys), Settings.Default.ClientInformationKeyCode.ToUpper());
            }
            catch
            {
                // Default back to I
                key = Keys.I;
            }

            KeyStore.Instance.AddKeyDefinition("ClientInfo", key, ((Settings.Default.ClientInfomationCtrlKey) ? Keys.Control : Keys.None));

            // Register a handler for the key event
            KeyStore.Instance.KeyPress += Instance_KeyPress;

            // Trace listener for Client Info
            ClientInfoTraceListener clientInfoTraceListener = new ClientInfoTraceListener(_clientInfoForm);
            clientInfoTraceListener.Name = "ClientInfo TraceListener";
            Trace.Listeners.Add(clientInfoTraceListener);

            // Log to disk?
            if (!string.IsNullOrEmpty(Settings.Default.LogToDiskLocation))
            {
                TextWriterTraceListener listener = new TextWriterTraceListener(Settings.Default.LogToDiskLocation);
                Trace.Listeners.Add(listener);
            }

            Trace.WriteLine(new LogMessage("MainForm", "Client Initialised"), LogType.Info.ToString());
        }

        /// <summary>
        /// Handle the Key Event
        /// </summary>
        /// <param name="name"></param>
        void Instance_KeyPress(string name)
        {
            if (name != "ClientInfo")
                return;

            // Toggle
            if (_clientInfoForm.Visible)
                _clientInfoForm.Hide();
            else
            {
                _clientInfoForm.Show();
                _clientInfoForm.BringToFront();
            }
        }

        /// <summary>
        /// Called after the form has been shown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MainForm_Shown(object sender, EventArgs e)
        {
            // Create a cachemanager
            SetCacheManager();

            try
            {
                // Create the Schedule
                _schedule = new Schedule(Application.UserAppDataPath + "\\" + Properties.Settings.Default.ScheduleFile, ref _cacheManager, ref _clientInfoForm);

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

            // Is the mouse enabled?
            if (!Properties.Settings.Default.EnableMouse)
                // Hide the cursor
                Cursor.Hide();

            // Move the cursor to the starting place
            SetCursorStartPosition();

            // Show the splash screen
            ShowSplashScreen();

            // Change the default Proxy class
            OptionForm.SetGlobalProxy();

            // UserApp data
            Debug.WriteLine(new LogMessage("MainForm_Load", "User AppData Path: " + Application.UserAppDataPath), LogType.Info.ToString());
        }

        /// <summary>
        /// Called as the Main Form starts to close
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // We want to tidy up some stuff as this form closes.
            Trace.Listeners.Remove("ClientInfo TraceListener");

            // Close the client info screen
            _clientInfoForm.Hide();

            // Stop the schedule object
            _schedule.Stop();

            // Flush the stats
            _statLog.Flush();

            // Write the CacheManager to disk
            _cacheManager.WriteCacheManager();

            // Flush the logs
            Trace.Flush();
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
            Trace.WriteLine(new LogMessage("MainForm - ScheduleChangeEvent", string.Format("Schedule Changing to {0}", layoutPath)), LogType.Audit.ToString()); 

            _scheduleId = scheduleId;
            _layoutId = layoutId;

            if (_stat != null)
            {
                // Log the end of the currently running layout.
                _stat.toDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Record this stat event in the statLog object
                _statLog.RecordStat(_stat);
            }

            if (InvokeRequired)
            {
                BeginInvoke(new ChangeToNextLayoutDelegate(ChangeToNextLayout), layoutPath);
                return;
            }

            ChangeToNextLayout(layoutPath);
        }

        /// <summary>
        /// Change to the next layout
        /// </summary>
        private void ChangeToNextLayout(string layoutPath)
        {
            try
            {
                SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);

                // TODO: Check we are never out of the UI thread at this point

                DestroyLayout();

                _isExpired = false;

                PrepareLayout(layoutPath);

                _clientInfoForm.CurrentLayoutId = layoutPath;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("MainForm - ChangeToNextLayout", "Layout Change to " + layoutPath + " failed. Exception raised was: " + ex.Message), LogType.Error.ToString());
                _isExpired = true;

                ShowSplashScreen();

                // In 10 seconds fire the next layout?
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                timer.Interval = 10000;
                timer.Tick += new EventHandler(splashScreenTimer_Tick);

                // Start the timer
                timer.Start();
            }
        }

        /// <summary>
        /// Expire the Splash Screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void splashScreenTimer_Tick(object sender, EventArgs e)
        {
            Debug.WriteLine(new LogMessage("timer_Tick", "Loading next layout after splashscreen"));

            System.Windows.Forms.Timer timer = (System.Windows.Forms.Timer)sender;
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
            DateTime layoutModifiedTime;

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
                    using (FileStream fs = File.Open(layoutPath, FileMode.Open, FileAccess.Read, FileShare.Write))
                    {
                        using (XmlReader reader = XmlReader.Create(fs))
                        {
                            layoutXml.Load(reader);

                            reader.Close();
                        }
                        fs.Close();
                    }

                    layoutModifiedTime = File.GetLastWriteTime(layoutPath);
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
            _layoutWidth = int.Parse(layoutAttributes["width"].Value, CultureInfo.InvariantCulture);
            _layoutHeight = int.Parse(layoutAttributes["height"].Value, CultureInfo.InvariantCulture);


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
            options.LayoutModifiedDate = layoutModifiedTime;

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
                if (layoutAttributes["background"] != null && !string.IsNullOrEmpty(layoutAttributes["background"].Value))
                {
                    string bgFilePath = Settings.Default.LibraryPath + @"\backgrounds\" + backgroundWidth + "x" + backgroundHeight + "_" + layoutAttributes["background"].Value;

                    // Create a correctly sized background image in the temp folder
                    if (!File.Exists(bgFilePath))
                        GenerateBackgroundImage(layoutAttributes["background"].Value, backgroundWidth, backgroundHeight, bgFilePath);

                    BackgroundImage = new Bitmap(bgFilePath);
                    options.backgroundImage = bgFilePath;
                }
                else
                {
                    // Assume there is no background image
                    BackgroundImage = null;
                    options.backgroundImage = "";
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("MainForm - PrepareLayout", "Unable to set background: " + ex.Message), LogType.Error.ToString());
                
                // Assume there is no background image
                this.BackgroundImage = null;
                options.backgroundImage = "";
            }

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
                options.width = (int)(Convert.ToDouble(nodeAttibutes["width"].Value, CultureInfo.InvariantCulture) * _scaleFactor);
                options.height = (int)(Convert.ToDouble(nodeAttibutes["height"].Value, CultureInfo.InvariantCulture) * _scaleFactor);
                options.left = (int)(Convert.ToDouble(nodeAttibutes["left"].Value, CultureInfo.InvariantCulture) * _scaleFactor);
                options.top = (int)(Convert.ToDouble(nodeAttibutes["top"].Value, CultureInfo.InvariantCulture) * _scaleFactor);
                options.scaleFactor = _scaleFactor;

                // Store the original width and original height for scaling
                options.originalWidth = (int)Convert.ToDouble(nodeAttibutes["width"].Value, CultureInfo.InvariantCulture);
                options.originalHeight = (int)Convert.ToDouble(nodeAttibutes["height"].Value, CultureInfo.InvariantCulture);

                // Set the backgrounds (used for Web content offsets)
                options.backgroundLeft = options.left * -1;
                options.backgroundTop = options.top * -1;

                // Account for scaling
                options.left = options.left + (int)leftOverX;
                options.top = options.top + (int)leftOverY;

                // All the media nodes for this region / layout combination
                options.mediaNodes = region.SelectNodes("media");

                Region temp = new Region(ref _statLog, ref _cacheManager);
                temp.DurationElapsedEvent += new Region.DurationElapsedDelegate(temp_DurationElapsedEvent);

                Debug.WriteLine("Created new region", "MainForm - Prepare Layout");

                // Dont be fooled, this innocent little statement kicks everything off
                temp.regionOptions = options;

                _regions.Add(temp);
                Controls.Add(temp);

                Debug.WriteLine("Adding region", "MainForm - Prepare Layout");
            }

            // Null stuff
            listRegions = null;
            listMedia = null;
        }

        /// <summary>
        /// Generates a background image and saves it in the library for use later
        /// </summary>
        /// <param name="layoutAttributes"></param>
        /// <param name="backgroundWidth"></param>
        /// <param name="backgroundHeight"></param>
        /// <param name="bgFilePath"></param>
        private static void GenerateBackgroundImage(string sourceFile, int backgroundWidth, int backgroundHeight, string bgFilePath)
        {
            Trace.WriteLine(new LogMessage("MainForm - GenerateBackgroundImage", "Trying to generate a background image. It will be saved: " + bgFilePath), LogType.Audit.ToString());

            using (Image img = Image.FromFile(Settings.Default.LibraryPath + @"\" + sourceFile))
            {
                using (Bitmap bmp = new Bitmap(img, backgroundWidth, backgroundHeight))
                {
                    EncoderParameters encoderParameters = new EncoderParameters(1);
                    EncoderParameter qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
                    encoderParameters.Param[0] = qualityParam;

                    ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");

                    bmp.Save(bgFilePath, jpegCodec, encoderParameters);
                }
            }
        }

        /// <summary>
        /// Shows the splash screen (set the background to the embedded resource)
        /// </summary>
        private void ShowSplashScreen()
        {
            if (!string.IsNullOrEmpty(Settings.Default.SplashOverride))
            {
                try
                {
                    using (Image bgSplash = Image.FromFile(Settings.Default.SplashOverride))
                    {
                        Bitmap bmpSplash = new Bitmap(bgSplash, _clientSize);
                        BackgroundImage = bmpSplash;
                    }
                }
                catch
                {
                    Trace.WriteLine(new LogMessage("ShowSplashScreen", "Unable to load user splash screen"), LogType.Error.ToString());
                    ShowDefaultSplashScreen();
                }
            }
            else
            {
                ShowDefaultSplashScreen();
            }
        }

        /// <summary>
        /// Show the Default Splash Screen
        /// </summary>
        private void ShowDefaultSplashScreen()
        {
            // We are running with the Default.xml - meaning the schedule doesnt exist
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Stream resourceStream = assembly.GetManifestResourceStream("XiboClient.Resources.splash.jpg");

            Debug.WriteLine("Showing Splash Screen");

            // Load into a stream and then into an Image
            try
            {
                using (Image bgSplash = Image.FromStream(resourceStream))
                {
                    Bitmap bmpSplash = new Bitmap(bgSplash, _clientSize);
                    BackgroundImage = bmpSplash;
                }
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
                if (!temp._hasExpired)
                {
                    _isExpired = false;
                }
            }

            if (_isExpired)
            {
                // Inform each region that the layout containing it has expired
                foreach (Region temp in _regions)
                {
                    temp._layoutExpired = true;
                }

                System.Diagnostics.Debug.WriteLine("Region Expired - Next Region.", "MainForm - DurationElapsedEvent");
                _schedule.NextLayout();
            }
        }

        /// <summary>
        /// Disposes Layout - removes the controls
        /// </summary>
        private void DestroyLayout() 
        {
            Debug.WriteLine("Destroying Layout", "MainForm - DestoryLayout");

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
        /// Set the Cursor start position
        /// </summary>
        private void SetCursorStartPosition()
        {
            Point position;

            switch (Settings.Default.CursorStartPosition)
            {
                case "Top Left":
                    position = new Point(0, 0);
                    break;

                case "Top Right":
                    position = new Point(_clientSize.Width, 0);
                    break;

                case "Bottom Left":
                    position = new Point(0, _clientSize.Height);
                    break;

                case "Bottom Right":
                default:
                    position = new Point(_clientSize.Width, _clientSize.Height);
                    break;
            }

            Cursor.Position = position;
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