/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006,2007,2008 Daniel Garner
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

namespace XiboClient
{
    class XiboTraceListener : TraceListener
    {
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
            traceMessages = new Collection<TraceMessage>();
            logPath = Application.UserAppDataPath + @"/" + Properties.Settings.Default.logLocation;
        }

        private void AddToCollection(string message, string category)
        {
            TraceMessage traceMessage;

            traceMessage.category = category;
            traceMessage.dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            traceMessage.message = message;

            traceMessages.Add(traceMessage);
        }

        private void FlushToFile()
        {
            if (traceMessages.Count < 1) return;

            try
            {
                // Open the Text Writer
                StreamWriter tw = new StreamWriter(File.Open(logPath, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);

                String theMessage;

                foreach (TraceMessage message in traceMessages)
                {
                    String traceMsg = message.message.ToString();

                    theMessage = String.Format("<trace date=\"{0}\" category=\"{1}\">{2}</trace>", message.dateTime, message.category, traceMsg);
                    tw.WriteLine(theMessage);
                }

                // Close the tw.
                tw.Close();
                tw.Dispose();

                // Remove the messages we have just added
                traceMessages.Clear();
            }
            catch
            {
                // What can we do?
            }
            finally
            {
                traceMessages.Clear();
            }
        }

        public override void Write(string message)
        {
            AddToCollection(message, "Audit");
        }

        public override void Write(object o)
        {
            AddToCollection(o.ToString(), "Audit");
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

        public override void Close()
        {
            FlushToFile();
        }

        public override void Flush()
        {
            FlushToFile();
        }

        private Collection<TraceMessage> traceMessages;
        private String logPath;
    }

    [Serializable]
    struct TraceMessage
    {
        public String message;
        public String dateTime;
        public String category;
    }
}
