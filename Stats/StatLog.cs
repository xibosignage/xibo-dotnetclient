/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
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
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace XiboClient.Stats
{
    public sealed class StatLog
    {
        private static readonly Lazy<StatLog>
            lazy =
            new Lazy<StatLog>
            (() => new StatLog());

        public static StatLog Instance { get { return lazy.Value; } }

        private Collection<Stat> _stats;

        private StatLog()
        {
            _stats = new Collection<Stat>();
        }

        /// <summary>
        /// RecordStat
        /// </summary>
        /// <param name="stat"></param>
        public void RecordStat(Stat stat)
        {
            if (!ApplicationSettings.Default.StatsEnabled || !stat.isEnabled)
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
                using (FileStream fileStream = File.Open(string.Format("{0}_{1}", ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.StatsLogFile, DateTime.Now.ToFileTimeUtc().ToString()), FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    using (StreamWriter tw = new StreamWriter(fileStream, Encoding.UTF8))
                    {
                        foreach (Stat stat in _stats)
                        {
                            tw.WriteLine(stat.ToString());
                        }
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
    }
}
