/**
 * Copyright (C) 2023 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - https://xibosignage.com
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
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XiboClient.XmdsAgents
{
    class FaultsAgent
    {
        private static object _locker = new object();
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

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
        /// Run Thread
        /// </summary>
        public void Run()
        {
            Trace.WriteLine(new LogMessage("FaultsAgent - Run", "Thread Started"), LogType.Info.ToString());

            while (!_forceStop)
            {
                // If we are restarting, reset
                _manualReset.Reset();

                // Reset backOff
                int retryAfterSeconds = 0;
                lock (_locker)
                {
                    try
                    {
                        using (xmds.xmds xmds = new xmds.xmds())
                        {
                            xmds.Credentials = null;
                            xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=reportFaults";
                            xmds.UseDefaultCredentials = false;
                            xmds.ReportFaults(ApplicationSettings.Default.ServerKey, _hardwareKey, CacheManager.Instance.UnsafeListAsJsonString());
                        }
                    }
                    catch (WebException webEx) when (webEx.Response is HttpWebResponse httpWebResponse && (int)httpWebResponse.StatusCode == 429)
                    {
                        // Get the header for how long we ought to wait
                        retryAfterSeconds = webEx.Response.Headers["Retry-After"] != null ? int.Parse(webEx.Response.Headers["Retry-After"]) : 120;

                        // Log it.
                        Trace.WriteLine(new LogMessage("FaultsAgent", "Run: 429 received, waiting for " + retryAfterSeconds + " seconds."), LogType.Info.ToString());
                    }
                    catch (WebException webEx)
                    {
                        // Increment the quantity of XMDS failures and bail out
                        ApplicationSettings.Default.IncrementXmdsErrorCount();

                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("FaultsAgent - Run", "WebException in Run: " + webEx.Message), LogType.Info.ToString());
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("FaultsAgent - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                    }
                }

                if (retryAfterSeconds > 0)
                {
                    // Sleep this thread until we've fulfilled our try after
                    _manualReset.WaitOne(retryAfterSeconds * 1000);
                }
                else
                {
                    // Sleep this thread until the next collection interval
                    _manualReset.WaitOne((int)(ApplicationSettings.Default.CollectInterval * ApplicationSettings.Default.XmdsCollectionIntervalFactor() * 1000));
                }
            }

            Trace.WriteLine(new LogMessage("FaultsAgent - Run", "Thread Stopped"), LogType.Info.ToString());
        }
    }
}
