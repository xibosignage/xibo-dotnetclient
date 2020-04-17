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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XiboClient.Error;
using XiboClient.Log;
using XiboClient.Logic;
using XiboClient.Rendering;
using XiboClient.Stats;

namespace XiboClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Schedule Class
        /// </summary>
        private Schedule _schedule;

        /// <summary>
        /// Overlay Regions
        /// </summary>
        private Collection<Layout> _overlays;

        /// <summary>
        /// The Currently Running Layout
        /// </summary>
        private Layout currentLayout;

        /// <summary>
        /// Some other misc state tracking things that need looking at
        /// </summary>
        private bool _changingLayout = false;
        private int _scheduleId;
        private int _layoutId;
        private bool _screenSaver = false;

        /// <summary>
        /// Splash Screen Logic
        /// </summary>
        private bool _showingSplash = false;
        private System.Windows.Controls.Image splashScreen;

        /// <summary>
        /// Delegates to Invoke various actions after yielding 
        /// </summary>
        /// <param name="layoutPath"></param>
        private delegate void ChangeToNextLayoutDelegate(string layoutPath);
        private delegate void ManageOverlaysDelegate(List<ScheduleItem> overlays);

        /// <summary>
        /// The InfoScreen
        /// </summary>
        private InfoScreen infoScreen;

        #region DLL Imports

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

        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        #endregion

        /// <summary>
        /// Initialise Player
        /// </summary>
        /// <param name="screenSaver"></param>
        public MainWindow(bool screenSaver)
        {
            // Set the Cache Manager
            CacheManager.Instance.SetCacheManager();

            InitializeComponent();

            if (screenSaver)
            {
                InitializeScreenSaver();
            }

            InitializeXibo();
        }

        /// <summary>
        /// Initialise Xibo
        /// </summary>
        private void InitializeXibo()
        {
            // Set the title
            Title = ApplicationSettings.GetProductNameFromAssembly();

            // Check the directories exist
            if (!Directory.Exists(ApplicationSettings.Default.LibraryPath + @"\backgrounds\"))
            {
                // Will handle the create of everything here
                Directory.CreateDirectory(ApplicationSettings.Default.LibraryPath + @"\backgrounds");
            }

            // Initialise the database
            StatManager.Instance.InitDatabase();

            // Default the XmdsConnection
            ApplicationSettings.Default.XmdsLastConnection = DateTime.MinValue;

            // Bind to the resize event
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            // Show in taskbar
            ShowInTaskbar = ApplicationSettings.Default.ShowInTaskbar;

            // Events
            Loaded += MainWindow_Loaded;
            Closing += MainForm_FormClosing;
            ContentRendered += MainForm_Shown;

            // Trace listener for Client Info
            ClientInfoTraceListener clientInfoTraceListener = new ClientInfoTraceListener
            {
                Name = "ClientInfo TraceListener"
            };
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
                    ClientInfo.Instance.UpdateStatusMarkerFile();

                    // Start watchdog
                    WatchDogManager.Start();
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("MainForm - InitializeXibo", "Cannot start watchdog. E = " + e.Message), LogType.Error.ToString());
                }
            }
