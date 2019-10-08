/*
 * Xibo - Digital Signage - http://www.xibo.org.uk
 * Copyright (C) 2019 Xibo Signage Ltd
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
using System.Diagnostics;
using XiboClient.Log;
using System.Xml;
using XiboClient.Logic;
using System.Net;

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

            while (!_forceStop)
            {
                lock (_locker)
                {
                    try
                    {
                        // If we are restarting, reset
                        _manualReset.Reset();

                        HardwareKey key = new HardwareKey();

                        Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Thread Woken and Lock Obtained"), LogType.Info.ToString());

                        using (xmds.xmds xmds = new xmds.xmds())
                        {
                            xmds.Credentials = null;
                            xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=registerDisplay";
                            xmds.UseDefaultCredentials = false;

                            // Store the XMR address
                            string xmrAddress = ApplicationSettings.Default.XmrNetworkAddress;

                            RegisterAgent.ProcessRegisterXml(xmds.RegisterDisplay(
                                ApplicationSettings.Default.ServerKey, 
                                key.Key, 
                                ApplicationSettings.Default.DisplayName, 
                                "windows", 
                                ApplicationSettings.Default.ClientVersion, 
                                ApplicationSettings.Default.ClientCodeVersion, 
                                Environment.OSVersion.ToString(), 
                                key.MacAddress,
                                key.Channel,
                                key.getXmrPublicKey()));

                            // Set the flag to indicate we have a connection to XMDS
                            ApplicationSettings.Default.XmdsLastConnection = DateTime.Now;

                            // Has the XMR address changed?
                            if (xmrAddress != ApplicationSettings.Default.XmrNetworkAddress)
                            {
                                OnXmrReconfigure();
                            }

                            // Is the timezone empty?
                            if (string.IsNullOrEmpty(ApplicationSettings.Default.DisplayTimeZone))
                            {
                                reportTimezone();
                            }

                            // Have we been asked to move CMS instance?

                            // Have we been asked to switch to HTTPS?
                            if (ApplicationSettings.Default.ForceHttps && xmds.Url.ToLowerInvariant().StartsWith("http://"))
                            {
                                Trace.WriteLine(new LogMessage("RegisterAgent - Run", "We have been asked to move to HTTPS from our current HTTP."), LogType.Info.ToString());

                                // Try it and see.
                                try
                                {
                                    xmds.Url = xmds.Url.Replace("http", "https");
                                    xmds.RegisterDisplay(
                                        ApplicationSettings.Default.ServerKey,
                                        key.Key,
                                        ApplicationSettings.Default.DisplayName,
                                        "windows",
                                        ApplicationSettings.Default.ClientVersion,
                                        ApplicationSettings.Default.ClientCodeVersion,
                                        Environment.OSVersion.ToString(),
                                        key.MacAddress,
                                        key.Channel,
                                        key.getXmrPublicKey());

                                    // If that worked (no errors), update our setting
                                    ApplicationSettings.Default.ServerUri = ApplicationSettings.Default.ServerUri.Replace("http", "https");
                                    ApplicationSettings.Default.Save();
                                }
                                catch
                                {
                                    Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Error swapping to HTTPS"), LogType.Error.ToString());
                                }
                            }
                        }
                    }
                    catch (WebException webEx)
                    {
                        // Increment the quantity of XMDS failures and bail out
                        ApplicationSettings.Default.IncrementXmdsErrorCount();

                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("RegisterAgent - Run", "WebException in Run: " + webEx.Message), LogType.Info.ToString());
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Exception in Run: " + ex.Message), LogType.Info.ToString());
                    }
                }

                // Sleep this thread until the next collection interval
                _manualReset.WaitOne((int)(ApplicationSettings.Default.CollectInterval * ApplicationSettings.Default.XmdsCollectionIntervalFactor() * 1000));
            }

            Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Thread Stopped"), LogType.Info.ToString());
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
                    }
                    catch
                    {
                        // No date, no need to remove
                    }

                    string md5 = Hashes.MD5(result.OuterXml);

                    if (md5 == ApplicationSettings.Default.Hash)
                        return result.DocumentElement.Attributes["message"].Value;

                    // Populate the settings based on the XML we've received.
                    ApplicationSettings.Default.PopulateFromXml(result);

                    // Store the MD5 hash and the save
                    ApplicationSettings.Default.Hash = md5;
                    ApplicationSettings.Default.Save();

                    // If we have screenshot requested set, then take and send
                    // we don't have a client info form here, so we can't send that data.
                    if (ApplicationSettings.Default.ScreenShotRequested)
                    {
                        ScreenShot.TakeAndSend();
                    }
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
        private void reportTimezone()
        {
            using (xmds.xmds xmds = new xmds.xmds())
            {
                string status = "{\"timeZone\":\"" + WindowsToIana(TimeZone.CurrentTimeZone.StandardName) + "\"}";

                xmds.Credentials = null;
                xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=notifyStatus";
                xmds.UseDefaultCredentials = false;
                xmds.NotifyStatusAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, status);
            }
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
