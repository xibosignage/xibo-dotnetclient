/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2012 Daniel Garner and James Packer
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

// 17/08/2012 Dan Set process priority to RealTime
// 21/08/2012 Dan Only enable visual styles for Options Form

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

            // Ensure our process has the highest priority
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

            Application.SetCompatibleTextRenderingDefault(false);

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
                                RunClient(new IntPtr(long.Parse(args[1])));
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
                    // No arguments were passed - we run the usual client
                    RunClient();
                }
            }
            catch (Exception ex)
            {
                HandleUnhandledException(ex);
            }

            // Catch unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);

            // Always flush at the end
            Trace.WriteLine(new LogMessage("Main", "Application Finished"), LogType.Info.ToString());
            Trace.Flush();

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
            HandleUnhandledException(e);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleUnhandledException(e);
        }

        static void HandleUnhandledException(Object o)
        {
            Exception e = o as Exception;

            // What happens if we cannot start?
            Trace.WriteLine(new LogMessage("Main", "Unhandled Exception: " + e.Message), LogType.Error.ToString());
            Trace.WriteLine(new LogMessage("Main", "Stack Trace: " + e.StackTrace), LogType.Error.ToString());

            // TODO: Can we just restart the application?

            // Shutdown the application
            CefRuntime.Shutdown();
            Environment.Exit(1);
        }

        [DllImport("User32.dll")]
        public static extern int ShowWindowAsync(IntPtr hWnd , int swCommand);
    }
}