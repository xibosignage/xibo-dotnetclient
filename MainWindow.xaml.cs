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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Xml;
using System.Net;
using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using XiboClient.Log;
using System.Threading;
using XiboClient.Properties;
using System.Globalization;
using XiboClient.Logic;
using XiboClient.Error;
using System.Drawing.Imaging;
using System.Windows.Threading;
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
        private bool _showingSplash = false;

        /// <summary>
        /// Delegates to Invoke various actions after yielding 
        /// </summary>
        /// <param name="layoutPath"></param>
        private delegate void ChangeToNextLayoutDelegate(string layoutPath);
        private delegate void ManageOverlaysDelegate(Collection<ScheduleItem> overlays);

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

        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        #endregion

        /*public MainWindow(IntPtr previewHandle)
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
        }*/

        public MainWindow(bool screenSaver)
        {
            InitializeComponent();

            if (screenSaver)
            {
                InitializeScreenSaver(false);
            }

            InitializeXibo();
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeXibo();
        }

        private void InitializeXibo()
        {
            // Set the title
            Title = ApplicationSettings.GetProductNameFromAssembly();

            Thread.CurrentThread.Name = "UI Thread";

            // Check the directories exist
            if (!Directory.Exists(ApplicationSettings.Default.LibraryPath + @"\backgrounds\"))
            {
                // Will handle the create of everything here
                Directory.CreateDirectory(ApplicationSettings.Default.LibraryPath + @"\backgrounds");
            }

            // Default the XmdsConnection
            ApplicationSettings.Default.XmdsLastConnection = DateTime.MinValue;

            // Set the Main Window Size
            SetMainWindowSize();

            // Bind to the resize event
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            // Show in taskbar
            ShowInTaskbar = ApplicationSettings.Default.ShowInTaskbar;

            // Setup the proxy information
            OptionsForm.SetGlobalProxy();

            // Events
            Loaded += MainWindow_Loaded;
            Closing += MainForm_FormClosing;
            ContentRendered += MainForm_Shown;
            
            // Define the hotkey
            /*Keys key;
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
            KeyStore.Instance.KeyPress += Instance_KeyPress;*/

            // Trace listener for Client Info
            ClientInfoTraceListener clientInfoTraceListener = new ClientInfoTraceListener(ClientInfo.Instance);
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

            // An empty set of overlays
            _overlays = new Collection<Layout>();

            // Switch to TLS 2.1
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            Trace.WriteLine(new LogMessage("MainForm", "Player Initialised"), LogType.Info.ToString());
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
        /*void Instance_KeyPress(string name)
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
        }*/

        /// <summary>
        /// main window loding event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Is the mouse enabled?
            if (!ApplicationSettings.Default.EnableMouse)
            {
                // Hide the cursor
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

            // UserApp data
            Debug.WriteLine(new LogMessage("MainForm_Load", "User AppData Path: " + ApplicationSettings.Default.LibraryPath), LogType.Info.ToString());
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
                    TopMost = true;
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message, LogType.Error.ToString());
                MessageBox.Show("Fatal Error initialising the application. " + ex.Message, "Fatal Error");
                Close();
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
            {
                // Hide the cursor
                Mouse.OverrideCursor = Cursors.None;
            }

            // Move the cursor to the starting place
            if (!_screenSaver)
                SetCursorStartPosition();

            // Show the splash screen
            ShowSplashScreen();

            // Change the default Proxy class
            OptionsForm.SetGlobalProxy();

            // UserApp data
            Debug.WriteLine(new LogMessage("MainForm_Load", "User AppData Path: " + ApplicationSettings.Default.LibraryPath), LogType.Info.ToString());
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
                //if (_clientInfoForm != null)
                //    _clientInfoForm.Hide();

                // Stop the schedule object
                if (_schedule != null)
                    _schedule.Stop();

                // Flush the stats
                FlushStats();

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
                    currentLayout.Stop();

                    DestroyLayout();
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
                    _showingSplash = false;

                    // Add this Layout to our controls
                    this.Scene.Children.Add(currentLayout);

                    ClientInfo.Instance.CurrentLayoutId = layoutPath;
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
            Debug.WriteLine(new LogMessage("timer_Tick", "Loading next layout after splashscreen"));

            DispatcherTimer timer = (DispatcherTimer)sender;
            timer.Stop();

            _schedule.NextLayout();
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
            System.Windows.Controls.Image img = new System.Windows.Controls.Image()
            {
                Name = "Splash"
            };
            img.Source = new BitmapImage(path);
            this.Scene.Children.Add(img);
        }

        /// <summary>
        /// Disposes Layout - removes the controls
        /// </summary>
        private void DestroyLayout()
        {
            Debug.WriteLine("Destroying Layout", "MainForm - DestoryLayout");

            this.currentLayout.Remove();
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
        /// Force a flush of the stats log
        /// </summary>
        public void FlushStats()
        {
            try
            {
                StatLog.Instance.Flush();
            }
            catch
            {
                Trace.WriteLine(new LogMessage("MainForm - FlushStats", "Unable to Flush Stats"), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// Overlay change event.
        /// </summary>
        /// <param name="overlays"></param>
        void ScheduleOverlayChangeEvent(Collection<ScheduleItem> overlays)
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
        public void ManageOverlays(Collection<ScheduleItem> overlays)
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
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
            }

            Debug.WriteLine("SetMainWindowSize: Setting Size to " + Width + "x" + Height);

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
