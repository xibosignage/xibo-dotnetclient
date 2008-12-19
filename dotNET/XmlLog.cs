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
        /// Writes a message to the XML log
        /// </summary>
        /// <param name="message"></param>
        /// <param name="cat"></param>
        public static void Append(String message, Catagory cat, int scheduleID, int layoutID, string mediaID)
        {
            if (!Properties.Settings.Default.auditEnabled && cat == Catagory.Audit) return;
            if (cat == Catagory.Stat) return; //We dont want to send stats without a type

            try
            {
                XmlTextWriter xw = new XmlTextWriter(File.Open(Application.UserAppDataPath + "//" + Properties.Settings.Default.logLocation, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);

                xw.WriteStartElement(cat.ToString());
                xw.WriteElementString("date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                xw.WriteElementString("message", message);
                xw.WriteElementString("scheduleID", scheduleID.ToString());
                xw.WriteElementString("layoutID", layoutID.ToString());
                if (mediaID != "0") xw.WriteElementString("mediaID", mediaID.ToString());
                xw.WriteEndElement();

                xw.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "XmlLog - Append");
            }

            // Test the size of the XML file
            FileInfo fileInfo = new FileInfo(Application.UserAppDataPath + @"/" + Properties.Settings.Default.logLocation);

            // If its greater than a certain size - send it to the WebService
            if (fileInfo.Length > 6000)
            {
                XmlLog log = new XmlLog(Application.UserAppDataPath + @"/" + Properties.Settings.Default.logLocation);
                log.PrepareAndSend();
            }
        }

        public static void Append(String message, Catagory cat)
        {
            if (!Properties.Settings.Default.auditEnabled && cat == Catagory.Audit) return;
            if (cat == Catagory.Stat) return; //We dont want to send stats without a type

            try
            {
                XmlTextWriter xw = new XmlTextWriter(File.Open(Application.UserAppDataPath + "//" + Properties.Settings.Default.logLocation, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);

                xw.WriteStartElement(cat.ToString());
                xw.WriteElementString("date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                xw.WriteElementString("message", message);
                xw.WriteEndElement();

                xw.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "XmlLog - Append");
            }

            // Test the size of the XML file
            FileInfo fileInfo = new FileInfo(Application.UserAppDataPath + @"/" + Properties.Settings.Default.logLocation);

            // If its greater than a certain size - send it to the WebService
            if (fileInfo.Length > 6000)
            {
                XmlLog log = new XmlLog(Application.UserAppDataPath + @"/" + Properties.Settings.Default.logLocation);
                log.PrepareAndSend();
            }
        }

        public static void AppendStat(String message, Catagory cat, StatType type, int scheduleID, int layoutID, string mediaID)
        {
            if (cat != Catagory.Stat) return;
            if (!Properties.Settings.Default.statsEnabled) return;

            try
            {
                XmlTextWriter xw = new XmlTextWriter(File.Open(Application.UserAppDataPath + "//" + Properties.Settings.Default.logLocation, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);

                xw.WriteStartElement(cat.ToString());
                
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
                System.Diagnostics.Debug.WriteLine(ex.Message, "XmlLog - Append");
            }

            // Test the size of the XML file
            FileInfo fileInfo = new FileInfo(Application.UserAppDataPath + @"/" + Properties.Settings.Default.logLocation);

            // If its greater than a certain size - send it to the WebService
            if (fileInfo.Length > 6000)
            {
                XmlLog log = new XmlLog(Application.UserAppDataPath + @"/" + Properties.Settings.Default.logLocation);
                log.PrepareAndSend();

                log.xmds1.Dispose();
            }
        }

        /// <summary>
        /// Creates a new XmlLog Class used for sending the log
        /// </summary>
        /// <param name="logPath"></param>
        public XmlLog(string logPath)
        {
            this.logPath = logPath;
            this.logPathTemp = String.Format("{0}[{1}]", logPath, DateTime.Now.ToFileTime().ToString());

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
            // Rename the Log (to prevent sending the same log file again, before this one is sent)
            try
            {
                File.Move(logPath, logPathTemp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "XmlLog - Prepare and Send");
                System.Diagnostics.Debug.WriteLine("Unable to move log file", "XmlLog - Prepare and Send");
            }

            currentFile = 0;

            // Get a list of all the log files avaiable to process
            DirectoryInfo di = new DirectoryInfo(Application.UserAppDataPath);

            files = di.GetFiles("log.xml*");

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

        private XiboClient.xmds.xmds xmds1;
        private HardwareKey hardwareKey;
        string logPath;
        string logPathTemp;

        FileInfo[] files;
        int currentFile;
    }

    public enum Catagory { Audit, Error, Stat }
    public enum StatType { LayoutStart, LayoutEnd, MediaStart, MediaEnd };
}
