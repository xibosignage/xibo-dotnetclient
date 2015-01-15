/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2009-2014 Spring Signage Ltd
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
using System.Collections.ObjectModel;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;
using System.Threading;

namespace XiboClient
{
    class StatLog
    {
        private Collection<Stat> _stats;
        private String _lastSubmit;
        private HardwareKey _hardwareKey;
        private Boolean _xmdsProcessing;

        public StatLog()
        {
            _stats = new Collection<Stat>();
            
            // Get the key for this display
            _hardwareKey = new HardwareKey();

            _xmdsProcessing = false;
        }

        /// <summary>
        /// Record a complete Layout Event
        /// </summary>
        /// <param name="fromDT"></param>
        /// <param name="toDT"></param>
        /// <param name="scheduleID"></param>
        /// <param name="layoutID"></param>
        public void RecordLayout(String fromDT, String toDT, int scheduleID, int layoutID)
        {
            if (!ApplicationSettings.Default.StatsEnabled) return;

            Stat stat = new Stat();

            stat.type = StatType.Layout;
            stat.fromDate = fromDT;
            stat.toDate = toDT;
            stat.scheduleID = scheduleID;
            stat.layoutID = layoutID;

            _stats.Add(stat);

            return;
        }

        /// <summary>
        /// Record a complete Media Event
        /// </summary>
        /// <param name="fromDT"></param>
        /// <param name="toDT"></param>
        /// <param name="layoutID"></param>
        /// <param name="mediaID"></param>
        public void RecordMedia(String fromDT, String toDT, int layoutID, String mediaID)
        {
            if (!ApplicationSettings.Default.StatsEnabled) return;

            Stat stat = new Stat();

            stat.type = StatType.Media;
            stat.fromDate = fromDT;
            stat.toDate = toDT;
            stat.layoutID = layoutID;
            stat.mediaID = mediaID;

            _stats.Add(stat);

            return;
        }

        /// <summary>
        /// Record a complete Event
        /// </summary>
        /// <param name="fromDT"></param>
        /// <param name="toDT"></param>
        /// <param name="tag"></param>
        public void RecordEvent(String fromDT, String toDT, String tag)
        {
            if (!ApplicationSettings.Default.StatsEnabled) return;

            Stat stat = new Stat();

            stat.type = StatType.Event;
            stat.fromDate = fromDT;
            stat.toDate = toDT;
            stat.tag = tag;

            _stats.Add(stat);

            return;
        }

        /// <summary>
        /// RecordStat
        /// </summary>
        /// <param name="stat"></param>
        public void RecordStat(Stat stat)
        {
            if (!ApplicationSettings.Default.StatsEnabled) 
                return;

            Debug.WriteLine(String.Format("Recording a Stat Record. Current Count = {0}", _stats.Count.ToString()), LogType.Audit.ToString());

            _stats.Add(stat);

            // At some point we will need to flush the stats
            if (_stats.Count >= ApplicationSettings.Default.StatsFlushCount)
            {
                Flush();
            }

            return;
        }

        /// <summary>
        /// Flush the stats
        /// </summary>
        public void Flush()
        {
            Debug.WriteLine(new LogMessage("Flush", String.Format("IN")), LogType.Audit.ToString());

            // Determine if there is anything to flush
            if (_stats.Count < 1) 
                return;

            // Flush to File
            FlushToFile();

            // See if there are any records to flush to XMDS
            Thread logSubmit = new Thread(new ThreadStart(ProcessQueueToXmds));
            logSubmit.Start();

            Debug.WriteLine(new LogMessage("Flush", String.Format("OUT")), LogType.Audit.ToString());
        }

        /// <summary>
        /// Send the Stat to a File
        /// </summary>
        private void FlushToFile()
        {
            Debug.WriteLine(new LogMessage("FlushToFile", String.Format("IN")), LogType.Audit.ToString());

            // There is something to flush - we want to parse the collection adding to the TextWriter each time.
            try
            {
                // Open the Text Writer
                using (StreamWriter tw = new StreamWriter(File.Open(string.Format("{0}_{1}", ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.StatsLogFile, DateTime.Now.ToFileTimeUtc().ToString()), FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8))
                {
                    foreach (Stat stat in _stats)
                    {
                        tw.WriteLine(stat.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                // Log this exception
                Trace.WriteLine(new LogMessage("FlushToFile", String.Format("Error writing stats to file with exception {0}", ex.Message)), LogType.Error.ToString());
            }
            finally
            {
                // Always clear the stats. If the file open failed for some reason then we dont want to try again
                _stats.Clear();
            }

            Debug.WriteLine(new LogMessage("FlushToFile", String.Format("OUT")), LogType.Audit.ToString());
        }

        /// <summary>
        /// Send the Stats to XMDS
        /// </summary>
        private void ProcessQueueToXmds()
        {
            Debug.WriteLine(new LogMessage("FlushToXmds", String.Format("IN")), LogType.Audit.ToString());

            int threshold = ((int)ApplicationSettings.Default.CollectInterval * 5);

            // Determine where we want to log.
            if (ApplicationSettings.Default.XmdsLastConnection.AddSeconds(threshold) < DateTime.Now && true)
            {
                return;
            }

            // Get a list of all the log files waiting to be sent to XMDS.
            string[] logFiles = Directory.GetFiles(ApplicationSettings.Default.LibraryPath, "*" + ApplicationSettings.Default.StatsLogFile + "*");

            foreach (string fileName in logFiles)
            {
                // If we have some, create an XMDS object
                using (xmds.xmds logtoXmds = new xmds.xmds())
                {
                    logtoXmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds;

                    // construct the log message
                    StringBuilder builder = new StringBuilder();
                    builder.Append("<log>");

                    foreach (string entry in File.ReadAllLines(fileName))
                        builder.Append(entry);

                    builder.Append("</log>");

                    try
                    {
                        logtoXmds.SubmitStats(ApplicationSettings.Default.ServerKey, _hardwareKey.Key, builder.ToString());

                        // Delete the file we are on
                        File.Delete(fileName);
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(new LogMessage("FlushToXmds", string.Format("Exception when submitting to XMDS: {0}", e.Message)), LogType.Error.ToString());
                    }
                }
            }

            // Log out
            System.Diagnostics.Debug.WriteLine(new LogMessage("FlushToXmds", String.Format("OUT")), LogType.Audit.ToString());
        }
    }

    class Stat 
    {
        public StatType type;
        public String fromDate;
        public String toDate;
        public int layoutID;
        public int scheduleID;
        public String mediaID;
        public String tag;

        public override string ToString()
        {
            // Format the message into the expected XML sub nodes.
            // Just do this with a string builder rather than an XML builder.
            String theMessage;

            theMessage = String.Format("<stat type=\"{0}\" fromdt=\"{1}\" todt=\"{2}\" layoutid=\"{3}\" scheduleid=\"{4}\" mediaid=\"{5}\"></stat>", type, fromDate, toDate, layoutID.ToString(), scheduleID.ToString(), mediaID);
            
            return theMessage;
        }
    }

    public enum StatType { Layout, Media, Event };
}
