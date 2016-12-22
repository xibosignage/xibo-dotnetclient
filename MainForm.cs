/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-16 Daniel Garner
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
using XiboClient.Logic;
using XiboClient.Control;
using XiboClient.Error;

namespace XiboClient
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Schedule Class
        /// </summary>
        private Schedule _schedule;

        /// <summary>
        /// Regions
        /// </summary>
        private Collection<Region> _regions;

        /// <summary>
        /// Overlay Regions
        /// </summary>
        private Collection<Region> _overlays;

        private bool _changingLayout = false;
        private int _scheduleId;
        private int _layoutId;
        private bool _screenSaver = false;
        private bool _showingSplash = false;

        double _layoutWidth;
        double _layoutHeight;
        double _scaleFactor;
        private Size _clientSize;

        private StatLog _statLog;
        private Stat _stat;
        private CacheManager _cacheManager;

        private ClientInfo _clientInfoForm;

        private delegate void ChangeToNextLayoutDelegate(string layoutPath);
        private delegate void ManageOverlaysDelegate(Collection<ScheduleItem> overlays);

        /// <summary>
        /// Border style - usually none, but useful for debugging.
        /// </summary>
        private BorderStyle _borderStyle = BorderStyle.None;

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

        // Changes the parent window of the specified child window
        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        // Changes an attribute of the specified window
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // Retrieves information about the specified window
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        // Retrieves the coordinates of a window's client area
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out Rectangle lpRect);

        public MainForm(IntPtr previewHandle)
        {
            InitializeComponent();
            
            // Set the preview window of the screen saver selection 
            // dialog in Windows as the parent of this form.
            SetParent(this.Handle, previewHandle);

            // Set this form to a child form, so that when the screen saver selection 
            // dialog in Windows is closed, this form will also close.
            SetWindowLong(this.Handle, -16, new IntPtr(GetWindowLong(this.Handle, -16) | 0x40000000));

            // Set the size of the screen saver to the size of the screen saver 
            // preview window in the screen saver selection dialog in Windows.
            Rectangle ParentRect;
            GetClientRect(previewHandle, out ParentRect);
            
            ApplicationSettings.Default.SizeX = ParentRect.Size.Width;
            ApplicationSettings.Default.SizeY = ParentRect.Size.Height;
            ApplicationSettings.Default.OffsetX = 0;
            ApplicationSettings.Default.OffsetY = 0;

            InitializeScreenSaver(true);
            InitializeXibo();
        }

        public MainForm(bool screenSaver)
        {
            InitializeComponent();

            if (screenSaver)
                InitializeScreenSaver(false);
            
            InitializeXibo();
        }

        public MainForm()
        {
            InitializeComponent();
            InitializeXibo();
        }

        private void InitializeXibo()
        {
            Thread.CurrentThread.Name = "UI Thread";

            // Check the directories exist
            if (!Directory.Exists(ApplicationSettings.Default.LibraryPath) || !Directory.Exists(ApplicationSettings.Default.LibraryPath + @"\backgrounds\"))
            {
                // Will handle the create of everything here
                Directory.CreateDirectory(ApplicationSettings.Default.LibraryPath + @"\backgrounds");
            }

            // Default the XmdsConnection
            ApplicationSettings.Default.XmdsLastConnection = DateTime.MinValue;

            // Override the default size if necessary
            if (ApplicationSettings.Default.SizeX != 0)
            {
                _clientSize = new Size((int)ApplicationSettings.Default.SizeX, (int)ApplicationSettings.Default.SizeY);
                Size = _clientSize;
                WindowState = FormWindowState.Normal;
                Location = new Point((int)ApplicationSettings.Default.OffsetX, (int)ApplicationSettings.Default.OffsetY);
                StartPosition = FormStartPosition.Manual;
            }
            else
            {
                _clientSize = SystemInformation.PrimaryMonitorSize;
                ApplicationSettings.Default.SizeX = _clientSize.Width;
                ApplicationSettings.Default.SizeY = _clientSize.Height;
            }

            // Show in taskbar
            ShowInTaskbar = ApplicationSettings.Default.ShowInTaskbar;

            // Setup the proxy information
            OptionForm.SetGlobalProxy();

            _statLog = new StatLog();

            this.FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
            this.Shown += new EventHandler(MainForm_Shown);

            // Create the info form
            _clientInfoForm = new ClientInfo();
            _clientInfoForm.Hide();

            // Define the hotkey
            Keys key;
            try
            {
                key = (Keys)Enum.Parse(typeof(Keys), ApplicationSettings.Default.ClientInformationKeyCode.ToUpper());
            }
            catch
            {
                // Default back to I
                key = Keys.I;
            }

            KeyStore.Instance.AddKeyDefinition("ClientInfo", key, ((ApplicationSettings.Default.ClientInfomationCtrlKey) ? Keys.Control : Keys.None));

            // Register a handler for the key event
            KeyStore.Instance.KeyPress += Instance_KeyPress;

            // Trace listener for Client Info
            ClientInfoTraceListener clientInfoTraceListener = new ClientInfoTraceListener(_clientInfoForm);
            clientInfoTraceListener.Name = "ClientInfo TraceListener";
            Trace.Listeners.Add(clientInfoTraceListener);

            // Log to disk?
            if (!string.IsNullOrEmpty(ApplicationSettings.Default.LogToDiskLocation))
            {
                TextWriterTraceListener listener = new TextWriterTraceListener(ApplicationSettings.Default.LogToDiskLocation);
                Trace.Listeners.Add(listener);
            }

#if !DEBUG
            // Initialise the watchdog
            if (!_screenSaver)
            {
                try
                {
                    // Update/write the status.json file
                    File.WriteAllText(Path.Combine(ApplicationSettings.Default.LibraryPath, "status.json"), "{\"lastActivity\":\"" + DateTime.Now.ToString() + "\"}");

                    // Start watchdog
                    WatchDogManager.Start();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("MainForm - InitializeXibo", "Cannot start watchdog. E = " + e.Message), LogType.Error.ToString());
                }
            }
#endif
            // An empty set of overlay regions
            _overlays = new Collection<Region>();
            
            Trace.WriteLine(new LogMessage("MainForm", "Client Initialised"), LogType.Info.ToString());
        }

        private void InitializeScreenSaver(bool preview)
        {
            _screenSaver = true;

            // Configure some listeners for the mouse (to quit)
            if (!preview)
            {
                KeyStore.Instance.ScreenSaver = true;

                MouseInterceptor.Instance.MouseEvent += Instance_MouseEvent;
            }
        }

        void Instance_MouseEvent()
        {
            Close();
        }

        /// <summary>
        /// Handle the Key Event
        /// </summary>
        /// <param name="name"></param>
        void Instance_KeyPress(string name)
        {
            Debug.WriteLine("KeyPress " + name);
            if (name == "ClientInfo")
            {
                // Toggle
                if (_clientInfoForm.Visible)
                {
                    _clientInfoForm.Hide();
#if !DEBUG
                    if (!_screenSaver)
                        TopMost = true;
#endif
                }
                else
                {
#if !DEBUG
                    if (!_screenSaver)
                        TopMost = false;
#endif
                    _clientInfoForm.Show();
                    _clientInfoForm.BringToFront();
                }
            }
            else if (name == "ScreenSaver")
            {
                Debug.WriteLine("Closing due to ScreenSaver key press");
                if (!_screenSaver)
                    return;

                Close();
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
                _schedule = new Schedule(ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.ScheduleFile, ref _cacheManager, ref _clientInfoForm);

                // Bind to the schedule change event - notifys of changes to the schedule
                _schedule.ScheduleChangeEvent += ScheduleChangeEvent;

                // Bind to the overlay change event
                _schedule.OverlayChangeEvent += ScheduleOverlayChangeEvent;

                // Initialize the other schedule components
                _schedule.InitializeComponents();

                // Set this form to topmost
#if !DEBUG
                if (!_screenSaver)
                    TopMost = true;
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message, LogType.Error.ToString());
                MessageBox.Show("Fatal Error initialising the application. " + ex.Message, "Fatal Error");
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
            // Is the mouse enabled?
            if (!ApplicationSettings.Default.EnableMouse)
                // Hide the cursor
                Cursor.Hide();

            // Move the cursor to the starting place
            if (!_screenSaver)
                SetCursorStartPosition();

            // Show the splash screen
            ShowSplashScreen();

            // Change the default Proxy class
            OptionForm.SetGlobalProxy();

            // UserApp data
            Debug.WriteLine(new LogMessage("MainForm_Load", "User AppData Path: " + ApplicationSettings.Default.LibraryPath), LogType.Info.ToString());
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

            try
            {
                // Close the client info screen
                if (_clientInfoForm != null)
                    _clientInfoForm.Hide();

                // Stop the schedule object
                if (_schedule != null)
                    _schedule.Stop();

                // Flush the stats
                if (_statLog != null)
                    _statLog.Flush();

                // Write the CacheManager to disk
                if (_cacheManager != null)
                    _cacheManager.WriteCacheManager();
            }
            catch (NullReferenceException)
            {
                // Stopped before we really started, nothing to do
            }

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
                using (FileStream fileStream = File.Open(ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.CacheManagerFile, FileMode.Open))
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
        void ScheduleChangeEvent(string layoutPath, int scheduleId, int layoutId)
        {
            Trace.WriteLine(new LogMessage("MainForm - ScheduleChangeEvent", string.Format("Schedule Changing to {0}", layoutPath)), LogType.Audit.ToString());

            // We are changing the layout
            _changingLayout = true;

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
            if (ApplicationSettings.Default.PreventSleep)
            {
                try
                {
                    SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
                }
                catch
                {
                    Trace.WriteLine(new LogMessage("MainForm - ChangeToNextLayout", "Unable to set Thread Execution state"), LogType.Info.ToString());
                }
            }

            try
            {
                // Destroy the Current Layout
                try
                {
                    DestroyLayout();
                }
                catch (Exception e)
                {
                    // Force collect all controls
                    foreach (System.Windows.Forms.Control control in Controls)
                    {
                        control.Dispose();
                        Controls.Remove(control);
                    }

                    Trace.WriteLine(new LogMessage("MainForm - ChangeToNextLayout", "Destroy Layout Failed. Exception raised was: " + e.Message), LogType.Info.ToString());
                    throw e;
                }

                // Prepare the next layout
                try
                {
                    PrepareLayout(layoutPath);

                    _clientInfoForm.CurrentLayoutId = layoutPath;
                    _schedule.CurrentLayoutId = _layoutId;
                }
                catch (DefaultLayoutException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    DestroyLayout();
                    Trace.WriteLine(new LogMessage("MainForm - ChangeToNextLayout", "Prepare Layout Failed. Exception raised was: " + e.Message), LogType.Info.ToString());
                    throw;
                }

                _clientInfoForm.ControlCount = Controls.Count;

                // Do we need to notify?
                try
                {
                    if (ApplicationSettings.Default.SendCurrentLayoutAsStatusUpdate)
                    {
                        using (xmds.xmds statusXmds = new xmds.xmds())
                        {
                            statusXmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds;
                            statusXmds.NotifyStatusAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, "{\"currentLayoutId\":" + _layoutId + "}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("MainForm - ChangeToNextLayout", "Notify Status Failed. Exception raised was: " + e.Message), LogType.Info.ToString());
                    throw;
                }
            }
            catch (Exception ex)
            {
                if (!(ex is DefaultLayoutException))
                    Trace.WriteLine(new LogMessage("MainForm - ChangeToNextLayout", "Layout Change to " + layoutPath + " failed. Exception raised was: " + ex.Message), LogType.Error.ToString());

                if (!_showingSplash)
                    ShowSplashScreen();
                
                // In 10 seconds fire the next layout
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                timer.Interval = 10000;
                timer.Tick += new EventHandler(splashScreenTimer_Tick);

                // Start the timer
                timer.Start();
            }

            // We have finished changing the layout
            _changingLayout = false;
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
            // Get this layouts XML
            XmlDocument layoutXml = new XmlDocument();
            DateTime layoutModifiedTime;

            // Default or not
            if (layoutPath == ApplicationSettings.Default.LibraryPath + @"\Default.xml" || String.IsNullOrEmpty(layoutPath))
            {
                throw new DefaultLayoutException();
            }
            else
            {
                // try to open the layout file
                try
                {
                    using (FileStream fs = File.Open(layoutPath, FileMode.Open, FileAccess.Read, FileShare.Write))
                    {
                        using (XmlReader reader = XmlReader.Create(fs))
                        {
                            layoutXml.Load(reader);

                            reader.Close();
                        }
                        fs.Close();
                    }
                }
                catch (IOException ioEx) 
                {
                    _cacheManager.Remove(layoutPath);
                    Trace.WriteLine(new LogMessage("MainForm - PrepareLayout", "IOException: " + ioEx.ToString()), LogType.Error.ToString());
                    throw;
                }

                layoutModifiedTime = File.GetLastWriteTime(layoutPath);
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
                    string bgFilePath = ApplicationSettings.Default.LibraryPath + @"\backgrounds\" + backgroundWidth + "x" + backgroundHeight + "_" + layoutAttributes["background"].Value;

                    // Create a correctly sized background image in the temp folder
                    if (!File.Exists(bgFilePath))
                        GenerateBackgroundImage(layoutAttributes["background"].Value, backgroundWidth, backgroundHeight, bgFilePath);

                    BackgroundImage = new Bitmap(bgFilePath);
                    options.backgroundImage = @"/backgrounds/" + backgroundWidth + "x" + backgroundHeight + "_" + layoutAttributes["background"].Value; ;
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
                        string.Format(string.Format("An empty layout detected, will show for {0} seconds.", ApplicationSettings.Default.EmptyLayoutDuration.ToString()))), LogType.Info.ToString());

                    // Put a small dummy region in place, with a small dummy media node - which expires in 10 seconds.
                    XmlDocument dummyXml = new XmlDocument();
                    dummyXml.LoadXml(string.Format("<region id='blah' width='1' height='1' top='1' left='1'><media id='blah' type='text' duration='{0}'><raw><text></text></raw></media></region>",
                        ApplicationSettings.Default.EmptyLayoutDuration.ToString()));

                    // Replace the list of regions (they mean nothing as they are empty)
                    listRegions = dummyXml.SelectNodes("/region");
                }
            }

            // Create a start record for this layout
            _stat = new Stat();
            _stat.type = StatType.Layout;
            _stat.scheduleID = _scheduleId;
            _stat.layoutID = _layoutId;
            _stat.fromDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (XmlNode region in listRegions)
            {
                // Is there any media
                if (region.ChildNodes.Count == 0)
                {
                    Debug.WriteLine("A region with no media detected");
                    continue;
                }

                // Region loop setting
                options.RegionLoop = false;

                XmlNode regionOptionsNode = region.SelectSingleNode("options");

                if (regionOptionsNode != null)
                {
                    foreach (XmlNode option in regionOptionsNode.ChildNodes)
                    {
                        if (option.Name == "loop" && option.InnerText == "1")
                            options.RegionLoop = true;
                    }
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
                temp.BorderStyle = _borderStyle;

                Debug.WriteLine("Created new region", "MainForm - Prepare Layout");

                // Dont be fooled, this innocent little statement kicks everything off
                temp.regionOptions = options;

                _regions.Add(temp);
                Controls.Add(temp);

                Debug.WriteLine("Adding region", "MainForm - Prepare Layout");
            }

            // We have loaded a layout and therefore are no longer showing the splash screen
            _showingSplash = false;

            // Null stuff
            listRegions = null;
            listMedia = null;

            // Bring overlays to the front
            foreach (Region region in _overlays)
            {
                region.BringToFront();
            }
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

            using (Image img = Image.FromFile(ApplicationSettings.Default.LibraryPath + @"\" + sourceFile))
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
            _showingSplash = true;

            if (!string.IsNullOrEmpty(ApplicationSettings.Default.SplashOverride))
            {
                try
                {
                    using (Image bgSplash = Image.FromFile(ApplicationSettings.Default.SplashOverride))
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
            Trace.WriteLine(new LogMessage("MainForm - DurationElapsedEvent", "Region Elapsed"), LogType.Audit.ToString());

            // Are we already changing the layout?
            if (_changingLayout)
            {
                Trace.WriteLine(new LogMessage("MainForm - DurationElapsedEvent", "Already Changing Layout"), LogType.Audit.ToString());
                return;
            }

            bool isExpired = true;
            
            // Check the other regions to see if they are also expired.
            foreach (Region temp in _regions)
            {
                if (!temp.hasExpired())
                {
                    isExpired = false;
                    break;
                }
            }

            // If we are sure we have expired after checking all regions, then set the layout expired flag on them all
            if (isExpired)
            {
                // Inform each region that the layout containing it has expired
                foreach (Region temp in _regions)
                {
                    temp.setLayoutExpired();
                }

                Trace.WriteLine(new LogMessage("MainForm - DurationElapsedEvent", "All Regions have expired. Raising a Next layout event."), LogType.Audit.ToString());

                // We are changing the layout
                _changingLayout = true;
                
                // Yield and restart
                _schedule.NextLayout();
            }
        }

        /// <summary>
        /// Disposes Layout - removes the controls
        /// </summary>
        private void DestroyLayout() 
        {
            Debug.WriteLine("Destroying Layout", "MainForm - DestoryLayout");

            if (_regions == null) 
                return;

            lock (_regions)
            {
                foreach (Region region in _regions)
                {
                    try
                    {
                        // Remove the region from the list of controls
                        Controls.Remove(region);

                        // Clear the region
                        region.Clear();
                        
                        Trace.WriteLine(new LogMessage("MainForm - DestoryLayout", "Calling Dispose on Region " + region.regionOptions.regionId), LogType.Audit.ToString());
                        region.Dispose();
                    }
                    catch (Exception e)
                    {
                        // If we can't dispose we should log to understand why
                        Trace.WriteLine(new LogMessage("MainForm - DestoryLayout", e.Message), LogType.Info.ToString());
                    }
                }

                _regions.Clear();
            }

            _regions = null;
        }

        /// <summary>
        /// Set the Cursor start position
        /// </summary>
        private void SetCursorStartPosition()
        {
            Point position;

            switch (ApplicationSettings.Default.CursorStartPosition)
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

        /// <summary>
        /// Overlay change event.
        /// </summary>
        /// <param name="overlays"></param>
        void ScheduleOverlayChangeEvent(Collection<ScheduleItem> overlays)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new ManageOverlaysDelegate(ManageOverlays), overlays);
                return;
            }

            ManageOverlays(overlays);
        }

        /// <summary>
        /// Manage Overlays
        /// </summary>
        /// <param name="overlays"></param>
        public void ManageOverlays(Collection<ScheduleItem> overlays)
        {
            try
            {
                // Parse all overlays and compare what we have now to the overlays we have already created (see OverlayRegions)

                // Take the ones we currently have up and remove them if they aren't in the new list
                // We use a for loop so that we are able to remove the region from the collection
                for (int i = 0; i < _overlays.Count; i++)
                {
                    Region region = _overlays[i];
                    bool found = false;

                    foreach (ScheduleItem item in overlays)
                    {
                        if (item.scheduleid == region.scheduleId && _cacheManager.GetMD5(item.id + ".xlf") == region.hash)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        Debug.WriteLine("Removing overlay which is no-longer required. Overlay: " + region.scheduleId, "Overlays");
                        region.Clear();
                        region.Dispose();
                        Controls.Remove(region);
                        _overlays.Remove(region);
                    }
                }

                // Take the ones that are in the new list and add them
                foreach (ScheduleItem item in overlays)
                {
                    // Check its not already added.
                    bool found = false;
                    foreach (Region region in _overlays)
                    {
                        if (region.scheduleId == item.scheduleid)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found)
                        continue;

                    // Parse the layout for regions, and create them.
                    string layoutPath = item.layoutFile;

                    // Get this layouts XML
                    XmlDocument layoutXml = new XmlDocument();

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
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(new LogMessage("MainForm - _schedule_OverlayChangeEvent", string.Format("Could not find the layout file {0}: {1}", layoutPath, ex.Message)), LogType.Info.ToString());
                        continue;
                    }

                    // Attributes of the main layout node
                    XmlNode layoutNode = layoutXml.SelectSingleNode("/layout");

                    XmlAttributeCollection layoutAttributes = layoutNode.Attributes;

                    // Set the background and size of the form
                    double layoutWidth = int.Parse(layoutAttributes["width"].Value, CultureInfo.InvariantCulture);
                    double layoutHeight = int.Parse(layoutAttributes["height"].Value, CultureInfo.InvariantCulture);

                    // Scaling factor, will be applied to all regions
                    double scaleFactor = Math.Min(_clientSize.Width / layoutWidth, _clientSize.Height / layoutHeight);

                    // Want to be able to center this shiv - therefore work out which one of these is going to have left overs
                    int backgroundWidth = (int)(layoutWidth * scaleFactor);
                    int backgroundHeight = (int)(layoutHeight * scaleFactor);

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
                    RegionOptions options = new RegionOptions();

                    // Get the regions
                    XmlNodeList listRegions = layoutXml.SelectNodes("/layout/region");

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

                        options.scheduleId = item.scheduleid;
                        options.layoutId = item.id;
                        options.regionId = nodeAttibutes["id"].Value.ToString();
                        options.width = (int)(Convert.ToDouble(nodeAttibutes["width"].Value, CultureInfo.InvariantCulture) * scaleFactor);
                        options.height = (int)(Convert.ToDouble(nodeAttibutes["height"].Value, CultureInfo.InvariantCulture) * scaleFactor);
                        options.left = (int)(Convert.ToDouble(nodeAttibutes["left"].Value, CultureInfo.InvariantCulture) * scaleFactor);
                        options.top = (int)(Convert.ToDouble(nodeAttibutes["top"].Value, CultureInfo.InvariantCulture) * scaleFactor);
                        options.scaleFactor = scaleFactor;

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
                        temp.scheduleId = item.scheduleid;
                        temp.hash = _cacheManager.GetMD5(item.id + ".xlf");
                        temp.BorderStyle = _borderStyle;

                        // Dont be fooled, this innocent little statement kicks everything off
                        temp.regionOptions = options;

                        _overlays.Add(temp);
                        Controls.Add(temp);
                        temp.BringToFront();
                    }

                    // Null stuff
                    listRegions = null;
                }

                _clientInfoForm.ControlCount = Controls.Count;
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("MainForm - _schedule_OverlayChangeEvent", "Unknown issue managing overlays. Ex = " + e.Message), LogType.Info.ToString());
            }
        }
    }
}