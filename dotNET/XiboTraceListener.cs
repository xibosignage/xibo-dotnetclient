/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2012 Daniel Garner
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

/// 17/02/12 Dan Changed to always Log audit if no category is given

namespace XiboClient
{
    class XiboTraceListener : TraceListener
    {
        private Collection<TraceMessage> _traceMessages;
        private String _logPath;
        private Boolean _xmdsProcessing;
        private xmds.xmds _xmds;
        private String _lastSubmit;
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
            _logPath = Application.UserAppDataPath + @"/" + Properties.Settings.Default.logLocation;

            _xmdsProcessing = false;
            _xmds = new xmds.xmds();

            // Register a listener for the XMDS stats
            _xmds.SubmitLogCompleted += new XiboClient.xmds.SubmitLogCompletedEventHandler(_xmds_SubmitLogCompleted);

            // Get the key for this display
            _hardwareKey = new HardwareKey();
        }

        private void AddToCollection(string message, string category)
        {
            TraceMessage traceMessage;

            traceMessage.category = category;
            traceMessage.dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            traceMessage.message = message;

            _traceMessages.Add(traceMessage);
        }

        private void FlushToFile()
        {
            if (_traceMessages.Count < 1) return;

            try
            {
                // Open the Text Writer
                StreamWriter tw = new StreamWriter(File.Open(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);

                String theMessage;

                foreach (TraceMessage message in _traceMessages)
                {
                    String traceMsg = message.message.ToString();

                    theMessage = String.Format("<trace date=\"{0}\" category=\"{1}\">{2}</trace>", message.dateTime, message.category, traceMsg);
                    tw.WriteLine(theMessage);
                }

                // Close the tw.
                tw.Close();
                tw.Dispose();

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

        /// <summary>
        /// Flush the log to XMDS
        /// </summary>
        private void FlushToXmds()
        {
            String log;

            log = "<log>";

            // Load the Stats collection into a string
            try
            {
                foreach (TraceMessage traceMessage in _traceMessages)
                {
                    String traceMsg = traceMessage.message.ToString();

                    if (!traceMsg.Contains("<message>"))
                        traceMsg = SecurityElement.Escape(traceMsg);

                    log += String.Format("<trace date=\"{0}\" category=\"{1}\">{2}</trace>", traceMessage.dateTime, traceMessage.category, traceMsg);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(new LogMessage("FlushToXmds", String.Format("Error converting stat to a string {0}", ex.Message)), LogType.Error.ToString());
            }

            log += "</log>";

            // Store the stats as the last sent (so we have a record if it fails)
            _lastSubmit = log;

            // Clear the stats collection
            _traceMessages.Clear();

            // Submit the string to XMDS
            _xmdsProcessing = true;

            _xmds.SubmitLogAsync(Properties.Settings.Default.Version, Properties.Settings.Default.ServerKey, _hardwareKey.Key, log);
        }

        /// <summary>
        /// Capture the XMDS call and see if it went well
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _xmds_SubmitLogCompleted(object sender, XiboClient.xmds.SubmitLogCompletedEventArgs e)
        {
            _xmdsProcessing = false;

            // Test if we succeeded or not
            if (e.Error != null)
            {
                // We had an error, log it.
                System.Diagnostics.Trace.WriteLine(new LogMessage("_xmds_SubmitLogCompleted", String.Format("Error during Submit to XMDS {0}", e.Error.Message)), LogType.Error.ToString());

                // Dump the stats to a file instead
                if (_lastSubmit != "")
                {
                    try
                    {
                        // Open the Text Writer
                        StreamWriter tw = new StreamWriter(File.Open(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);

                        try
                        {
                            tw.Write(_lastSubmit);
                        }
                        catch {}
                        finally
                        {
                            tw.Close();
                            tw.Dispose();
                        }
                    }
                    catch {}
                }
            }

            // Clear the last sumbit
            _lastSubmit = "";
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

        public override void Close()
        {
            // Determine if there is anything to flush
            if (_traceMessages.Count < 1) return;

            // As we are closing if XMDS is already busy just log to file.
            if (_xmdsProcessing)
            {
                FlushToFile();
            }
            else
            {
                int threshold = ((int)Properties.Settings.Default.collectInterval * 5);

                // Determine where we want to log.
                if (Properties.Settings.Default.XmdsLastConnection.AddSeconds(threshold) < DateTime.Now)
                {
                    FlushToFile();
                }
                else
                {
                    FlushToXmds();
                }
            }
        }

        public override void Flush()
        {
            // Determine if there is anything to flush
            if (_traceMessages.Count < 1 || _xmdsProcessing) return;

            int threshold = ((int)Properties.Settings.Default.collectInterval * 5);

            // Determine where we want to log.
            if (Properties.Settings.Default.XmdsLastConnection.AddSeconds(threshold) < DateTime.Now)
            {
                FlushToFile();
            }
            else
            {
                FlushToXmds();
            }
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
