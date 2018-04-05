/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006 - 2016 Daniel Garner
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace XiboClient.XmdsAgents
{
    class LogAgent
    {
        public static object _locker = new object();

        // Members to stop the thread
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        /// <summary>
        /// Wake Up
        /// </summary>
        public void WakeUp()
        {
            _manualReset.Set();
        }

        /// <summary>
        /// Stops the thread
        /// </summary>
        public void Stop()
        {
            _forceStop = true;
            _manualReset.Set();
        }

        /// <summary>
        /// Runs the agent
        /// </summary>
        public void Run()
        {
            Trace.WriteLine(new LogMessage("LogAgent - Run", "Thread Started"), LogType.Info.ToString());

            while (!_forceStop)
            {
                lock (_locker)
                {
                    try
                    {
                        // If we are restarting, reset
                        _manualReset.Reset();

                        HardwareKey key = new HardwareKey();

                        Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Thread Woken and Lock Obtained"), LogType.Audit.ToString());

                        using (xmds.xmds xmds = new xmds.xmds())
                        {
                            xmds.Credentials = null;
                            xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=submitLog";
                            xmds.UseDefaultCredentials = false;

                            // Log
                            ProcessFiles(xmds, key.Key, ApplicationSettings.Default.LogLocation);

                            xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=submitStats";

                            // Stat
                            ProcessFiles(xmds, key.Key, ApplicationSettings.Default.StatsLogFile);
                        }
                    }
                    catch (WebException webEx)
                    {
                        // Increment the quantity of XMDS failures and bail out
                        ApplicationSettings.Default.IncrementXmdsErrorCount();

                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("LogAgent - Run", "WebException in Run: " + webEx.Message), LogType.Info.ToString());
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("LogAgent - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                    }
                }

                // Sleep this thread until the next collection interval
                _manualReset.WaitOne((int)(ApplicationSettings.Default.CollectInterval * ApplicationSettings.Default.XmdsCollectionIntervalFactor() * 1000));
            }

            Trace.WriteLine(new LogMessage("LogAgent - Run", "Thread Stopped"), LogType.Info.ToString());
        }

        private void ProcessFiles(xmds.xmds xmds, string key, string type)
        {
            // Test for old files
            DateTime testDate = DateTime.Now.AddDays(ApplicationSettings.Default.LibraryAgentInterval * -1);
            
            // Track processed files
            int filesProcessed = 0;

            // Get a list of all the log files waiting to be sent to XMDS.
            DirectoryInfo directory = new DirectoryInfo(ApplicationSettings.Default.LibraryPath);

            // Loop through each file
            foreach (FileInfo fileInfo in directory.GetFiles("*" + type + "*"))
            {
                if (fileInfo.LastAccessTime < testDate)
                {
                    Trace.WriteLine(new LogMessage("LogAgent - Run", "Deleting old file: " + fileInfo.Name), LogType.Info.ToString());
                    File.Delete(fileInfo.FullName);
                    continue;
                }

                // Only process as many files in one go as configured
                if (filesProcessed >= ApplicationSettings.Default.MaxLogFileUploads)
                    break;

                // construct the log message
                StringBuilder builder = new StringBuilder();
                builder.Append("<log>");

                foreach (string entry in File.ReadAllLines(fileInfo.FullName))
                    builder.Append(entry);

                builder.Append("</log>");

                if (type == ApplicationSettings.Default.StatsLogFile)
                    xmds.SubmitStats(ApplicationSettings.Default.ServerKey, key, builder.ToString());
                else
                    xmds.SubmitLog(ApplicationSettings.Default.ServerKey, key, builder.ToString());

                // Delete the file we are on
                File.Delete(fileInfo.FullName);

                // Increment files processed
                filesProcessed++;
            }
        }
    }
}
