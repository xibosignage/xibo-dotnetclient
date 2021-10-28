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
using System.Collections.Generic;
using System.Device.Location;

namespace XiboClient.Stats
{
    public class Stat
    {
        public StatType Type;
        public DateTime From;
        public DateTime To;
        public int LayoutId;
        public int ScheduleId;
        public string WidgetId;
        public string Tag;
        public int Duration;
        public int Count;

        public GeoCoordinate GeoStart;
        public GeoCoordinate GeoEnd;

        /// <summary>
        /// Engagements (such as geo-location, tags)
        /// </summary>
        public Dictionary<string, Engagement> Engagements { get; set; } 

        /// <summary>
        /// Constructor
        /// </summary>
        public Stat()
        {
            Engagements = new Dictionary<string, Engagement>();
        }

        /// <summary>
        /// Return a StatType for the string provided
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static StatType StatTypeFromString(string type)
        {
            if (type.ToLowerInvariant() == "layout")
            {
                return StatType.Layout;
            }
            else if (type.ToLowerInvariant() == "media")
            {
                return StatType.Media;
            }
            else
            {
                return StatType.Event;
            }
        }
    }

    public enum StatType { Layout, Media, Event };
}
