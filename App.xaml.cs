using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using XiboClient.Logic;

namespace XiboClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [STAThread]
        protected override void OnStartup(StartupEventArgs e)
        {
            NativeMethods.SetErrorMode(NativeMethods.SetErrorMode(0) |
                           ErrorModes.SEM_NOGPFAULTERRORBOX |
                           ErrorModes.SEM_FAILCRITICALERRORS |
                           ErrorModes.SEM_NOOPENFILEERRORBOX);


            // Ensure our process has the highest priority
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

#if !DEBUG
            // Catch unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
#endif

            // Add the Xibo Tracelistener
            Trace.Listeners.Add(new XiboTraceListener());

            try
            {
                // Check for any passed arguments
                if (e.Args.Length > 0)
                {
                    if (e.Args[0].ToString() == "o")
                    {
                        RunSettings();
                    }
                    else
                    {
                        switch (e.Args[0].ToLower().Trim().Substring(0, 2))
                        {
                            // Preview the screen saver
                            case "/p":
                                // args[1] is the handle to the preview window
                                RunClient(new IntPtr(long.Parse(e.Args[1])));
                                break;

                            // Show the screen saver
                            case "/s":
                                RunClient(true);
                                break;

                            // Configure the screesaver's settings
                            case "/c":
                                // Show the settings form
                                RunSettings();
                                break;

                            // Show the screen saver
                            default:
                                RunClient(true);
                                break;
                        }
                    }
                }
                else
                {
                    RunClient();
                }
            }
            catch (Exception ex)
            {
                HandleUnhandledException(ex);
            }

            // Always flush at the end
            Trace.WriteLine(new LogMessage("Main", "Application Finished"), LogType.Info.ToString());
            Trace.Flush();
        }

        private static void RunSettings()
        {
            // If we are showing the options form, enable visual styles
            OptionsForm windowMain = new OptionsForm();
            windowMain.ShowDialog();
        }

        /// <summary>
        /// Run the Player
        /// </summary>
        private static void RunClient()
        {
            RunClient(false);
        }

        /// <summary>
        /// Run the Player
        /// </summary>
        /// <param name="screenSaver"></param>
        private static void RunClient(bool screenSaver)
        {
            Trace.WriteLine(new LogMessage("Main", "Client Started"), LogType.Info.ToString());

            KeyInterceptor.SetHook();
            if (screenSaver)
            {
                MouseInterceptor.SetHook();
            }

            MainWindow windowMain = new MainWindow(screenSaver);
            windowMain.ShowDialog();

            KeyInterceptor.UnsetHook();
            if (screenSaver)
            {
                MouseInterceptor.UnsetHook();
            }
        }

        /// <summary>
        /// Run the Player
        /// </summary>
        /// <param name="previewWindow"></param>
        private static void RunClient(IntPtr previewWindow)
        {
            Trace.WriteLine(new LogMessage("Main", "Client Started"), LogType.Info.ToString());
        }

        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            HandleUnhandledException(e.Exception);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleUnhandledException(e.ExceptionObject);
        }

        static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleUnhandledException(e.Exception);
        }

        /// <summary>
        /// Event for unhandled exceptions
        /// </summary>
        /// <param name="o"></param>
        static void HandleUnhandledException(Object o)
        {
            Exception e = o as Exception;

            // What happens if we cannot start?
            Trace.WriteLine(new LogMessage("Main", "Unhandled Exception: " + e.Message), LogType.Error.ToString());
            Trace.WriteLine(new LogMessage("Main", "Stack Trace: " + e.StackTrace), LogType.Audit.ToString());

            try
            {
                string productName = ApplicationSettings.GetProductNameFromAssembly();

                // Also write to the event log
                try
                {
                    if (!EventLog.SourceExists(productName))
                    {
                        EventLog.CreateEventSource(productName, "Xibo");
                    }

                    EventLog.WriteEntry(productName, e.ToString(), EventLogEntryType.Error);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("Main", "Couldn't write to event log: " + ex.Message), LogType.Info.ToString());
                }

                Trace.Flush();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("Main", "Unable to write to event log " + ex.Message), LogType.Info.ToString());
            }

            // Exit the application and allow it to be restarted by the Watchdog.
            Environment.Exit(0);
        }

        [DllImport("User32.dll")]
        public static extern int ShowWindowAsync(IntPtr hWnd, int swCommand);

        internal static class NativeMethods
        {
            [DllImport("kernel32.dll")]
            internal static extern ErrorModes SetErrorMode(ErrorModes mode);
        }

        [Flags]
        internal enum ErrorModes : uint
        {
            SYSTEM_DEFAULT = 0x0,
            SEM_FAILCRITICALERRORS = 0x0001,
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            SEM_NOGPFAULTERRORBOX = 0x0002,
            SEM_NOOPENFILEERRORBOX = 0x8000
        }
    }
}
