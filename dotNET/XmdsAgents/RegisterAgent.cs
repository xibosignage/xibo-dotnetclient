/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006 - 2014 Daniel Garner
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

namespace XiboClient.XmdsAgents
{
    class RegisterAgent
    {
        public static object _locker = new object();

        // Members to stop the thread
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

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
                            xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds;
                            xmds.UseDefaultCredentials = false;

                            RegisterAgent.ProcessRegisterXml(xmds.RegisterDisplay(ApplicationSettings.Default.ServerKey, key.Key, ApplicationSettings.Default.DisplayName, "windows", ApplicationSettings.Default.ClientVersion, ApplicationSettings.Default.ClientCodeVersion, Environment.OSVersion.ToString(), key.MacAddress));

                            // Set the flag to indicate we have a connection to XMDS
                            ApplicationSettings.Default.XmdsLastConnection = DateTime.Now;

                            // Do we need to send a screenshot?
                            if (ApplicationSettings.Default.ScreenShotRequested)
                                ScreenShot.TakeAndSend();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("RegisterAgent - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                    }
                }

                // Sleep this thread until the next collection interval
                _manualReset.WaitOne((int)ApplicationSettings.Default.CollectInterval * 1000);
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

                if (result.DocumentElement.Attributes["code"].Value == "READY")
                {
                    // Get the config element
                    if (result.DocumentElement.ChildNodes.Count <= 0)
                        throw new Exception("Configuration not set for this display");

                    foreach (XmlNode node in result.DocumentElement.ChildNodes)
                    {
                        Object value = node.InnerText;

                        switch (node.Attributes["type"].Value)
                        {
                            case "int":
                                value = Convert.ToInt32(value);
                                break;

                            case "double":
                                value = Convert.ToDecimal(value);
                                break;

                            case "string":
                            case "word":
                                value = node.InnerText;
                                break;

                            case "checkbox":
                                value = (node.InnerText == "0") ? false : true;
                                break;

                            default:
                                message += String.Format("Unable to set {0} with value {1}", node.Name, value) + Environment.NewLine;
                                continue;
                        }

                        // Match these to settings
                        try
                        {
                            ApplicationSettings.Default[node.Name] = Convert.ChangeType(value, ApplicationSettings.Default[node.Name].GetType());
                        }
                        catch
                        {
                            message += "Invalid Configuration Option from CMS [" + node.Name + "]" + Environment.NewLine;
                        }
                    }

                    ApplicationSettings.Default.Save();
                }
                else
                {
                    message += result.DocumentElement.Attributes["message"].Value;
                }

                if (string.IsNullOrEmpty(message))
                    message = result.DocumentElement.Attributes["message"].Value;
            }
            catch (Exception ex)
            {
                message += ex.Message;
            }

            return message;
        }
    }
}
