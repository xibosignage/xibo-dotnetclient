/**
 * Copyright (C) 2025 Xibo Signage Ltd
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XiboClient.Control;

namespace XiboClient.XmdsAgents
{
    internal class WeatherAgent
    {
        private static readonly object _locker = new object();
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        private bool _isWeatherRequired = false;

        public delegate void OnWeatherDelegate(List<CriteriaRequest> items);
        public event OnWeatherDelegate OnWeather;

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
        /// Enable?
        /// </summary>
        /// <param name="enable"></param>
        public void Enable(bool enable)
        {
            LogMessage.Audit("WeatherAgent", "Enable", enable ? "enabled" : "disabled");

            bool oldSet = _isWeatherRequired;
            _isWeatherRequired = enable;

            if (!oldSet && enable)
            {
                WakeUp();
            }
        }

        /// <summary>
        /// Run Thread
        /// </summary>
        public void Run()
        {
            LogMessage.Info("WeatherAgent", "Run", "Thread Started");

            int retryAfterSeconds;

            while (!_forceStop)
            {
                // If we are restarting, reset
                _manualReset.Reset();

                // Reset backOff
                retryAfterSeconds = 0;

                lock (_locker)
                {
                    if (_isWeatherRequired)
                    {
                        try
                        {
                            // Download using XMDS GetResource
                            using (xmds.xmds xmds = new xmds.xmds())
                            {
                                xmds.Credentials = null;
                                xmds.UseDefaultCredentials = true;

                                xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=getWeather";
                                string result = xmds.GetWeather(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey);

                                JObject weather = JsonConvert.DeserializeObject<JObject>(result);

                                // Build a list of criteria
                                var resolvedItems = new List<CriteriaRequest>();
                                foreach (var item in weather)
                                {
                                    resolvedItems.Add(new CriteriaRequest()
                                    {
                                        metric = item.Key,
                                        value = item.Value.ToString(),
                                        ttl = (ApplicationSettings.Default.CollectInterval + 60) // Add 60s to the collection interval so that we account for any delays getting the next update
                                    });
                                }

                                OnWeather?.Invoke(resolvedItems);
                            }
                        }
                        catch (WebException webEx) when (webEx.Response is HttpWebResponse httpWebResponse && (int)httpWebResponse.StatusCode == 429)
                        {
                            // Get the header for how long we ought to wait
                            retryAfterSeconds = webEx.Response.Headers["Retry-After"] != null ? int.Parse(webEx.Response.Headers["Retry-After"]) : 120;

                            // Log it.
                            LogMessage.Info("WeatherAgent", "Run", "429 received, waiting for " + retryAfterSeconds + " seconds.");
                        }
                        catch (WebException webEx)
                        {
                            // Increment the quantity of XMDS failures and bail out
                            ApplicationSettings.Default.IncrementXmdsErrorCount();

                            // Log this message, but dont abort the thread
                            LogMessage.Info("WeatherAgent", "Run", "WebException: " + webEx.Message);
                        }
                        catch (Exception ex)
                        {
                            // Log this message, but dont abort the thread
                            LogMessage.Error("WeatherAgent", "Run", "Exception: " + ex.Message);
                        }
                    }
                }

                if (retryAfterSeconds > 0)
                {
                    // Sleep this thread until we've fulfilled our try after
                    _manualReset.WaitOne(retryAfterSeconds * 1000);
                }
                else
                {
                    // Sleep this thread until the next collection
                    _manualReset.WaitOne(ApplicationSettings.Default.CollectInterval * 1000);
                }
            }

            LogMessage.Info("WeatherAgent", "Run", "Thread Stopped");
        }
    }
}
