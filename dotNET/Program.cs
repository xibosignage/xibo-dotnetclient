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
        static void Main(string[] arg)
        {
            Process[] RunningProcesses = Process.GetProcessesByName("XiboClient");
         
            if(RunningProcesses.Length <= 1)
            {
                // Ensure our process has the highest priority
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

                Application.SetCompatibleTextRenderingDefault(false);

                Trace.Listeners.Add(new XiboTraceListener());
                Trace.AutoFlush = false;

                Form formMain;

                try
                {
                    if (arg.GetLength(0) > 0 && arg[0].ToString() == "o")
                    {
                        // If we are showing the options form, enable visual styles
                        Application.EnableVisualStyles();

                        Trace.WriteLine(new LogMessage("Main", "Options Started"), LogType.Info.ToString());
                        formMain = new OptionForm();
                    }
                    else
                    {
                        Trace.WriteLine(new LogMessage("Main", "Client Started"), LogType.Info.ToString());
                        formMain = new MainForm();
                    }
                    
                    Application.Run(formMain);
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
            }
            else
            {
                ShowWindowAsync(RunningProcesses[0].MainWindowHandle, 6);
                ShowWindowAsync(RunningProcesses[0].MainWindowHandle, 9);
            }
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
            Environment.Exit(1);
        }

        [DllImport("User32.dll")]
        public static extern int ShowWindowAsync(IntPtr hWnd , int swCommand);
    }
}