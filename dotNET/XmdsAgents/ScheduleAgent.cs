/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006 - 2012 Daniel Garner
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
using System.Text;
using System.Threading;
using XiboClient.Properties;
using System.Diagnostics;
using XiboClient.Log;

/// 17/02/12 Dan Created
/// 20/02/12 Dan Added ClientInfo

namespace XiboClient.XmdsAgents
{
    class ScheduleAgent
    {
        public static object _locker = new object();

        // Members to stop the thread
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        /// <summary>
        /// Current Schedule Manager for this Xibo Client
        /// </summary>
        public ScheduleManager CurrentScheduleManager
        {
            set
            {
                _scheduleManager = value;
            }
        }
        private ScheduleManager _scheduleManager;

        /// <summary>
        /// Schedule File Location
        /// </summary>
        public string ScheduleLocation
        {
            set
            {
                _scheduleLocation = value;
            }
        }
        private string _scheduleLocation;

        /// <summary>
        /// Client Hardware key
        /// </summary>
        public string HardwareKey
        {
            set
            {
                _hardwareKey = value;
            }
        }
        private string _hardwareKey;

        /// <summary>
        /// Client Info Form
        /// </summary>
        public ClientInfo ClientInfoForm
        {
            set
            {
                _clientInfoForm = value;
            }
        }
        private ClientInfo _clientInfoForm;

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
            Trace.WriteLine(new LogMessage("ScheduleAgent - Run", "Thread Started"), LogType.Info.ToString());

            while (!_forceStop)
            {
                lock (_locker)
                {
                    try
                    {
                        // If we are restarting, reset
                        _manualReset.Reset();

                        Trace.WriteLine(new LogMessage("ScheduleAgent - Run", "Thread Woken and Lock Obtained"), LogType.Info.ToString());

                        _clientInfoForm.ScheduleStatus = "Running: Get Data from Xibo Server";

                        using (xmds.xmds xmds = new xmds.xmds())
                        {
                            xmds.Credentials = null;
                            xmds.Url = Properties.Settings.Default.XiboClient_xmds_xmds;
                            xmds.UseDefaultCredentials = false;

                            string scheduleXml = xmds.Schedule(Settings.Default.ServerKey, _hardwareKey, Settings.Default.Version);

                            // Set the flag to indicate we have a connection to XMDS
                            Settings.Default.XmdsLastConnection = DateTime.Now;

                            _clientInfoForm.ScheduleStatus = "Running: Data Received";

                            // Hash of the result
                            string md5NewSchedule = Hashes.MD5(scheduleXml);
                            string md5CurrentSchedule = Hashes.MD5(ScheduleManager.GetScheduleXmlString(_scheduleLocation));

                            // Compare the results of the HASH
                            if (md5CurrentSchedule != md5NewSchedule)
                            {
                                Trace.WriteLine(new LogMessage("Schedule Agent - Run", "Received new schedule"));

                                _clientInfoForm.ScheduleStatus = "Running: New Schedule Received";

                                // Write the result to the schedule xml location
                                ScheduleManager.WriteScheduleXmlToDisk(_scheduleLocation, scheduleXml);

                                // Indicate to the schedule manager that it should read the XML file
                                _scheduleManager.RefreshSchedule = true;
                            }

                            _clientInfoForm.ScheduleStatus = "Sleeping";
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("ScheduleAgent - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                        _clientInfoForm.ScheduleStatus = "Error. " + ex.Message;
                    }
                }

                // Sleep this thread until the next collection interval
                _manualReset.WaitOne((int)Settings.Default.collectInterval * 1000);
            }

            Trace.WriteLine(new LogMessage("ScheduleAgent - Run", "Thread Stopped"), LogType.Info.ToString());
        }
    }
}
