/*
 * Xibo - Digital Signage - http://www.xibo.org.uk
 * Copyright (C) 2020 Xibo Signage Ltd
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
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using XiboClient.Log;
using XiboClient.Logic;

namespace XiboClient.XmdsAgents
{
    class RegisterAgent
    {
        public static object _locker = new object();

        // Members to stop the thread
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        // Events
        public delegate void OnXmrReconfigureDelegate();
        public event OnXmrReconfigureDelegate OnXmrReconfigure;

        public delegate void OnRegisterCompleteDelegate(bool error);
        public event OnRegisterCompleteDelegate OnRegisterComplete;

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
            Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Thread Started"), LogType.Info.ToString());

            int retryAfterSeconds = 0;

            while (!_forceStop)
            {
                lock (_locker)
                {
                    try
                    {
                        // If we are restarting, reset
                        _manualReset.Reset();

                        // Reset backOff
                        retryAfterSeconds = 0;

                        HardwareKey key = new HardwareKey();

                        Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Thread Woken and Lock Obtained"), LogType.Info.ToString());

                        using (xmds.xmds xmds = new xmds.xmds())
                        {
                            xmds.Credentials = null;
                            xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=registerDisplay";
                            xmds.UseDefaultCredentials = false;

                            // Store the XMR address
                            string xmrAddress = ApplicationSettings.Default.XmrNetworkAddress;

                            RegisterAgent.ProcessRegisterXml(callRegister(xmds, key));

                            // If we have screenshot requested set, then take and send
                            if (ApplicationSettings.Default.ScreenShotRequested)
                            {
                                // Take a screenshot
                                ScreenShot.TakeAndSend();

                                // Send status notification
                                ClientInfo.Instance.NotifyStatusToXmds();
                            }

                            // Set the flag to indicate we have a connection to XMDS
                            ApplicationSettings.Default.XmdsLastConnection = DateTime.Now;

                            // Has the XMR address changed?
                            if (xmrAddress != ApplicationSettings.Default.XmrNetworkAddress)
                            {
                                OnXmrReconfigure();
                            }

                            // Notify Status
                            NotifyStatus();

                            // Have we been asked to move CMS instance?
                            // CMS MOVE
                            // --------
                            if (!string.IsNullOrEmpty(ApplicationSettings.Default.NewCmsAddress)
                                && !string.IsNullOrEmpty(ApplicationSettings.Default.NewCmsKey)
                                && ApplicationSettings.Default.NewCmsAddress != ApplicationSettings.Default.ServerUri
                                )
                            {
                                // Make a call using the new details, and see if it works.
                                string oldUri = ApplicationSettings.Default.ServerUri;
                                string oldKey = ApplicationSettings.Default.ServerKey;
                                ApplicationSettings.Default.ServerUri = ApplicationSettings.Default.NewCmsAddress;
                                ApplicationSettings.Default.ServerKey = ApplicationSettings.Default.NewCmsKey;

                                Trace.WriteLine(new LogMessage("RegisterAgent - Run", "We have been asked to move to a new CMS. " + ApplicationSettings.Default.NewCmsAddress), LogType.Info.ToString());

                                // Try it and see.
                                try
                                {
                                    xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=registerDisplay";
                                    string xml = callRegister(xmds, key);

                                    // If that worked (no errors), update our settings
                                    ApplicationSettings.Default.NewCmsAddress = "";
                                    ApplicationSettings.Default.NewCmsKey = "";
                                    // ServerUri/Key will be updated too.
                                    ApplicationSettings.Default.Save();

                                    ProcessRegisterXml(xml);
                                }
                                catch (Exception e)
                                {
                                    Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Error swapping to new CMS. E = " + e.Message.ToString()), LogType.Error.ToString());

                                    // Switch back to the old values for subsequent tries
                                    ApplicationSettings.Default.ServerUri = oldUri;
                                    ApplicationSettings.Default.ServerKey = oldKey;
                                }
                            }

                            // Have we been asked to switch to HTTPS?
                            // HTTPS MOVE
                            // ----------
                            if (ApplicationSettings.Default.ForceHttps && xmds.Url.ToLowerInvariant().StartsWith("http://"))
                            {
                                Trace.WriteLine(new LogMessage("RegisterAgent - Run", "We have been asked to move to HTTPS from our current HTTP."), LogType.Info.ToString());

                                // Try it and see.
                                try
                                {
                                    string url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=registerDisplay";
                                    xmds.Url = url.Replace("http://", "https://");
                                    callRegister(xmds, key);

                                    // If that worked (no errors), update our setting
                                    ApplicationSettings.Default.ServerUri = ApplicationSettings.Default.ServerUri.Replace("http://", "https://");
                                    ApplicationSettings.Default.Save();
                                }
                                catch (Exception e)
                                {
                                    Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Error swapping to HTTPS. E = " + e.Message.ToString()), LogType.Error.ToString());
                                }
                            }
                        }

                        // Complete
                        OnRegisterComplete?.Invoke(false);
                    }
                    catch (WebException webEx) when (webEx.Response is HttpWebResponse httpWebResponse && (int)httpWebResponse.StatusCode == 429)
                    {
                        // Get the header for how long we ought to wait
                        retryAfterSeconds = webEx.Response.Headers["Retry-After"] != null ? int.Parse(webEx.Response.Headers["Retry-After"]) : 120;

                        // Log it.
                        Trace.WriteLine(new LogMessage("LogAgent", "Run: 429 received, waiting for " + retryAfterSeconds + " seconds."), LogType.Info.ToString());

                        // Complete, failed
                        OnRegisterComplete?.Invoke(true);
                    }
                    catch (WebException webEx)
                    {
                        // Increment the quantity of XMDS failures and bail out
                        ApplicationSettings.Default.IncrementXmdsErrorCount();

                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("RegisterAgent - Run", "WebException in Run: " + webEx.Message), LogType.Info.ToString());

                        // Complete, failed
                        OnRegisterComplete?.Invoke(true);
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Exception in Run: " + ex.Message), LogType.Info.ToString());

                        // Complete, failed
                        OnRegisterComplete?.Invoke(true);
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

            Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Thread Stopped"), LogType.Info.ToString());
        }

        private string callRegister(xmds.xmds xmds, HardwareKey key)
        {
            return xmds.RegisterDisplay(
                ApplicationSettings.Default.ServerKey,
                key.Key,
                ApplicationSettings.Default.DisplayName,
                "windows",
                ApplicationSettings.Default.ClientVersion,
                ApplicationSettings.Default.ClientCodeVersion,
                Environment.OSVersion.ToString(),
                key.MacAddress,
                key.Channel,
                key.getXmrPublicKey(),
                string.Empty
            );
        }

        public static string ProcessRegisterXml(string xml)
        {
            string message = "";

            try
            {
                // Load the result into an XML document
                XmlDocument result = new XmlDocument();
                result.LoadXml(xml);

                // Test the XML
                if (result.DocumentElement.Attributes["code"].Value == "READY")
                {
                    // Get the config element
                    if (result.DocumentElement.ChildNodes.Count <= 0)
                        throw new Exception("Configuration not set for this display");

                    // Hash after removing the date
                    try
                    {
                        if (result.DocumentElement.Attributes["date"] != null)
                            result.DocumentElement.Attributes["date"].Value = "";

                        if (result.DocumentElement.Attributes["localDate"] != null)
                            result.DocumentElement.Attributes["localDate"].Value = "";

                        // Check for our CRC32 cache attributes
                        if (result.DocumentElement.Attributes["checkSchedule"] != null)
                        {
                            ApplicationSettings.Default.XmdsCheckSchedule = result.DocumentElement.Attributes["checkSchedule"].Value;
                        }

                        if (result.DocumentElement.Attributes["checkRf"] != null)
                        {
                            ApplicationSettings.Default.XmdsCheckRf = result.DocumentElement.Attributes["checkRf"].Value;
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("Can't read root attributes from Register XML");
                    }

                    string md5 = Hashes.MD5(result.OuterXml);

                    if (md5 == ApplicationSettings.Default.Hash)
                    {
                        return result.DocumentElement.Attributes["message"].Value;
                    }

                    // Populate the settings based on the XML we've received.
                    ApplicationSettings.Default.PopulateFromXml(result);

                    // Store the MD5 hash and the save
                    ApplicationSettings.Default.Hash = md5;
                    ApplicationSettings.Default.Save();
                }
                else
                {
                    message += result.DocumentElement.Attributes["message"].Value;
                }

                // Append the informational message with the message attribute.
                message = result.DocumentElement.Attributes["message"].Value + Environment.NewLine + message;
            }
            catch (Exception ex)
            {
                message += ex.Message;
            }

            return message;
        }

        /// <summary>
        /// Report the timezone to XMDS
        /// </summary>
        private void NotifyStatus()
        {
            StringBuilder sb = new StringBuilder();
            using (StringWriter sw = new StringWriter(sb))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.None;
                writer.WriteStartObject();
                writer.WritePropertyName("deviceName");
                writer.WriteValue(Environment.MachineName);
                writer.WritePropertyName("width");
                writer.WriteValue(ClientInfo.Instance.PlayerWidth);
                writer.WritePropertyName("height");
                writer.WriteValue(ClientInfo.Instance.PlayerHeight);

                // GeoLocation
                if (ClientInfo.Instance.CurrentGeoLocation != null && !ClientInfo.Instance.CurrentGeoLocation.IsUnknown)
                {
                    writer.WritePropertyName("latitude");
                    writer.WriteValue(ClientInfo.Instance.CurrentGeoLocation.Latitude);
                    writer.WritePropertyName("longitude");
                    writer.WriteValue(ClientInfo.Instance.CurrentGeoLocation.Longitude);
                }

                try
                {
                    // Use Drive Info
                    DriveInfo info = ClientInfo.Instance.GetDriveInfo();
                    if (info != null)
                    {
                        writer.WritePropertyName("availableSpace");
                        writer.WriteValue(info.TotalFreeSpace);
                        writer.WritePropertyName("totalSpace");
                        writer.WriteValue(info.TotalSize);
                    }
                }
                catch
                {
                    Trace.WriteLine(new LogMessage("NotifyStatus", "Unable to get drive info"), LogType.Info.ToString());
                }


                // Timezone
                // we only do the timezone if it is currently empty, otherwise we stick with whatever has been set.
                if (string.IsNullOrEmpty(ApplicationSettings.Default.DisplayTimeZone))
                {
                    writer.WritePropertyName("timeZone");
                    writer.WriteValue(WindowsToIana(TimeZone.CurrentTimeZone.StandardName));
                }

                // LAN IP address
                string ipAddress = HardwareKey.LocalIPAddress();
                if (!string.IsNullOrEmpty(ipAddress))
                {
                    writer.WritePropertyName("lanIpAddress");
                    writer.WriteValue(ipAddress);
                }

                // Finish
                writer.WriteEndObject();

                // Report
                using (xmds.xmds xmds = new xmds.xmds())
                {
                    xmds.Credentials = null;
                    xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=notifyStatus";
                    xmds.UseDefaultCredentials = false;
                    xmds.NotifyStatusAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, sb.ToString());
                }
            }

            sb.Clear();
        }

        /// <summary>
        /// Windows to IANA timezone mapping
        /// ref: http://stackoverflow.com/questions/17348807/how-to-translate-between-windows-and-iana-time-zones
        /// </summary>
        /// <param name="windowsZoneId"></param>
        /// <returns></returns>
        private string WindowsToIana(string windowsZoneId)
        {
            if (windowsZoneId.Equals("UTC", StringComparison.Ordinal))
                return "Etc/UTC";

            var source = NodaTime.TimeZones.TzdbDateTimeZoneSource.Default;
            string result;
            // If there's no such mapping, result will be null.
            source.WindowsMapping.PrimaryMapping.TryGetValue(windowsZoneId, out result);
            // Canonicalize
            if (result != null)
            {
                result = source.CanonicalIdMap[result];
            }
            return result;
        }
    }
}
