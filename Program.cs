/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2015 Daniel Garner and the Xibo Developers
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
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Xilium.CefGlue;
using XiboClient.Logic;
using System.Threading.Tasks;

namespace XiboClient
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            NativeMethods.SetErrorMode(NativeMethods.SetErrorMode(0) |
                           ErrorModes.SEM_NOGPFAULTERRORBOX |
                           ErrorModes.SEM_FAILCRITICALERRORS |
                           ErrorModes.SEM_NOOPENFILEERRORBOX);

            // Do we need to initialise CEF?
            if (ApplicationSettings.Default.UseCefWebBrowser)
            {
                try
                {
                    CefRuntime.Load();
                }
                catch (DllNotFoundException ex)
                {
                    MessageBox.Show(ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                catch (CefRuntimeException ex)
                {
                    MessageBox.Show(ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 2;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 3;
                }

                var settings = new CefSettings();
                settings.MultiThreadedMessageLoop = true;
                settings.SingleProcess = false;
                settings.LogSeverity = CefLogSeverity.Disable;
                settings.LogFile = "cef.log";
                settings.ResourcesDirPath = System.IO.Path.GetDirectoryName(new Uri(System.Reflection.Assembly.GetEntryAssembly().CodeBase).LocalPath);
                settings.RemoteDebuggingPort = 20480;

                CefRuntime.Initialize(new CefMainArgs(args), settings, null, IntPtr.Zero);
            }

            // Ensure our process has the highest priority
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

            Application.SetCompatibleTextRenderingDefault(false);

#if !DEBUG
            // Catch unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
#endif

            // Add the Xibo Tracelistener
            Trace.Listeners.Add(new XiboTraceListener());

            try
            {
                // Check for any passed arguments
                if (args.Length > 0)
                {
                    if (args[0].ToString() == "o")
                    {
                        RunSettings();
                    }
                    else
                    {
                        switch (args[0].ToLower().Trim().Substring(0, 2))
                        {
                            // Preview the screen saver
                            case "/p":
                                // args[1] is the handle to the preview window
                                KeyInterceptor.SetHook();
                                MouseInterceptor.SetHook();
                                RunClient(new IntPtr(long.Parse(args[1])));
                                KeyInterceptor.UnsetHook();
                                MouseInterceptor.UnsetHook();
                                break;

                            // Show the screen saver
                            case "/s":
                                KeyInterceptor.SetHook();
                                MouseInterceptor.SetHook();
                                RunClient(true);
                                KeyInterceptor.UnsetHook();
                                MouseInterceptor.UnsetHook();
                                break;

                            // Configure the screesaver's settings
                            case "/c":
                                // Show the settings form
                                RunSettings();
                                break;

                            // Show the screen saver
                            default:
                                KeyInterceptor.SetHook();
                                MouseInterceptor.SetHook();
                                RunClient(true);
                                KeyInterceptor.UnsetHook();
                                MouseInterceptor.UnsetHook();
                                break;
                        }
                    }
                }
                else
                {
                    // Add a message filter
                    Application.AddMessageFilter(KeyStore.Instance);

                    // No arguments were passed - we run the usual client
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

            if (ApplicationSettings.Default.UseCefWebBrowser)
                CefRuntime.Shutdown();

            return 0;
        }       

        private static void RunClient()
        {
            Trace.WriteLine(new LogMessage("Main", "Client Started"), LogType.Info.ToString());
            Application.Run(new MainForm());
        }

        private static void RunClient(bool screenSaver)
        {
            Trace.WriteLine(new LogMessage("Main", "Client Started"), LogType.Info.ToString());
            Application.Run(new MainForm(screenSaver));
        }

        private static void RunClient(IntPtr previewWindow)
        {
            Trace.WriteLine(new LogMessage("Main", "Client Started"), LogType.Info.ToString());
            Application.Run(new MainForm(previewWindow));
        }

        private static void RunSettings()
        {
            // If we are showing the options form, enable visual styles
            Application.EnableVisualStyles();

            Trace.WriteLine(new LogMessage("Main", "Options Started"), LogType.Info.ToString());
            Application.Run(new OptionForm());
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
            Trace.WriteLine(new LogMessage("Main", "Stack Trace: " + e.StackTrace), LogType.Error.ToString());

            try
            {
                // Also write to the event log
                if (!EventLog.SourceExists(Application.ProductName))
                    EventLog.CreateEventSource(Application.ProductName, "Xibo");

                EventLog.WriteEntry(Application.ProductName, e.ToString(), EventLogEntryType.Error);

                // Shutdown the application
                if (ApplicationSettings.Default.UseCefWebBrowser)
                    CefRuntime.Shutdown();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("Main", "Unable to write to event log " + ex.Message), LogType.Error.ToString());
            }

            // Try to restart
            Application.Restart();
        }

        [DllImport("User32.dll")]
        public static extern int ShowWindowAsync(IntPtr hWnd , int swCommand);

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