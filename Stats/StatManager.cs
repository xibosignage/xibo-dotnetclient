/**
 * Copyright (C) 2020 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - http://www.xibo.org.uk
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
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using XiboClient.XmdsAgents;

namespace XiboClient.Stats
{
    public sealed class StatManager
    {
        public static object _locker = new object();

        private static readonly Lazy<StatManager>
            lazy =
            new Lazy<StatManager>
            (() => new StatManager());

        /// <summary>
        /// Instance
        /// </summary>
        public static StatManager Instance { get { return lazy.Value; } }

        /// <summary>
        /// Proof of Play stats
        /// </summary>
        private Dictionary<string, Stat> proofOfPlay = new Dictionary<string, Stat>();

        /// <summary>
        /// The database path
        /// </summary>
        private string databasePath;

        /// <summary>
        /// Last time we sent stats
        /// </summary>
        public DateTime LastSendDate { get; set; }

        /// <summary>
        /// A Stat Agent which we will maintain in a thread
        /// </summary>
        private StatAgent statAgent;
        private Thread statAgentThread;

        /// <summary>
        /// Init table
        /// usually run on start up
        /// </summary>
        public void InitDatabase()
        {
            // No error catching in here - if we fail to create this DB then we have big issues?
            this.databasePath = ApplicationSettings.Default.LibraryPath + @"\pop.db";

            if (!File.Exists(this.databasePath))
            {
                File.Create(this.databasePath);
            }

            using (var connection = new SqliteConnection("Filename=" + this.databasePath))
            {
                string sql = "CREATE TABLE IF NOT EXISTS stat (" +
                    "_id INTEGER PRIMARY KEY, " +
                    "fromdt TEXT, " +
                    "todt TEXT, " +
                    "type TEXT, " +
                    "scheduleId INT, " +
                    "layoutId INT, " +
                    "widgetId TEXT, " +
                    "tag TEXT, " +
                    "processing INT" +
                    ")";

                // Open the connection
                connection.Open();

                // Create an execute a command.
                using (var command = new SqliteCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Start the Stat Manager
        /// </summary>
        public void Start()
        {
            this.statAgent = new StatAgent();
            this.statAgentThread = new Thread(new ThreadStart(this.statAgent.Run));
            this.statAgentThread.Name = "StatAgentThread";
            this.statAgentThread.Start();
        }

        /// <summary>
        /// Stop the StatManager
        /// </summary>
        public void Stop()
        {
            this.statAgent.Stop();
        }

        /// <summary>
        /// Layout Start Event
        /// </summary>
        /// <param name="scheduleId"></param>
        /// <param name="layoutId"></param>
        public void LayoutStart(int scheduleId, int layoutId)
        {
            lock (_locker)
            {
                // New record, which we put in the dictionary
                string key = scheduleId + "-" + layoutId;
                Stat stat = new Stat();
                stat.Type = StatType.Layout;
                stat.From = DateTime.Now;
                stat.ScheduleId = scheduleId;
                stat.LayoutId = layoutId;

                this.proofOfPlay.Add(key, stat);
            }
        }

        /// <summary>
        /// Layout Stop Event
        /// </summary>
        /// <param name="scheduleId"></param>
        /// <param name="layoutId"></param>
        /// <param name="statEnabled"></param>
        /// <returns>Duration</returns>
        public double LayoutStop(int scheduleId, int layoutId, bool statEnabled)
        {
            double duration = 0;

            lock (_locker)
            {
                // Record we expect to already be open in the Dictionary
                string key = scheduleId + "-" + layoutId;
                Stat stat;

                if (this.proofOfPlay.TryGetValue(key, out stat))
                {
                    // Remove from the Dictionary
                    this.proofOfPlay.Remove(key);

                    // Set the to date
                    stat.To = DateTime.Now;

                    // Work our the duration
                    duration = (stat.To - stat.From).TotalSeconds;

                    if (ApplicationSettings.Default.StatsEnabled && statEnabled)
                    {
                        // Record
                        RecordStat(stat);
                    }
                }
                else
                {
                    // This is bad, we should log it
                    Trace.WriteLine(new LogMessage("StatManager", "LayoutStop: Closing stat record without an associated opening record."), LogType.Info.ToString());
                }
            }

            return duration;
        }

        /// <summary>
        /// Widget Start Event
        /// </summary>
        /// <param name="scheduleId"></param>
        /// <param name="layoutId"></param>
        /// <param name="widgetId"></param>
        public void WidgetStart(int scheduleId, int layoutId, string widgetId)
        {
            lock (_locker)
            {
                // New record, which we put in the dictionary
                string key = scheduleId + "-" + layoutId + "-" + widgetId;
                Stat stat = new Stat();
                stat.Type = StatType.Media;
                stat.From = DateTime.Now;
                stat.ScheduleId = scheduleId;
                stat.LayoutId = layoutId;
                stat.WidgetId = widgetId;

                this.proofOfPlay.Add(key, stat);
            }
        }

        /// <summary>
        /// Widget Stop Event
        /// </summary>
        /// <param name="scheduleId"></param>
        /// <param name="layoutId"></param>
        /// <param name="widgetId"></param>
        /// <param name="statEnabled"></param>
        /// <returns>Duration</returns>
        public double WidgetStop(int scheduleId, int layoutId, string widgetId, bool statEnabled)
        {
            double duration = 0;

            lock (_locker)
            {
                // Record we expect to already be open in the Dictionary
                string key = scheduleId + "-" + layoutId + "-" + widgetId;
                Stat stat;

                if (this.proofOfPlay.TryGetValue(key, out stat))
                {
                    // Remove from the Dictionary
                    this.proofOfPlay.Remove(key);

                    // Set the to date
                    stat.To = DateTime.Now;

                    // Work our the duration
                    duration = (stat.To - stat.From).TotalSeconds;

                    if (ApplicationSettings.Default.StatsEnabled && statEnabled)
                    {
                        // Record
                        RecordStat(stat);
                    }
                }
                else
                {
                    // This is bad, we should log it
                    Trace.WriteLine(new LogMessage("StatManager", "WidgetStop: Closing stat record without an associated opening record."), LogType.Info.ToString());
                }
            }

            return duration;
        }

        /// <summary>
        /// Records a stat record
        /// </summary>
        /// <param name="stat"></param>
        private void RecordStat(Stat stat)
        {
            try
            {
                using (var connection = new SqliteConnection("Filename=" + this.databasePath))
                {
                    connection.Open();

                    SqliteCommand command = new SqliteCommand();
                    command.Connection = connection;

                    // Parameterize
                    command.CommandText = "INSERT INTO stat (type, fromdt, todt, scheduleId, layoutId, widgetId, tag, processing) " +
                        "VALUES (@type, @fromdt, @todt, @scheduleId, @layoutId, @widgetId, @tag, @processing)";

                    command.Parameters.AddWithValue("@type", stat.Type.ToString());
                    command.Parameters.AddWithValue("@fromdt", stat.From.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF"));
                    command.Parameters.AddWithValue("@todt", stat.To.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF"));
                    command.Parameters.AddWithValue("@scheduleId", stat.ScheduleId);
                    command.Parameters.AddWithValue("@layoutId", stat.LayoutId);
                    command.Parameters.AddWithValue("@widgetId", stat.WidgetId ?? "");
                    command.Parameters.AddWithValue("@tag", stat.Tag ?? "");
                    command.Parameters.AddWithValue("@processing", 0);

                    // Execute and don't wait for the result
                    command.ExecuteNonQueryAsync();

                    // TODO: should we trigger a send to happen?
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("StatManager", "RecordStat: Error saving stat to database. Ex = " + ex.Message), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// Mark stat records to be sent if there are some to send
        /// </summary>
        /// <param name="marker"></param>
        /// <param name="isBacklog"></param>
        /// <returns></returns>
        public bool MarkRecordsForSend(int marker, bool isBacklog)
        {
            string aggregationLevel = ApplicationSettings.Default.AggregationLevel.ToLowerInvariant();

            // Run query
            using (var connection = new SqliteConnection("Filename=" + this.databasePath))
            using (SqliteCommand cmd = new SqliteCommand())
            {
                connection.Open();

                cmd.Connection = connection;
                cmd.CommandText = "UPDATE stat SET processing = @processing WHERE _id IN (" +
                    "SELECT _id FROM stat WHERE ifnull(processing, 0) = 0 " + ((aggregationLevel == "individual") ? "" : " AND todt < @todt ") + " ORDER BY _id LIMIT @limit" +
                    ")";

                // Set the marker
                cmd.Parameters.AddWithValue("@processing", marker);

                if (aggregationLevel == "hourly")
                {
                    // Hourly (get only from last hour)
                    cmd.Parameters.AddWithValue("@todt", DateTime.Now.ToString("yyyy-MM-dd HH:00:00.0000000"));

                    // if we are in backlog mode, then take a days worth of worse case, otherwise an hours worth
                    cmd.Parameters.AddWithValue("@limit", (isBacklog) ? 86400 : 3600);
                }
                else if (aggregationLevel == "daily")
                {
                    // Daily (get only from this day)
                    cmd.Parameters.AddWithValue("@todt", DateTime.Now.Date.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF"));

                    // The maximum number of records we can get in one day (or at least a reasonable number is 1 per second)
                    // gather at least that many, regardless of mode.
                    cmd.Parameters.AddWithValue("@limit", 86400);
                }
                else
                {
                    // Individual
                    cmd.Parameters.AddWithValue("@limit", (isBacklog) ? 300 : 50);
                }

                return cmd.ExecuteNonQuery() > 0;
            }
        }

        /// <summary>
        /// Unmark records marked for send
        /// </summary>
        /// <param name="marker"></param>
        public void UnmarkRecordsForSend(int marker)
        {
            using (var connection = new SqliteConnection("Filename=" + this.databasePath))
            using (SqliteCommand cmd = new SqliteCommand())
            {
                connection.Open();
                cmd.Connection = connection;
                cmd.CommandText = "UPDATE stat SET processing = 0 WHERE processing = @processing";
                cmd.Parameters.AddWithValue("processing", marker);
                cmd.ExecuteScalar();
            }
        }

        /// <summary>
        /// Delete stats that have been sent
        /// </summary>
        /// <param name="marker"></param>
        public void DeleteSent(int marker)
        {
            using (var connection = new SqliteConnection("Filename=" + this.databasePath))
            using (SqliteCommand cmd = new SqliteCommand())
            {
                connection.Open();
                cmd.Connection = connection;
                cmd.CommandText = "DELETE FROM stat WHERE processing = @processing";
                cmd.Parameters.AddWithValue("processing", marker);
                cmd.ExecuteScalar();
            }
        }

        /// <summary>
        /// Get XML for the stats to send
        /// </summary>
        /// <param name="marker"></param>
        /// <returns></returns>
        public string GetXmlForSend(int marker)
        {
            string aggregationLevel = ApplicationSettings.Default.AggregationLevel.ToLowerInvariant();
            StringBuilder builder = new StringBuilder();

            using (XmlWriter writer = XmlWriter.Create(builder))
            using (var connection = new SqliteConnection("Filename=" + this.databasePath))
            using (SqliteCommand cmd = new SqliteCommand())
            {
                // Start off our XML document
                writer.WriteStartDocument();
                writer.WriteStartElement("log");

                connection.Open();
                cmd.Connection = connection;
                cmd.CommandText = "SELECT type, fromdt, todt, scheduleId, layoutId, widgetId, tag FROM stat WHERE processing = @processing";
                cmd.Parameters.AddWithValue("processing", marker);

                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    // Aggregate the records or not?
                    if (aggregationLevel == "individual")
                    {
                        while (reader.Read())
                        {
                            DateTime from = reader.GetDateTime(1);
                            DateTime to = reader.GetDateTime(2);
                            writer.WriteStartElement("stat");
                            writer.WriteAttributeString("type", reader.GetString(0));
                            writer.WriteAttributeString("fromdt", from.ToString("yyyy-MM-dd HH:mm:ss"));
                            writer.WriteAttributeString("todt", to.ToString("yyyy-MM-dd HH:mm:ss"));
                            writer.WriteAttributeString("scheduleid", reader.GetString(3));
                            writer.WriteAttributeString("layoutid", reader.GetString(4));
                            writer.WriteAttributeString("mediaid", reader.GetString(5));
                            writer.WriteAttributeString("tag", reader.GetString(6));
                            writer.WriteAttributeString("duration", "" + (to - from).TotalSeconds);
                            writer.WriteAttributeString("count", "1");
                            writer.WriteEndElement();
                        }
                    }
                    else
                    {
                        // Create a Dictionary to store our aggregates
                        Dictionary<string, Stat> aggregates = new Dictionary<string, Stat>();

                        while (reader.Read())
                        {
                            // Decide where our aggregate falls
                            DateTime fromAggregate;
                            DateTime toAggregate;
                            DateTime from = reader.GetDateTime(1);
                            DateTime to = reader.GetDateTime(2);
                            int duration = Convert.ToInt32((to - from).TotalSeconds);

                            if (aggregationLevel == "daily")
                            {
                                // This is the from date set to midnight, and the to date set to a day after
                                fromAggregate = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0);
                                toAggregate = fromAggregate.AddDays(1);
                            }
                            else
                            {
                                // This is the date set to the top of the fromdt hour
                                fromAggregate = new DateTime(from.Year, from.Month, from.Day, from.Hour, 0, 0);
                                toAggregate = fromAggregate.AddHours(1);
                            }

                            // If the to date is actually outside this window, then treat the record as its own whole entry
                            // don't split it
                            if (to > toAggregate)
                            {
                                // Reset to the event dates.
                                fromAggregate = from;
                                toAggregate = to;
                            }

                            // Parse the other columns from the reader
                            int layoutId = reader.GetInt32(4);
                            int scheduleId = reader.GetInt32(3);

                            string type = reader.GetString(0);
                            string mediaId = reader.GetString(5);
                            string tag = reader.GetString(6);

                            // Create a Key to anchor the aggregate.
                            string key = type + scheduleId + "-" + mediaId + tag + fromAggregate.ToString("yyyyMMddHHmm") + toAggregate.ToString("yyyyMMddHHmm");

                            if (!aggregates.ContainsKey(key))
                            {
                                Stat stat = new Stat();
                                stat.Type = Stat.StatTypeFromString(type);
                                stat.ScheduleId = scheduleId;
                                stat.LayoutId = layoutId;
                                stat.WidgetId = mediaId;
                                stat.Tag = tag;

                                // Dates are our aggregate dates
                                stat.From = fromAggregate;
                                stat.To = toAggregate;

                                // Duration
                                stat.Count = 1;
                                stat.Duration = duration;

                                // Add to the dictionary
                                aggregates.Add(key, stat);
                            }
                            else
                            {
                                Stat stat = new Stat();
                                aggregates.TryGetValue(key, out stat);

                                // Add the duration.
                                stat.Count++;
                                stat.Duration += duration;
                            }
                        }

                        // Process the aggregates
                        foreach (Stat stat in aggregates.Values)
                        {
                            writer.WriteStartElement("stat");
                            writer.WriteAttributeString("type", stat.Type.ToString());
                            writer.WriteAttributeString("fromdt", stat.From.ToString("yyyy-MM-dd HH:mm:ss"));
                            writer.WriteAttributeString("todt", stat.To.ToString("yyyy-MM-dd HH:mm:ss"));
                            writer.WriteAttributeString("scheduleid", stat.ScheduleId.ToString());
                            writer.WriteAttributeString("layoutid", stat.LayoutId.ToString());
                            writer.WriteAttributeString("mediaid", stat.WidgetId);
                            writer.WriteAttributeString("tag", stat.Tag);
                            writer.WriteAttributeString("duration", "" + stat.Duration);
                            writer.WriteAttributeString("count", "" + stat.Count);
                            writer.WriteEndElement();
                        }
                    }
                }

                // Closing log element
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return builder.ToString();
        }

        /// <summary>
        /// Get the Total Number of Recorded Stats
        /// </summary>
        /// <returns></returns>
        public int TotalRecorded()
        {
            using (var connection = new SqliteConnection("Filename=" + this.databasePath))
            using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM stat", connection))
            {
                connection.Open();
                var result = cmd.ExecuteScalar();
                return result == null ? 0 : Convert.ToInt32(result);
            }
        }

        /// <summary>
        /// Get the total number of stats ready to send
        /// </summary>
        /// <returns></returns>
        public int TotalReady()
        {
            if (ApplicationSettings.Default.AggregationLevel.ToLowerInvariant() == "individual")
            {
                return TotalRecorded();
            }

            // Calculate a cut off date
            string cutOff = ApplicationSettings.Default.AggregationLevel.ToLowerInvariant() == "daily"
                ? DateTime.Now.Date.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF")
                : DateTime.Now.ToString("yyyy-MM-dd HH:") + ":00:00.0000000";

            // Run query
            using (var connection = new SqliteConnection("Filename=" + this.databasePath))
            using (SqliteCommand cmd = new SqliteCommand())
            {
                connection.Open();

                cmd.Connection = connection;
                cmd.CommandText = "SELECT COUNT(*) FROM stat WHERE todt < @todt";
                cmd.Parameters.AddWithValue("@todt", cutOff);

                var result = cmd.ExecuteScalar();
                return result == null ? 0 : Convert.ToInt32(result);
            }
        }
    }
}