#endif

            // An empty set of overlays
            _overlays = new Collection<Layout>();

            // Switch to TLS 2.1
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            Trace.WriteLine(new LogMessage("MainForm", "Player Initialised"), LogType.Info.ToString());
        }

        /// <summary>
        /// Initialise the Screen Saver
        /// </summary>
        private void InitializeScreenSaver()
        {
            _screenSaver = true;

            // Configure some listeners for the mouse (to quit)
            KeyStore.Instance.ScreenSaver = true;
            MouseInterceptor.Instance.MouseEvent += Instance_MouseEvent;
        }

        /// <summary>
        /// Handle Mouse Events
        /// </summary>
        private void Instance_MouseEvent()
        {
            System.Windows.Application.Current.Shutdown();
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
                if (this.infoScreen == null)
                {
#if !DEBUG
                    // Make our window not topmost so that we can see the info screen
                    if (!_screenSaver)
                    {
                        Topmost = false;
                    }
#endif

                    this.infoScreen = new InfoScreen();
                    this.infoScreen.Closed += InfoScreen_Closed;
                    this.infoScreen.Show();
                }
                else
                {
                    this.infoScreen.Close();

#if !DEBUG
                    // Bring the window back to Topmost if we need to
                    if (!_screenSaver)
                    {
                        Topmost = true;
                    }
#endif
                }
            }
            else if (name == "ScreenSaver")
            {
                Debug.WriteLine("Closing due to ScreenSaver key press");
                if (!_screenSaver)
                    return;

                System.Windows.Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// InfoScreen Closed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InfoScreen_Closed(object sender, EventArgs e)
        {
            this.infoScreen.Closed -= InfoScreen_Closed;
            this.infoScreen = null;
        }

        /// <summary>
        /// main window loding event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the Main Window Size
            SetMainWindowSize();

            // Is the mouse enabled?
            if (!ApplicationSettings.Default.EnableMouse)
            {
                // Hide the cursor
                Mouse.OverrideCursor = System.Windows.Input.Cursors.None;
            }

            // Move the cursor to the starting place
            if (!_screenSaver)
            {
                SetCursorStartPosition();
            }

            // Show the splash screen
            ShowSplashScreen();

            // Change the default Proxy class
            OptionsForm.SetGlobalProxy();

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

            // UserApp data
            Debug.WriteLine(new LogMessage("MainForm_Load", "User AppData Path: " + ApplicationSettings.Default.LibraryPath), LogType.Info.ToString());

            // Initialise CEF
            CefSharp.Wpf.CefSettings settings = new CefSharp.Wpf.CefSettings();
            settings.CachePath = ApplicationSettings.Default.LibraryPath + @"\CEF";
            settings.LogFile = ApplicationSettings.Default.LibraryPath + @"\CEF\cef.log";
            settings.LogSeverity = CefSharp.LogSeverity.Fatal;
            CefSharp.Cef.Initialize(settings);
        }

        /// <summary>
        /// Called after the form has been shown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MainForm_Shown(object sender, EventArgs e)
        {
            try
            {
                // Create the Schedule
                _schedule = new Schedule(ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.ScheduleFile);

                // Bind to the schedule change event - notifys of changes to the schedule
                _schedule.ScheduleChangeEvent += ScheduleChangeEvent;

                // Bind to the overlay change event
                _schedule.OverlayChangeEvent += ScheduleOverlayChangeEvent;

                // Initialize the other schedule components
                _schedule.InitializeComponents();

                // Set this form to topmost
#if !DEBUG
                if (!_screenSaver)
                    Topmost = true;
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message, LogType.Error.ToString());
                System.Windows.MessageBox.Show("Fatal Error initialising the application. " + ex.Message, "Fatal Error");
                Close();
            }
        }

        /// <summary>
        /// Called as the Main Form starts to close
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, CancelEventArgs e)
        {
            // We want to tidy up some stuff as this form closes.
            Trace.Listeners.Remove("ClientInfo TraceListener");

            try
            {
                // Close the client info screen
                if (this.infoScreen != null)
                {
                    this.infoScreen.Close();
                }

                // Stop the schedule object
                if (_schedule != null)
                    _schedule.Stop();

                // Write the CacheManager to disk
                CacheManager.Instance.WriteCacheManager();
            }
            catch (NullReferenceException)
            {
                // Stopped before we really started, nothing to do
            }

            // Flush the logs
            Trace.Flush();
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

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new ChangeToNextLayoutDelegate(ChangeToNextLayout), layoutPath);
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
                    if (this.currentLayout != null)
                    {
                        this.currentLayout.Stop();
                        DestroyLayout();
                    }
                }
                catch (Exception e)
                {
                    // Force collect all controls
                    this.Scene.Children.Clear();

                    Trace.WriteLine(new LogMessage("MainForm - ChangeToNextLayout", "Destroy Layout Failed. Exception raised was: " + e.Message), LogType.Info.ToString());
                    throw e;
                }

                // Prepare the next layout
                try
                {
                    currentLayout = PrepareLayout(layoutPath, false);

                    // We have loaded a layout background and therefore are no longer showing the splash screen
                    // Remove the Splash Screen Image
                    RemoveSplashScreen();

                    // Match Background Colors
                    this.Background = currentLayout.BackgroundColor;

                    // Add this Layout to our controls
                    this.Scene.Children.Add(currentLayout);

                    // Start
                    currentLayout.Start();

                    // Update client info
                    ClientInfo.Instance.CurrentLayoutId = _layoutId + "";
                    ClientInfo.Instance.CurrentlyPlaying = layoutPath;
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

                ClientInfo.Instance.ControlCount = this.Scene.Children.Count;

                // Do we need to notify?
                try
                {
                    if (ApplicationSettings.Default.SendCurrentLayoutAsStatusUpdate)
                    {
                        using (xmds.xmds statusXmds = new xmds.xmds())
                        {
                            statusXmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=notifyStatus";
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

                // Do we have more than one Layout in our schedule?
                if (_schedule.ActiveLayouts > 1)
                {
                    _schedule.NextLayout();
                }
                else
                {
                    if (!_showingSplash)
                    {
                        ShowSplashScreen();
                    }

                    // In 10 seconds fire the next layout
                    DispatcherTimer timer = new DispatcherTimer()
                    {
                        Interval = new TimeSpan(0, 0, 10)
                    };
                    timer.Tick += new EventHandler(splashScreenTimer_Tick);
                    timer.Start();
                }
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
            DispatcherTimer timer = (DispatcherTimer)sender;
            timer.Stop();

            if (_showingSplash)
            {
                Debug.WriteLine(new LogMessage("timer_Tick", "Loading next layout after splashscreen"));

                // Put the next Layout up
                _schedule.NextLayout();
            }
        }

        /// <summary>
        /// Prepares the Layout.. rendering all the necessary controls
        /// </summary>
        /// <param name="layoutPath"></param>
        /// <param name="isOverlay"></param>
        /// <returns></returns>
        private Layout PrepareLayout(string layoutPath, bool isOverlay)
        {
            // Default or not
            if (layoutPath == ApplicationSettings.Default.LibraryPath + @"\Default.xml" || String.IsNullOrEmpty(layoutPath))
            {
                throw new DefaultLayoutException();
            }
            else
            {
                try
                {
                    // Construct a new Current Layout
                    Layout layout = new Layout();
                    layout.Width = Width;
                    layout.Height = Height;
                    layout.Schedule = _schedule;
                    layout.loadFromFile(layoutPath, _layoutId, _scheduleId, isOverlay);
                    return layout;
                }
                catch (IOException)
                {
                    CacheManager.Instance.Remove(layoutPath);

                    throw new DefaultLayoutException();
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
                    System.Windows.Controls.Image img = new System.Windows.Controls.Image()
                    {
                        Name = "Splash"
                    };
                    img.Source = new BitmapImage(new Uri(ApplicationSettings.Default.SplashOverride));
                    this.Scene.Children.Add(img);
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
            Uri path = new Uri("pack://application:,,,/Resources/splash.jpg");
            this.splashScreen = new System.Windows.Controls.Image()
            {
                Name = "Splash",
                Source = new BitmapImage(path)
            };
            this.Scene.Children.Add(this.splashScreen);
        }

        /// <summary>
        /// Remove the Splash Screen
        /// </summary>
        private void RemoveSplashScreen()
        {
            if (this.splashScreen != null)
            {
                this.Scene.Children.Remove(this.splashScreen);
            }

            // We've removed it
            this._showingSplash = false;
        }

        /// <summary>
        /// Disposes Layout - removes the controls
        /// </summary>
        private void DestroyLayout()
        {
            Debug.WriteLine("Destroying Layout", "MainForm - DestoryLayout");

            this.currentLayout.Remove();

            this.Scene.Children.Remove(this.currentLayout);
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
                    position = new Point(Width, 0);
                    break;

                case "Bottom Left":
                    position = new Point(0, Height);
                    break;

                case "Bottom Right":
                    position = new Point(Width, Height);
                    break;

                default:
                    // The default position or "unchanged" as it will be sent, is to not do anything
                    // leave the cursor where it is
                    return;
            }

            SetCursorPos((int)position.X, (int)position.Y);
        }

        /// <summary>
        /// Overlay change event.
        /// </summary>
        /// <param name="overlays"></param>
        void ScheduleOverlayChangeEvent(List<ScheduleItem> overlays)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new ManageOverlaysDelegate(ManageOverlays), overlays);
                return;
            }

            ManageOverlays(overlays);
        }

        /// <summary>
        /// Manage Overlays
        /// </summary>
        /// <param name="overlays"></param>
        public void ManageOverlays(List<ScheduleItem> overlays)
        {
            try
            {
                // Parse all overlays and compare what we have now to the overlays we have already created (see OverlayRegions)
                Debug.WriteLine("Arrived at Manage Overlays with " + overlays.Count + " overlay schedules to show. We're already showing " + _overlays.Count + " overlay Regions", "Overlays");

                // Take the ones we currently have up and remove them if they aren't in the new list or if they've been set to refresh
                // We use a for loop so that we are able to remove the region from the collection
                for (int i = 0; i < _overlays.Count; i++)
                {
                    Debug.WriteLine("Assessing Overlay Region " + i, "Overlays");

                    Layout layout = _overlays[i];
                    bool found = false;
                    bool refresh = false;

                    foreach (ScheduleItem item in overlays)
                    {
                        if (item.scheduleid == layout.ScheduleId)
                        {
                            found = true;
                            refresh = item.Refresh;
                            break;
                        }
                    }

                    if (!found || refresh)
                    {
                        if (refresh)
                        {
                            Trace.WriteLine(new LogMessage("MainForm - ManageOverlays", "Refreshing item that has changed."), LogType.Info.ToString());
                        }
                        Debug.WriteLine("Removing overlay " + i + " which is no-longer required. Overlay: " + layout.ScheduleId, "Overlays");

                        // Remove the Layout from the overlays collection
                        _overlays.Remove(layout);

                        // As we've removed the thing we're iterating over, reduce i
                        i--;

                        // Clear down and dispose of the region.
                        layout.Stop();
                        layout.Remove();

                        this.OverlayScene.Children.Remove(layout);
                    }
                    else
                    {
                        Debug.WriteLine("Overlay Layout found and not needing refresh " + i, "Overlays");
                    }
                }

                // Take the ones that are in the new list and add them
                foreach (ScheduleItem item in overlays)
                {
                    // Check its not already added.
                    bool found = false;
                    foreach (Layout layout in _overlays)
                    {
                        if (layout.ScheduleId == item.scheduleid)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        Debug.WriteLine("Layout already found for overlay - we're assuming here that if we've found one, they are all there.", "Overlays");
                        continue;
                    }

                    // Reset refresh
                    item.Refresh = false;

                    // Parse the layout for regions, and create them.
                    try
                    {
                        Layout layout = PrepareLayout(item.layoutFile, true);

                        // Add to our collection of Overlays
                        _overlays.Add(layout);

                        // Add to the Scene
                        OverlayScene.Children.Add(layout);

                        // Start
                        layout.Start();
                    }
                    catch (DefaultLayoutException)
                    {
                        // Unable to prepare this layout - log and move on
                        Trace.WriteLine(new LogMessage("MainForm - ManageOverlays", "Unable to Prepare Layout: " + item.layoutFile), LogType.Audit.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("MainForm - _schedule_OverlayChangeEvent", "Unknown issue managing overlays. Ex = " + e.Message), LogType.Info.ToString());
            }
        }

        /// <summary>
        /// Set the Main Window Size, either to the primary monitor, or the configured size
        /// </summary>
        private void SetMainWindowSize()
        {
            Debug.WriteLine("SetMainWindowSize: IN");

            // Override the default size if necessary
            if (ApplicationSettings.Default.SizeX != 0 || ApplicationSettings.Default.SizeY != 0)
            {
                Debug.WriteLine("SetMainWindowSize: Use Settings Size");

                // Determine the client size
                double sizeX = (double)ApplicationSettings.Default.SizeX;
                if (sizeX <= 0)
                {
                    sizeX = SystemParameters.PrimaryScreenWidth;
                }

                double sizeY = (int)ApplicationSettings.Default.SizeY;
                if (sizeY <= 0)
                {
                    sizeY = SystemParameters.PrimaryScreenHeight;
                }

                Width = sizeX;
                Height = sizeY;
                Top = (double)ApplicationSettings.Default.OffsetX;
                Left = (double)ApplicationSettings.Default.OffsetY;
            }
            else
            {
                Debug.WriteLine("SetMainWindowSize: Use Monitor Size");

                // Use the primary monitor size
                Top = 0;
                Left = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
            }

            Debug.WriteLine("SetMainWindowSize: Setting Size to " + Width + "x" + Height);

            // Store this in Client Info
            ClientInfo.Instance.PlayerWidth = (int)Width;
            ClientInfo.Instance.PlayerHeight = (int)Height;

            // Use the client size we've calculated to set the actual size of the form
            WindowState = WindowState.Normal;

            Debug.WriteLine("SetMainWindowSize: OUT");
        }

        /// <summary>
        /// Display Settings Changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            Trace.WriteLine(new LogMessage("SystemEvents_DisplaySettingsChanged",
                "Display Settings have changed, resizing the Player window and moving on to the next Layout. W="
                + SystemParameters.PrimaryScreenWidth.ToString() + ", H="
                + SystemParameters.PrimaryScreenHeight.ToString()), LogType.Info.ToString());

            // Reassert the size of our client (should resize if necessary)
            SetMainWindowSize();

            // Expire the current layout and move on
            _changingLayout = true;

            // Yield and restart
            _schedule.NextLayout();
        }
    }
}
