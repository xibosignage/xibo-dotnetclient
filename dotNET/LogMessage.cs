/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2009 Daniel Garner
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
using System.Text;
using System.Xml;
using System.Diagnostics;

namespace XiboClient
{
    public class LogMessage
    {
        public string _method;
        public string _message;
        public int _scheduleId;
        public int _layoutId;
        public int _mediaId;
        public DateTime LogDate;

        public LogMessage(String method, String message)
        {
            LogDate = DateTime.Now;
            _method = method;
            _message = message;
        }

        public LogMessage(String method, String message, int scheduleId, int layoutId)
        {
            LogDate = DateTime.Now;
            _method = method;
            _message = message;
            _scheduleId = scheduleId;
            _layoutId = layoutId;
        }

        public LogMessage(String method, String message, int scheduleId, int layoutId, int mediaId)
        {
            LogDate = DateTime.Now;
            _method = method;
            _message = message;
            _scheduleId = scheduleId;
            _layoutId = layoutId;
            _mediaId = mediaId;
        }

        /// <summary>
        /// Load the log message via XML
        /// </summary>
        /// <param name="xmlMessage"></param>
        public LogMessage(string xmlMessage)
        {
            XmlDocument xml = new XmlDocument();
            xml.LoadXml("<xml>" + xmlMessage + "</xml>");

            LogDate = DateTime.Parse(xml.GetElementsByTagName("logdate").Item(0).InnerText.ToString());
            _message = xml.GetElementsByTagName("message").Item(0).InnerText.ToString();
            _method = xml.GetElementsByTagName("method").Item(0).InnerText.ToString();
        }

        public override string ToString()
        {
            // Format the message into the expected XML sub nodes.
            // Just do this with a string builder rather than an XML builder.
            String theMessage;

            theMessage = String.Format("<message>{0}</message>", _message);
            theMessage += String.Format("<method>{0}</method>", _method);
            theMessage += String.Format("<logdate>{0}</logdate>", LogDate);

            if (_scheduleId != 0) theMessage += String.Format("<scheduleid>{0}</scheduleid>", _scheduleId.ToString());
            if (_layoutId != 0) theMessage += String.Format("<layoutid>{0}</layoutid>", _scheduleId.ToString());
            if (_mediaId != 0) theMessage += String.Format("<mediaid>{0}</mediaid>", _scheduleId.ToString());

            return theMessage;
        }
    }

    public enum LogType { Info, Audit, Error }
}
