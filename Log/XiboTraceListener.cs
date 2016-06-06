/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2016 Daniel Garner, Spring Signage Ltd
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
using System.IO;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using System.Security;
using System.Threading;
using System.Net;

/// 17/02/12 Dan Changed to always Log audit if no category is given

namespace XiboClient
{
    class XiboTraceListener : TraceListener
    {
        public static object _locker = new object();
        private Collection<TraceMessage> _traceMessages;
        private string _logPath;
        private HardwareKey _hardwareKey;

        public XiboTraceListener()
        {
            InitializeListener();
        }

        public XiboTraceListener(string r_strListenerName)
            : base(r_strListenerName)
		{
			InitializeListener() ;
		}

        private void InitializeListener()
        {
            // Make a new collection of TraceMessages
            _traceMessages = new Collection<TraceMessage>();
            _logPath = ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.LogLocation;

            // Get the key for this display
            _hardwareKey = new HardwareKey();
        }

        /// <summary>
        /// Get the LogType from a string
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        private static LogType GetLogTypeFromString(string category)
        {
            LogType logType;

            if (category == LogType.Audit.ToString())
                logType = LogType.Audit;
            else if (category == LogType.Error.ToString())
                logType = LogType.Error;
            else if (category == LogType.Info.ToString())
                logType = LogType.Info;
            else
                logType = LogType.Audit;

            return logType;
        }

        private void AddToCollection(string message, string category)
        {
            if (ApplicationSettings.Default.LogLevel == "off")
                return;

            LogType logtype = GetLogTypeFromString(category);

            // Determine if we should log this or not.
            if (ApplicationSettings.Default.LogLevel == "error" && logtype != LogType.Error)
                return;

            if (ApplicationSettings.Default.LogLevel == "info" && (logtype != LogType.Error && logtype != LogType.Info))
                return;
            
            _traceMessages.Add(new TraceMessage
            {
                category = category,
                dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                message = message
            });

            // Flush if we have build up a backlog.
            if (_traceMessages.Count > ((ApplicationSettings.Default.LogLevel == "audit") ? 100 : 25))
                Flush();
        }

        private void FlushToFile()
        {
            if (_traceMessages.Count < 1) 
                return;

            try
            {
                // Open the Text Writer
                using (FileStream fileStream = File.Open(string.Format("{0}_{1}", _logPath, DateTime.Now.ToFileTimeUtc().ToString()), FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    using (StreamWriter tw = new StreamWriter(fileStream, Encoding.UTF8))
                    {
                        string theMessage;

                        foreach (TraceMessage message in _traceMessages)
                        {
                            string traceMsg = message.message.ToString();

                            theMessage = string.Format("<trace date=\"{0}\" category=\"{1}\">{2}</trace>", message.dateTime, message.category, traceMsg);
                            tw.WriteLine(theMessage);
                        }
                    }
                }

                // Remove the messages we have just added
                _traceMessages.Clear();
            }
            catch
            {
                // What can we do?
            }
            finally
            {
                _traceMessages.Clear();
            }
        }

        public override void Write(string message)
        {
            AddToCollection(message, LogType.Audit.ToString());
        }

        public override void Write(object o)
        {
            AddToCollection(o.ToString(), LogType.Audit.ToString());
        }

        public override void Write(string message, string category)
        {
            AddToCollection(message, category);
        }

        public override void Write(object o, string category)
        {
            AddToCollection(o.ToString(), category);
        }

        public override void WriteLine(string message)
        {
            Write(message + "\n");
        }

        public override void WriteLine(object o)
        {
            Write(o.ToString() + "\n");
        }

        public override void WriteLine(string message, string category)
        {
            Write((message + "\n"), category);
        }

        public override void WriteLine(object o, string category)
        {
            Write((o.ToString() + "\n"), category);
        }

        public override void Fail(string message)
        {
            StackTrace objTrace = new StackTrace(true);
            message += "\n" + objTrace.ToString();

            AddToCollection(message, "");
        }

        public override void Fail(string message, string detailMessage)
        {
            StackTrace objTrace = new StackTrace(true);
            message += "\n" + objTrace.ToString();

            AddToCollection(message, detailMessage);
        }

        /// <summary>
        /// Close the Trace Listener
        /// </summary>
        public override void Close()
        {
            // Determine if there is anything to flush
            if (_traceMessages.Count < 1) 
                return;

            // Flush to file (we will send these next time we start up)
            FlushToFile();
        }

        /// <summary>
        /// Flush the Listener
        /// </summary>
        public override void Flush()
        {
            // Determine if there is anything to flush
            if (_traceMessages.Count < 1) 
                return;

            FlushToFile();
        }
    }

    [Serializable]
    struct TraceMessage
    {
        public String message;
        public String dateTime;
        public String category;
    }
}
