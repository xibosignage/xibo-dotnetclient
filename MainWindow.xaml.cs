/**
 * Copyright (C) 2023 Xibo Signage Ltd
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
using XiboClient.Action;
using XiboClient.Adspace;
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
        /// Schedule Change Lock
        /// </summary>
        public static object _scheduleLocker = new object();

        /// <summary>
        /// Overlay Regions
        /// </summary>
        private Collection<Layout> _overlays;

        /// <summary>
        /// The Currently Running Layout
        /// </summary>
        private Layout currentLayout;

        /// <summary>
        /// Are we in screensaver mode?
        /// </summary>
        private bool _screenSaver = false;

        /// <summary>
        /// Splash Screen Logic
        /// </summary>
        private bool _showingSplash = false;
        private System.Windows.Controls.Image splashScreen;

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
            MouseInterceptor.Instance.MouseClickEvent += MouseInterceptor_MouseClickEvent;

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
                    XiboClient.Control.WatchDogManager.Start();
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

            // Initialise the database
            StatManager.Instance.InitDatabase();

            Trace.WriteLine(new LogMessage("MainForm", "Player Initialised"), LogType.Info.ToString());
        }

        /// <summary>
        /// Initialise the Screen Saver
        /// </summary>
        private void InitializeScreenSaver()
        {
            _screenSaver = true;

            // Indicate to the KeyStore that we are a scrensaver
            KeyStore.Instance.ScreenSaver = true;

            // Mouse Move
            MouseInterceptor.Instance.MouseMoveEvent += MouseInterceptor_MouseMoveEvent;
        }

        /// <summary>
        /// Mouse Move Event
        /// </summary>
        private void MouseInterceptor_MouseMoveEvent()
        {
            if (_screenSaver)
            {
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Shutdown();
                }
            }
        }

        /// <summary>
        /// Mouse Click Event
        /// </summary>
        /// <param name="point"></param>
        private void MouseInterceptor_MouseClickEvent(System.Drawing.Point point)
        {
            if (_screenSaver)
            {
                System.Windows.Application.Current.Shutdown();
            }
            else if (!(point.X < Left || point.X > Width + Left || point.Y < Top || point.Y > Height + Top))
            {
                Debug.WriteLine("Inside Player: " + point.X + "," + point.Y 
                    + ". Player: " + Left + "," + Top + ". " + Width + "x" + Height, "MouseInterceptor_MouseClickEvent");

                // Rebase to Player dimensions and pass to Handle
                HandleActionTrigger("touch", "", 0, new Point
                {
                    X = point.X - Left,
                    Y = point.Y - Top
                });
            }
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
            ShowSplashScreen(0);

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
            CefSharp.CefSharpSettings.SubprocessExitIfParentProcessClosed = true;

            // Settings for Init
            CefSharp.Wpf.CefSettings settings = new CefSharp.Wpf.CefSettings
            {
                RootCachePath = ApplicationSettings.Default.LibraryPath + @"\CEF",
                CachePath = ApplicationSettings.Default.LibraryPath + @"\CEF",
                LogFile = ApplicationSettings.Default.LibraryPath + @"\CEF\cef.log",
                LogSeverity = CefSharp.LogSeverity.Fatal,
            };
            settings.CefCommandLineArgs["autoplay-policy"] = "no-user-gesture-required";
            settings.CefCommandLineArgs["disable-pinch"] = "1";
            settings.CefCommandLineArgs["disable-usb-keyboard-detect"] = "1";

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

                // Bind to the trigger received event
                _schedule.OnTriggerReceived += HandleActionTrigger;

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
                LogMessage.Error("MainForm", "MainForm_Shown", "Cannot initialise the application, unexpected exception." + ex.Message);
                LogMessage.Error("MainForm", "MainForm_Shown", ex.StackTrace.ToString());
                
                System.Windows.MessageBox.Show("Fatal Error initialising the application. " + ex.Message + ", " + ex.StackTrace.ToString(), "Fatal Error");
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
                {
                    _schedule.Stop();
                    _schedule.OnTriggerReceived -= HandleActionTrigger;
                }

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
        /// <param name="nextLayout"></param>
        void ScheduleChangeEvent(ScheduleItem nextLayout)
        {
            // We can only process 1 schedule change at a time.
            lock (_scheduleLocker)
            {
                Trace.WriteLine(new LogMessage("MainForm",
                        string.Format("ScheduleChangeEvent: Schedule Changing to Schedule {0}, Layout {1}", nextLayout.scheduleid, nextLayout.id)), LogType.Audit.ToString());

                // Issue a change to the next Layout
                Dispatcher.Invoke(new Action<ScheduleItem>(ChangeToNextLayout), nextLayout);
            }
        }

        /// <summary>
        /// Change to the next layout
        /// <param name="scheduleItem"></param>
        /// </summary>
        private void ChangeToNextLayout(ScheduleItem scheduleItem)
        {
            Debug.WriteLine("ChangeToNextLayout: called", "MainWindow");

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
                // Stop the Current Layout
                try
                {
                    if (this.currentLayout != null)
                    {
                        // Check to see if this Layout was a Layout Change Action that we can mark as being played
                        if (this.currentLayout.ScheduleItem.Override)
                        {
                            if (_schedule.NotifyLayoutActionFinished(this.currentLayout.ScheduleItem))
                            {
                                Debug.WriteLine("ChangeToNextLayout: not changing this time, because the current layout finishing will result in a schedule change.", "MainWindow");
                                return;
                            }
                        }

                        Debug.WriteLine("ChangeToNextLayout: stopping the current Layout", "MainWindow");

                        this.currentLayout.Stop();

                        Debug.WriteLine("ChangeToNextLayout: stopped and removed the current Layout: " + this.currentLayout.UniqueId, "MainWindow");

                        this.currentLayout = null;
                    }
                }
                catch (Exception e)
                {
                    // Force collect all controls
                    this.Scene.Children.Clear();

                    Trace.WriteLine(new LogMessage("MainForm", "ChangeToNextLayout: Destroy Layout Failed. Exception raised was: " + e.Message), LogType.Info.ToString());
                }

                // Prepare the next layout
                try
                {
                    this.currentLayout = PrepareLayout(scheduleItem);

                    // We have loaded a layout background and therefore are no longer showing the splash screen
                    // Remove the Splash Screen Image
                    RemoveSplashScreen();

                    // Start the Layout.
                    StartLayout(this.currentLayout);
                }
                catch (ShowSplashScreenException)
                {
                    // Pass straight out to show the splash screen
                    throw;
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("MainForm", "ChangeToNextLayout: Prepare/Start Layout Failed. Exception raised was: " + e.Message), LogType.Info.ToString());

                    // Remove the Layout again
                    if (this.currentLayout != null)
                    {
                        DestroyLayout(this.currentLayout);
                    }

                    // Pass out
                    throw;
                }
            }
            catch (ShowSplashScreenException)
            {
                // Specifically asked to show the splash screen.
                if (!_showingSplash)
                {
                    ShowSplashScreen(10);
                }
            }
            catch (Exception ex)
            {
                // Store the active layout count, so that we can remove this one that failed and still see if there is another to try
                int activeLayouts = _schedule.ActiveLayouts;

                if (scheduleItem.IsAdspaceExchange)
                {
                    LogMessage.Audit("MainForm", "ChangeToNextLayout", "No ad to show, e: " + ex.Message);
                }
                else
                {
                    LogMessage.Info("MainForm", "ChangeToNextLayout", "Layout Change to " + scheduleItem.layoutFile + " failed. Exception raised was: " + ex.Message);

                    // We could not prepare or start this Layout, so we ought to remove it from the Schedule.
                    _schedule.RemoveLayout(scheduleItem);
                }

                // Do we have more than one Layout in our Schedule which we can try?
                // and make sure they aren't solely AXE
                if (activeLayouts > 1 && activeLayouts > _schedule.ActiveAdspaceExchangeEvents)
                {
                    _schedule.NextLayout();
                }
                else if (scheduleItem != _schedule.GetDefaultLayout() && !_schedule.GetDefaultLayout().IsSplash())
                {
                    // Can we show the default layout?
                    try 
                    {
                        currentLayout = PrepareLayout(_schedule.GetDefaultLayout());

                        // We have loaded a layout background and therefore are no longer showing the splash screen
                        // Remove the Splash Screen Image
                        RemoveSplashScreen();

                        // Start the Layout.
                        StartLayout(this.currentLayout);
                    }
                    catch
                    {
                        Trace.WriteLine(new LogMessage("MainForm", "ChangeToNextLayout: Failed to show the default layout. Exception raised was: " + ex.Message), LogType.Error.ToString());
                        ShowSplashScreen(10);
                    }
                }
                else
                {
                    ShowSplashScreen(10);
                }
            }
        }

        /// <summary>
        /// Start a Layout
        /// </summary>
        /// <param name="layout"></param>
        private void StartLayout(Layout layout)
        {
            Debug.WriteLine("StartLayout: Starting...", "MainWindow");

            // Bind to Layout finished
            layout.OnLayoutStopped += Layout_OnLayoutStopped;

            // Match Background Colors
            this.Background = layout.BackgroundColor;

            // Add this Layout to our controls
            this.Scene.Children.Add(layout);

            // Start
            if (!layout.IsRunning)
            {
                Debug.WriteLine("StartLayout: Starting Layout", "MainWindow");
                layout.Start();
            }
            else
            {
                Trace.WriteLine(new LogMessage("MainForm", "StartLayout: Layout already running."), LogType.Error.ToString());
                return;
            }

            Debug.WriteLine("StartLayout: Started Layout", "MainWindow");

            // Update client info
            ClientInfo.Instance.CurrentLayoutId = layout.ScheduleItem.id;
            ClientInfo.Instance.CurrentlyPlaying = layout.ScheduleItem.layoutFile;
            ClientInfo.Instance.ControlCount = this.Scene.Children.Count;

            // Do we need to notify?
            try
            {
                if (ApplicationSettings.Default.SendCurrentLayoutAsStatusUpdate)
                {
                    using (xmds.xmds statusXmds = new xmds.xmds())
                    {
                        statusXmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=notifyStatus";
                        statusXmds.NotifyStatusAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, "{\"currentLayoutId\":" + this.currentLayout.ScheduleItem.id + "}");
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("MainForm", "StartLayout: Notify Status Failed. Exception raised was: " + e.Message), LogType.Info.ToString());
                throw;
            }
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
        /// <param name="scheduleItem"></param>
        /// <returns></returns>
        private Layout PrepareLayout(ScheduleItem scheduleItem)
        {
            // Default or not
            if (scheduleItem.IsSplash() || string.IsNullOrEmpty(scheduleItem.layoutFile))
            {
                throw new ShowSplashScreenException();
            }
            else if (CacheManager.Instance.IsUnsafeLayout(scheduleItem.id))
            {
                throw new LayoutInvalidException("Unsafe Layout");
            }
            else
            {
                try
                {
                    // Construct a new Current Layout
                    Layout layout = new Layout
                    {
                        Width = Width,
                        Height = Height,
                        Schedule = _schedule
                    };

                    // Is this an Adspace Exchange Layout?
                    if (scheduleItem.IsAdspaceExchange)
                    {
                        // Get an ad
                        Ad ad = _schedule.GetAd(Width, Height);
                        if (ad == null)
                        {
                            throw new LayoutInvalidException("No ad to play");
                        }

                        layout.LoadFromAd(scheduleItem, ad);
                    }
                    else
                    {
                        layout.LoadFromFile(scheduleItem);
                    }
                    return layout;
                }
                catch (IOException)
                {
                    if (!scheduleItem.IsAdspaceExchange)
                    {
                        CacheManager.Instance.Remove(scheduleItem.layoutFile);
                    }

                    throw new LayoutInvalidException("IO Exception");
                }
            }
        }

        /// <summary>
        /// Shows the splash screen (set the background to the embedded resource)
        /// <paramref name="timeout"/>
        /// </summary>
        private void ShowSplashScreen(int timeout)
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

            if (timeout > 0)
            {
                // In "timeout" seconds fire the next layout
                DispatcherTimer timer = new DispatcherTimer()
                {
                    Interval = new TimeSpan(0, 0, timeout)
                };
                timer.Tick += new EventHandler(splashScreenTimer_Tick);
                timer.Start();
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
        /// Event called when a Layout has been stopped
        /// </summary>
        private void Layout_OnLayoutStopped(Layout layout)
        {
            Debug.WriteLine("Layout_OnLayoutStopped: Layout completely stopped", "MainWindow");

            DestroyLayout(layout);
        }

        /// <summary>
        /// Disposes Layout - removes the controls
        /// </summary>
        private void DestroyLayout(Layout layout)
        {
            Debug.WriteLine("DestroyLayout: Destroying Layout", "MainWindow");

            layout.Remove();
            layout.OnLayoutStopped -= Layout_OnLayoutStopped;

            this.Scene.Children.Remove(layout);
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
            Dispatcher.BeginInvoke(new Action<List<ScheduleItem>>(ManageOverlays), overlays);
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
                        Layout layout = PrepareLayout(item);

                        // Add to our collection of Overlays
                        _overlays.Add(layout);

                        // Add to the Scene
                        OverlayScene.Children.Add(layout);

                        // Start
                        layout.Start();
                    }
                    catch (ShowSplashScreenException)
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
        /// Get actions
        /// </summary>
        /// <returns></returns>
        private List<Action.Action> GetActions()
        {
            List<Action.Action> actions = new List<Action.Action>();

            // Pull actions from the main layout and any overlays
            if (currentLayout != null)
            {
                actions.AddRange(currentLayout.GetActions());
            }

            // Add overlays
            if (_overlays != null)
            {
                foreach (Layout overlay in _overlays)
                {
                    actions.AddRange(overlay.GetActions());
                }
            }

            // Add the current schedule actions
            actions.AddRange(_schedule.GetActions());

            return actions;
        }

        /// <summary>
        /// Is the provided widgetId playing?
        /// </summary>
        /// <param name="sourceId"></param>
        /// <returns></returns>
        private bool IsWidgetIdPlaying(int sourceId)
        {
            if (currentLayout != null)
            {
                if (currentLayout.IsWidgetIdPlaying("" + sourceId))
                {
                    return true;
                }
            }

            foreach (Layout overlay in _overlays)
            {
                if (overlay.IsWidgetIdPlaying("" + sourceId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Is the provided widgetId playing?
        /// </summary>
        /// <param name="sourceId"></param>
        /// <returns></returns>
        private bool IsWidgetIdPlayingInRegion(Point point, int sourceId)
        {
            if (currentLayout != null)
            {
                if (currentLayout.GetCurrentWidgetIdForRegion(point).Contains("" + sourceId))
                {
                    return true;
                }
            }

            foreach (Layout overlay in _overlays)
            {
                if (overlay.GetCurrentWidgetIdForRegion(point).Contains("" + sourceId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Handle Action trigger from a Trigger
        /// </summary>
        /// <param name="triggerType"></param>
        /// <param name="triggerCode"></param>
        /// <param name="sourceId"></param>
        /// <param name="duration"></param>
        public void HandleActionTrigger(string triggerType, string triggerCode, int sourceId, int duration)
        {
            Debug.WriteLine("HandleActionTrigger: triggerType: " + triggerType + ", triggerCode: " + triggerCode, "MainForm");

            if (triggerType == "duration")
            {
                try
                {
                    ExecuteDurationTrigger(triggerCode, sourceId, duration);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("MainForm", "HandleActionTrigger: unable to execute duration trigger. e = " + e.Message), LogType.Error.ToString());
                }
            }
            else
            {
                HandleActionTrigger(triggerType, triggerCode, sourceId, new Point());
            }
        }

        /// <summary>
        /// Handle an incoming Action Trigger
        /// </summary>
        /// <param name="triggerType"></param>
        /// <param name="triggerCode"></param>
        /// <param name="sourceId"></param>
        /// <param name="point"></param>
        /// <param name="duration"></param>
        public void HandleActionTrigger(string triggerType, string triggerCode, int sourceId, Point point)
        {
            // Do we have any actions which match this trigger type?
            // These are in order, with Widgets first.
            foreach (Action.Action action in GetActions())
            {
                // Match the trigger type
                if (action.TriggerType != triggerType)
                {
                    continue;
                }

                // Match the sourceId if it has been provided
                if (sourceId != 0 && sourceId != action.SourceId)
                {
                    continue;
                }

                // Is this a trigger which must match the code?
                if (triggerType == "webhook" && !string.IsNullOrEmpty(action.TriggerCode) && action.TriggerCode != triggerCode)
                {
                    continue;
                }
                // Webhooks coming from a Widget must be active somewhere on the Layout
                else if (triggerType == "webhook" && action.Source == "widget" && !IsWidgetIdPlaying(action.SourceId))
                {
                    Debug.WriteLine(point.ToString() + " webhook matches widget which isn't playing: " + action.SourceId, "HandleActionTrigger");
                    continue;
                }
                // Does this action match the point provided?
                else if (triggerType == "touch" && !action.IsPointInside(point))
                {
                    Debug.WriteLine(point.ToString() + " not inside action: " + action.Rect.ToString(), "HandleActionTrigger");
                    continue;
                }
                // If the source of the action is a widget, it must currently be active.
                else if (triggerType == "touch" && action.Source == "widget" && !IsWidgetIdPlayingInRegion(point, action.SourceId))
                {
                    Debug.WriteLine(point.ToString() + " not active widget: " + action.SourceId, "HandleActionTrigger");
                    continue;
                }
                
                // Action found, so execute it
                try
                {
                    ExecuteAction(action);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("MainForm", "HandleActionTrigger: unable to execute action. e = " + e.Message), LogType.Error.ToString());
                }

                // Should we process further actions?
                if (!action.Bubble)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Execute the matched Action
        /// </summary>
        /// <param name="action"></param>
        public void ExecuteAction(Action.Action action)
        {
            // UI thread
            Dispatcher.Invoke(new System.Action(() =>
            {
                // Target can be `screen` or `region`
                // What type of action are we?
                switch (action.ActionType)
                {
                    case "next":
                        // Trigger the next layout/widget
                        if (action.Target == "screen")
                        {
                            // Next Layout
                            _schedule.NextLayout();
                        }
                        else
                        {
                            // Next Widget in the named region
                            currentLayout.RegionNext("" + action.TargetId);
                        }
                        break;

                    case "previous":
                        // Trigger the previous layout/widget
                        if (action.Target == "screen")
                        {
                            // Previous Layout
                            _schedule.PreviousLayout();
                        }
                        else
                        {
                            // Previous Widget in the named region
                            currentLayout.RegionPrevious("" + action.TargetId);
                        }
                        break;

                    case "navLayout":
                        // Navigate to the provided Layout
                        // target is always screen
                        Debug.WriteLine("MainWindow", "ExecuteAction: change to next layout with code " + action.LayoutCode);

                        ChangeToNextLayout(_schedule.GetScheduleItemForLayoutCode(action.LayoutCode));
                        break;

                    case "navWidget":
                        // Navigate to the provided Widget
                        // A widget action could come from a normal Layout or an overlay, which is it?
                        if (currentLayout.HasWidgetIdInDrawer(action.WidgetId))
                        {
                            if (action.Target == "screen")
                            {
                                // Expect a shell command.
                                currentLayout.ExecuteWidget(action.WidgetId);
                            }
                            else
                            {
                                // Provided Widget in the named region
                                currentLayout.RegionChangeToWidget(action.TargetId + "", action.WidgetId);
                            }
                        }
                        else
                        {
                            // Check in overlays
                            foreach (Layout overlay in _overlays)
                            {
                                if (overlay.HasWidgetIdInDrawer(action.WidgetId))
                                {
                                    if (action.Target == "screen")
                                    {
                                        // Expect a shell command.
                                        overlay.ExecuteWidget(action.WidgetId);
                                    }
                                    else
                                    {
                                        // Provided Widget in the named region
                                        overlay.RegionChangeToWidget(action.TargetId + "", action.WidgetId);
                                    }
                                }
                            }
                        }

                        break;

                    case "command":
                        // Run a command directly
                        if (action.Target == "screen")
                        {
                            // Expect a stored command.
                            try
                            {
                                Command command = Command.GetByCode(action.CommandCode);
                                command.Run();
                            }
                            catch (Exception e)
                            {
                                Trace.WriteLine(new LogMessage("MainWindow", "ExecuteAction: cannot run Command: " + e.Message), LogType.Error.ToString());
                            }
                        }
                        else
                        {
                            // Not supported
                            Trace.WriteLine(new LogMessage("MainWindow", "ExecuteAction: command actions must be targeted to the screen."), LogType.Audit.ToString());
                        }
                        break;

                    default:
                        Trace.WriteLine(new LogMessage("MainWindow", "ExecuteAction: unknown type: " + action.ActionType), LogType.Error.ToString());
                        break;
                }
            }));
        }

        /// <summary>
        /// Execute Duration Trigger
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="sourceId"></param>
        /// <param name="duration"></param>
        public void ExecuteDurationTrigger(string operation, int sourceId, int duration)
        {
            // UI thread
            Dispatcher.Invoke(new System.Action(() =>
            {
                try
                {
                    string regionId = currentLayout.GetRegionIdByActiveWidgetId("" + sourceId);
                    switch (operation)
                    {

                        case "expire":
                            // Next Widget in the named region
                            currentLayout.RegionNext(regionId);
                            break;

                        case "extend":
                            currentLayout.RegionExtend(regionId, duration);
                            break;

                        case "set":
                            currentLayout.RegionSetDuration(regionId, duration);
                            break;
                    }
                } 
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message, "ExecuteDurationTrigger");
                }
            }));
        }

        /// <summary>
        /// Set the Main Window Size, either to the primary monitor, or the configured size
        /// </summary>
        private void SetMainWindowSize()
        {
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
                Left = (double)ApplicationSettings.Default.OffsetX;
                Top = (double)ApplicationSettings.Default.OffsetY;
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

            Trace.WriteLine(
                new LogMessage("MainForm", string.Format("SetMainWindowSize: window set to {0},{1}-{2}x{3}", Top, Left, Width, Height))
                , LogType.Audit.ToString());
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

            // Yield and restart
            _schedule.NextLayout();
        }
    }
}
