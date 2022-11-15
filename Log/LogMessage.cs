/*
 * Xibo - Digital Signage - https://xibosignage.com
 * Copyright (C) 2022 Xibo Signage Ltd
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
using System.Globalization;
using System.Security;
using System.Threading;
using System.Xml;
using XiboClient.Logic;

namespace XiboClient
{
    public class LogMessage
    {
        public string _thread { get; set; }
        public string _method { get; set; }
        public string _message { get; set; }
        public int _scheduleId { get; set; }
        public int _layoutId { get; set; }
        public int _mediaId { get; set; }
        public DateTime LogDate { get; set; }

        public static void Error(string className, string method, string message)
        {
            System.Diagnostics.Trace.WriteLine(new LogMessage(className, method + ": " + message), LogType.Error.ToString());
        }

        public static void Info(string className, string method, string message)
        {
            System.Diagnostics.Trace.WriteLine(new LogMessage(className, method + ": " + message), LogType.Info.ToString());
        }

        public static void Audit(string className, string method, string message)
        {
            System.Diagnostics.Trace.WriteLine(new LogMessage(className, method + ": " + message), LogType.Audit.ToString());
        }

        public static void Trace(string className, string method, string message)
        {
            System.Diagnostics.Debug.WriteLine(new LogMessage(className, method + ": " + message), "Trace");
        }

        public LogMessage(String method, String message)
        {
            LogDate = DateTime.Now;
            _method = method;
            _message = message;
            _thread = Thread.CurrentThread.Name;
        }

        public LogMessage(String method, String message, int scheduleId, int layoutId)
        {
            LogDate = DateTime.Now;
            _method = method;
            _message = message;
            _scheduleId = scheduleId;
            _layoutId = layoutId;
            _thread = Thread.CurrentThread.Name;
        }

        public LogMessage(String method, String message, int scheduleId, int layoutId, int mediaId)
        {
            LogDate = DateTime.Now;
            _method = method;
            _message = message;
            _scheduleId = scheduleId;
            _layoutId = layoutId;
            _mediaId = mediaId;
            _thread = Thread.CurrentThread.Name;
        }

        /// <summary>
        /// Load the log message via XML
        /// </summary>
        /// <param name="xmlMessage"></param>
        public LogMessage(string xmlMessage)
        {
            XmlDocument xml = new XmlDocument();
            xml.LoadXml("<xml>" + xmlMessage + "</xml>");

            try
            {
                LogDate = DateTime.Parse(XmlHelper.SelectFirstElementInnerTextOrDefault(xml, "logdate", "1970-01-01"));
                _message = XmlHelper.SelectFirstElementInnerTextOrDefault(xml, "message", "");
                _method = XmlHelper.SelectFirstElementInnerTextOrDefault(xml, "method", "");
                _thread = XmlHelper.SelectFirstElementInnerTextOrDefault(xml, "thread", "");
            }
            catch (NullReferenceException)
            {
                LogDate = DateTime.Now;
                _message = xmlMessage;
                _method = "Unknown";
                _thread = Thread.CurrentThread.Name;
            }
        }

        public override string ToString()
        {
            // Format the message into the expected XML sub nodes.
            // Just do this with a string builder rather than an XML builder.
            String theMessage;

            theMessage = String.Format("<logdate>{0}</logdate>", LogDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            theMessage += String.Format("<thread>{0}</thread>", _thread);
            theMessage += String.Format("<method>{0}</method>", _method);
            theMessage += String.Format("<message>{0}</message>", SecurityElement.Escape(_message));

            if (_scheduleId != 0) theMessage += String.Format("<scheduleid>{0}</scheduleid>", _scheduleId.ToString());
            if (_layoutId != 0) theMessage += String.Format("<layoutid>{0}</layoutid>", _scheduleId.ToString());
            if (_mediaId != 0) theMessage += String.Format("<mediaid>{0}</mediaid>", _scheduleId.ToString());

            return theMessage;
        }
    }

    public enum LogType { Info, Audit, Error }
}
