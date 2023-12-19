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
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Swan;
using System.IO;
using XiboClient.Log;

namespace XiboClient.XmdsAgents
{
    internal class DataAgent
    {
        private static readonly object _locker = new object();
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        private Dictionary<int, WidgetData> _widgets;

        /// <summary>
        /// New HTTP file required
        /// </summary>
        /// <param name="fileId"></param>
        public delegate void OnNewHttpRequiredFileDelegate(int mediaId, double fileSize, string md5, string saveAs, string path);
        public event OnNewHttpRequiredFileDelegate OnNewHttpRequiredFile;

        /// <summary>
        /// Construct a new data agent
        /// </summary>
        public DataAgent()
        {
            _widgets = new Dictionary<int, WidgetData>();
        }

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
        /// Clear our list of data items
        /// </summary>
        public void Clear()
        {
            lock (_locker)
            {
                _widgets.Clear();
            }
        }

        /// <summary>
        /// Add a widget to be kept updated
        /// </summary>
        /// <param name="widgetId"></param>
        /// <param name="updateInterval"></param>
        public void AddWidget(int widgetId, int updateInterval)
        {
            lock (_locker)
            { 
                if (!_widgets.ContainsKey(widgetId))
                {
                    WidgetData widgetData = new WidgetData();
                    widgetData.WidgetId = widgetId;
                    widgetData.UpdateInterval = updateInterval;
                    _widgets.Add(widgetId, widgetData);
                }
            }
        }

        /// <summary>
        /// Force a widget to be updated forthwith
        /// </summary>
        /// <param name="widgetId"></param>
        public void ForceUpdateWidget(int widgetId)
        {
            lock (_locker)
            {
                if (_widgets.ContainsKey(widgetId))
                {
                    _widgets[widgetId].ForceUpdate = true;
                }
            }
        }

        /// <summary>
        /// Run Thread
        /// </summary>
        public void Run()
        {
            LogMessage.Info("DataAgent", "Run", "Thread Started");

            int retryAfterSeconds;

            while (!_forceStop)
            {
                // If we are restarting, reset
                _manualReset.Reset();

                // Reset backOff
                retryAfterSeconds = 0;

                lock (_locker)
                {
                    string dataFilesList = "";
                    try
                    {
                        foreach (WidgetData widget in _widgets.Values)
                        {
                            if (widget.ForceUpdate || !widget.IsUpToDate)
                            {
                                // Download using XMDS GetResource
                                using (xmds.xmds xmds = new xmds.xmds())
                                {
                                    xmds.Credentials = null;
                                    xmds.UseDefaultCredentials = true;

                                    xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=getData";
                                    string result = xmds.GetData(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, widget.WidgetId);

                                    // Write the result to disk
                                    using (FileStream fileStream = File.Open(widget.Path, FileMode.Create, FileAccess.Write, FileShare.Read))
                                    {
                                        using (StreamWriter sw = new StreamWriter(fileStream))
                                        {
                                            sw.Write(result);
                                            sw.Close();
                                        }
                                    }

                                    // Clear the force update flag if set.
                                    widget.UpdatedDt = DateTime.Now;
                                    widget.ForceUpdate = false;

                                    // Load the result into a JSON response.
                                    try
                                    {
                                        JObject json = JsonConvert.DeserializeObject<JObject>(result);
                                        if (json != null && json.ContainsKey("files"))
                                        {
                                            foreach (JObject file in json.GetValueOrDefault("files").Cast<JObject>())
                                            {
                                                // Make a new fileagent somehow, to download this file.
                                                OnNewHttpRequiredFile?.Invoke(
                                                    int.Parse(file.GetValue("id").ToString()),
                                                    double.Parse(file.GetValue("size").ToString()),
                                                    file.GetValue("md5").ToString(),
                                                    file.GetValue("saveAs").ToString(),
                                                    file.GetValue("path").ToString()
                                                );
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogMessage.Error("DataAgent", "Run", "Unable to parse JSON result. e = " + ex.Message);
                                    }
                                }
                            }

                            dataFilesList += widget.WidgetId + ", " + widget.UpdatedDt + Environment.NewLine;
                        }
                    }
                    catch (WebException webEx) when (webEx.Response is HttpWebResponse httpWebResponse && (int)httpWebResponse.StatusCode == 429)
                    {
                        // Get the header for how long we ought to wait
                        retryAfterSeconds = webEx.Response.Headers["Retry-After"] != null ? int.Parse(webEx.Response.Headers["Retry-After"]) : 120;

                        // Log it.
                        LogMessage.Info("DataAgent", "Run", "429 received, waiting for " + retryAfterSeconds + " seconds.");
                    }
                    catch (WebException webEx)
                    {
                        // Increment the quantity of XMDS failures and bail out
                        ApplicationSettings.Default.IncrementXmdsErrorCount();

                        // Log this message, but dont abort the thread
                        LogMessage.Info("DataAgent", "Run", "WebException: " + webEx.Message);
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        LogMessage.Error("DataAgent", "Run", "Exception: " + ex.Message);
                    }

                    ClientInfo.Instance.UpdateDataFiles(dataFilesList);
                }

                if (retryAfterSeconds > 0)
                {
                    // Sleep this thread until we've fulfilled our try after
                    _manualReset.WaitOne(retryAfterSeconds * 1000);
                }
                else
                {
                    // Sleep this thread until for 60 seconds
                    _manualReset.WaitOne(60 * 1000);
                }
            }

            LogMessage.Info("DataAgent", "Run", "Thread Stopped");
        }
    }
}
