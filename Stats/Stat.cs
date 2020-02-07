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
using System;

namespace XiboClient.Stats
{
    public class Stat
    {
        public StatType type;
        public String fromDate;
        public String toDate;
        public int layoutID;
        public int scheduleID;
        public String mediaID;
        public String tag;

        /// <summary>
        /// Is this Stat enabled (if false it will not be recorded)
        /// </summary>
        public bool isEnabled = true;

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
