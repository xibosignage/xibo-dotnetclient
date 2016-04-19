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
using XiboClient.Logic;

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

            // Catch unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);

            // Always flush at the end
            Trace.WriteLine(new LogMessage("Main", "Application Finished"), LogType.Info.ToString());
            Trace.Flush();

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

            Environment.Exit(1);
        }

        [DllImport("User32.dll")]
        public static extern int ShowWindowAsync(IntPtr hWnd , int swCommand);
    }
}