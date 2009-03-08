/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006,2007,2008 Daniel Garner and James Packer
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
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace XiboClient
{
    class XmlLog
    {
        /// <summary>
        /// Creates a new XmlLog Class used for sending the log
        /// </summary>
        /// <param name="logPath"></param>
        public XmlLog()
        {
            logPath = Application.UserAppDataPath + @"/" + Properties.Settings.Default.logLocation;
            
            // Get the key for this display
            hardwareKey = new HardwareKey();

            // Setup the WebService call
            xmds1 = new XiboClient.xmds.xmds();
            xmds1.RecieveXmlLogCompleted += new XiboClient.xmds.RecieveXmlLogCompletedEventHandler(xmds1_RecieveXmlLogCompleted);

            return;
        }

        /// <summary>
        /// Prepares the log for sending
        /// </summary>
        public void PrepareAndSend()
        {
            currentFile = 0;

            // Get a list of all the log files avaiable to process
            DirectoryInfo di = new DirectoryInfo(Application.UserAppDataPath);

            files = di.GetFiles("*.ready");

            // There thought never be no files
            if (files.Length == 0) return;

            // Send them (one by one)
            SendLog(files[currentFile].FullName);

            return;
        }

        /// <summary>
        /// Sends and XmlLog files that are ready to send
        /// Binds a xmds1.RecieveXmlLogCompleted event
        /// </summary>
        public void SendLog(string filePath)
        {
            // Read the Xml
            try
            {
                StreamReader tr = new StreamReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                xmds1.RecieveXmlLogAsync(Properties.Settings.Default.ServerKey, hardwareKey.Key, tr.ReadToEnd(), Properties.Settings.Default.Version);
                tr.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Completed sending the XML Log
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void xmds1_RecieveXmlLogCompleted(object sender, XiboClient.xmds.RecieveXmlLogCompletedEventArgs e)
        {
            // Delete the Log File if success
            if (e.Error != null)
            {
                System.Diagnostics.Debug.WriteLine(e.Error.Message);
            }
            else
            {
                if (e.Result)
                {
                    try
                    {
                        // Do the delete
                        File.Delete(files[currentFile].FullName);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }
            }

            // Process the next file in the que
            currentFile++;

            if (currentFile < files.Length)
            {
                SendLog(files[currentFile].FullName);
            }

            return;
        }

        /// <summary>
        /// Appends a Trace message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="cat"></param>
        /*public static void Append(String message, Catagory cat)
        {
            if (cat == Catagory.Stat) return; //We dont want to send stats without a type

            System.Diagnostics.Trace.WriteLine(message, cat.ToString());
        }*/

        /// <summary>
        /// Appends a Stats XML message to the current Log
        /// </summary>
        /// <param name="message"></param>
        /// <param name="cat"></param>
        /// <param name="type"></param>
        /// <param name="scheduleID"></param>
        /// <param name="layoutID"></param>
        /// <param name="mediaID"></param>
        public static void AppendStat(String message, StatType type, int scheduleID, int layoutID, string mediaID)
        {
            if (!Properties.Settings.Default.statsEnabled) return;

            try
            {
                XmlTextWriter xw = new XmlTextWriter(File.Open(Application.UserAppDataPath + "//" + Properties.Settings.Default.logLocation, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);

                xw.WriteStartElement("stat");
                
                xw.WriteElementString("date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                xw.WriteElementString("message", message);
                xw.WriteElementString("type", type.ToString());
                xw.WriteElementString("scheduleID", scheduleID.ToString());
                xw.WriteElementString("layoutID", layoutID.ToString());
                if (mediaID != "0") xw.WriteElementString("mediaID", mediaID.ToString());
                
                xw.WriteEndElement();

                xw.Close();
            }
            catch (Exception ex)
            {
            }
        }

        private XiboClient.xmds.xmds xmds1;
        private HardwareKey hardwareKey;
        string logPath;

        FileInfo[] files;
        int currentFile;
    }

    public enum StatType { LayoutStart, LayoutEnd, MediaStart, MediaEnd };
}
