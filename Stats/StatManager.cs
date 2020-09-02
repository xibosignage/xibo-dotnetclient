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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using XiboClient.Log;
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
                // Open the connection
                connection.Open();

                // What version are we?
                int version = GetDbVersion(connection);

                if (version == 0)
                {
                    // Create the table fresh
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

                    // Create an execute a command.
                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                
                // Add the engagements column
                if (version <= 1)
                {
                    using (var command = new SqliteCommand("ALTER TABLE stat ADD COLUMN engagements TEXT", connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // Set the DB version to 2
                    SetDbVersion(connection, 2);
                }
            }
        }

        /// <summary>
        /// Get the current DB version.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        private int GetDbVersion(SqliteConnection connection)
        {
            try
            {
                using (var command = new SqliteCommand("PRAGMA user_version", connection))
                {
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Set the DB version
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="version"></param>
        private void SetDbVersion(SqliteConnection connection, int version)
        {
            using (var command = new SqliteCommand("PRAGMA user_version = " + version, connection))
            {
                command.ExecuteNonQuery();
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
                Stat stat = new Stat
                {
                    Type = StatType.Layout,
                    From = DateTime.Now,
                    ScheduleId = scheduleId,
                    LayoutId = layoutId
                };

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

                if (this.proofOfPlay.TryGetValue(key, out Stat stat))
                {
                    // Remove from the Dictionary
                    this.proofOfPlay.Remove(key);

                    // Set the to date
                    stat.To = DateTime.Now;

                    // Work our the duration
                    duration = (stat.To - stat.From).TotalSeconds;

                    // GeoLocation
                    AnnotateWithLocation(stat, duration);

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
            Debug.WriteLine(string.Format("WidgetStart: scheduleId: {0}, layoutId: {1}, widgetId: {2}", scheduleId, layoutId, widgetId), "StatManager");

            lock (_locker)
            {
                // New record, which we put in the dictionary
                string key = scheduleId + "-" + layoutId + "-" + widgetId;
                Stat stat = new Stat
                {
                    Type = StatType.Media,
                    From = DateTime.Now,
                    ScheduleId = scheduleId,
                    LayoutId = layoutId,
                    WidgetId = widgetId
                };

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
            Debug.WriteLine(string.Format("WidgetStop: scheduleId: {0}, layoutId: {1}, widgetId: {2}", scheduleId, layoutId, widgetId), "StatManager");

            double duration = 0;

            lock (_locker)
            {
                // Record we expect to already be open in the Dictionary
                string key = scheduleId + "-" + layoutId + "-" + widgetId;

                if (this.proofOfPlay.TryGetValue(key, out Stat stat))
                {
                    // Remove from the Dictionary
                    this.proofOfPlay.Remove(key);

                    // Set the to date
                    stat.To = DateTime.Now;

                    // Work our the duration
                    duration = (stat.To - stat.From).TotalSeconds;

                    // GeoLocation
                    AnnotateWithLocation(stat, duration);

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
        /// Annotate a stat record with an engagement
        /// </summary>
        /// <param name="stat"></param>
        /// <param name="duration"></param>
        private void AnnotateWithLocation(Stat stat, double duration)
        {
            // Do we have any engagements to record?
            if (ClientInfo.Instance.CurrentGeoLocation != null && !ClientInfo.Instance.CurrentGeoLocation.IsUnknown)
            {
                // Annotate our stat with the current geolocation
                Engagement engagement = new Engagement
                {
                    Tag = "LOCATION:" + ClientInfo.Instance.CurrentGeoLocation.Latitude + ":" + ClientInfo.Instance.CurrentGeoLocation.Longitude,
                    Duration = duration,
                    Count = 1
                };
                stat.Engagements.Add("LOCATION", engagement);
            }
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
                    command.CommandText = "INSERT INTO stat (type, fromdt, todt, scheduleId, layoutId, widgetId, tag, engagements, processing) " +
                        "VALUES (@type, @fromdt, @todt, @scheduleId, @layoutId, @widgetId, @tag, @engagements, @processing)";

                    command.Parameters.AddWithValue("@type", stat.Type.ToString());
                    command.Parameters.AddWithValue("@fromdt", stat.From.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("@todt", stat.To.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("@scheduleId", stat.ScheduleId);
                    command.Parameters.AddWithValue("@layoutId", stat.LayoutId);
                    command.Parameters.AddWithValue("@widgetId", stat.WidgetId ?? "");
                    command.Parameters.AddWithValue("@tag", stat.Tag ?? "");
                    command.Parameters.AddWithValue("@processing", 0);

                    // Do we have any engagements?
                    if (stat.Engagements.Count > 0)
                    {
                        // Make a simple collection with them in instead.
                        command.Parameters.AddWithValue("@engagements", JsonConvert.SerializeObject(stat.Engagements));
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@engagements", "");
                    }

                    // Execute and don't wait for the result
                    command.ExecuteNonQueryAsync().ContinueWith(t =>
                    {
                        var aggException = t.Exception.Flatten();
                        foreach (var exception in aggException.InnerExceptions)
                        {
                            Trace.WriteLine(new LogMessage("StatManager", "RecordStat: Error saving stat to database. Ex = " + exception.Message), LogType.Error.ToString());
                        }
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
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
                    cmd.Parameters.AddWithValue("@todt", DateTime.Now.ToString("yyyy-MM-dd HH:00:00.0000000", CultureInfo.InvariantCulture));

                    // if we are in backlog mode, then take a days worth of worse case, otherwise an hours worth
                    cmd.Parameters.AddWithValue("@limit", (isBacklog) ? 86400 : 3600);
                }
                else if (aggregationLevel == "daily")
                {
                    // Daily (get only from this day)
                    cmd.Parameters.AddWithValue("@todt", DateTime.Now.Date.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));

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

            using (XmlWriter writer = XmlWriter.Create(builder, new XmlWriterSettings {
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment
            }))
            using (var connection = new SqliteConnection("Filename=" + this.databasePath))
            using (SqliteCommand cmd = new SqliteCommand())
            {
                // Start off our XML document
                writer.WriteStartElement("log");

                connection.Open();
                cmd.Connection = connection;
                cmd.CommandText = "SELECT type, fromdt, todt, scheduleId, layoutId, widgetId, tag, IFNULL(engagements, \"\") AS engagements FROM stat WHERE processing = @processing";
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
                            writer.WriteAttributeString("type", reader.GetString(0).ToLowerInvariant());
                            writer.WriteAttributeString("fromdt", from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("todt", to.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("scheduleid", reader.GetString(3));
                            writer.WriteAttributeString("layoutid", reader.GetString(4));
                            writer.WriteAttributeString("mediaid", reader.GetString(5));
                            writer.WriteAttributeString("tag", reader.GetString(6));
                            writer.WriteAttributeString("duration", "" + Math.Round((to - from).TotalSeconds));
                            writer.WriteAttributeString("count", "1");

                            // Engagements
                            string engagementString = reader.GetString(7);
                            if (!string.IsNullOrEmpty(engagementString))
                            {
                                try
                                {
                                    // Deserialise them and process.
                                    Dictionary<string, Engagement> engagements = JsonConvert.DeserializeObject<Dictionary<string, Engagement>>(engagementString);

                                    writer.WriteStartElement("engagements");

                                    foreach (Engagement engagement in engagements.Values)
                                    {
                                        writer.WriteStartElement("engagement");
                                        writer.WriteAttributeString("tag", engagement.Tag);
                                        writer.WriteAttributeString("duration", "" + Math.Round(engagement.Duration));
                                        writer.WriteAttributeString("count", "" + engagement.Count);
                                        writer.WriteEndElement();
                                    }

                                    writer.WriteEndElement();
                                }
                                catch
                                {
                                    Debug.WriteLine("GetXmlForSend: Error processing engagements.", "StatManager");
                                }
                            }

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
                            int duration = Convert.ToInt32(Math.Round((to - from).TotalSeconds));

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

                            string type = reader.GetString(0).ToLowerInvariant();
                            string mediaId = reader.GetString(5);
                            string tag = reader.GetString(6);
                            string engagements = reader.GetString(7);

                            // Create a Key to anchor the aggregate.
                            string key = type + scheduleId + "-" + mediaId + tag + fromAggregate.ToString("yyyyMMddHHmm") + toAggregate.ToString("yyyyMMddHHmm");

                            if (!aggregates.ContainsKey(key))
                            {
                                Stat stat = new Stat
                                {
                                    Type = Stat.StatTypeFromString(type),
                                    ScheduleId = scheduleId,
                                    LayoutId = layoutId,
                                    WidgetId = mediaId,
                                    Tag = tag
                                };

                                // Engagements
                                try
                                {
                                    if (string.IsNullOrEmpty(engagements))
                                    {
                                        stat.Engagements = new Dictionary<string, Engagement>();
                                    }
                                    else
                                    {
                                        stat.Engagements = JsonConvert.DeserializeObject<Dictionary<string, Engagement>>(engagements);
                                    }
                                }
                                catch
                                {
                                    stat.Engagements = new Dictionary<string, Engagement>();
                                }

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

                                // Add the new engagements to the existing.
                                if (!string.IsNullOrEmpty(engagements))
                                {
                                    Dictionary<string, Engagement> newEngagements = JsonConvert.DeserializeObject<Dictionary<string, Engagement>>(engagements);

                                    foreach (KeyValuePair<string, Engagement> keyValuePair in newEngagements)
                                    {
                                        // Do we already have this key in this stat record
                                        if (stat.Engagements.ContainsKey(keyValuePair.Key))
                                        {
                                            // Update the existing
                                            stat.Engagements[keyValuePair.Key].Count += keyValuePair.Value.Count;
                                            stat.Engagements[keyValuePair.Key].Duration += keyValuePair.Value.Duration;
                                        }
                                        else
                                        {
                                            // Add it whole
                                            stat.Engagements.Add(keyValuePair.Key, keyValuePair.Value);
                                        }
                                    }
                                }
                            }
                        }

                        // Process the aggregates
                        foreach (Stat stat in aggregates.Values)
                        {
                            writer.WriteStartElement("stat");
                            writer.WriteAttributeString("type", stat.Type.ToString());
                            writer.WriteAttributeString("fromdt", stat.From.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("todt", stat.To.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                            writer.WriteAttributeString("scheduleid", stat.ScheduleId.ToString());
                            writer.WriteAttributeString("layoutid", stat.LayoutId.ToString());
                            writer.WriteAttributeString("mediaid", stat.WidgetId);
                            writer.WriteAttributeString("tag", stat.Tag);
                            writer.WriteAttributeString("duration", "" + stat.Duration);
                            writer.WriteAttributeString("count", "" + stat.Count);

                            if (stat.Engagements.Count > 0)
                            {
                                writer.WriteStartElement("engagements");

                                foreach (Engagement engagement in stat.Engagements.Values)
                                {
                                    writer.WriteStartElement("engagement");
                                    writer.WriteAttributeString("tag", engagement.Tag);
                                    writer.WriteAttributeString("duration", "" + Math.Round(engagement.Duration));
                                    writer.WriteAttributeString("count", "" + engagement.Count);
                                    writer.WriteEndElement();
                                }

                                writer.WriteEndElement();
                            }

                            writer.WriteEndElement();
                        }
                    }
                }

                // Closing log element
                writer.WriteEndElement();
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
                ? DateTime.Now.Date.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture)
                : DateTime.Now.ToString("yyyy-MM-dd HH:", CultureInfo.InvariantCulture) + ":00:00.0000000";

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
