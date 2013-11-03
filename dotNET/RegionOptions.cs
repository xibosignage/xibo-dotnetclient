/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2013 Daniel Garner
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

namespace XiboClient
{
    /// <summary>
    /// The options specific to a region
    /// </summary>
    struct RegionOptions
    {
        public double scaleFactor;
        public int width;
        public int height;
        public int top;
        public int left;
        public int originalWidth;
        public int originalHeight;

        public int backgroundLeft;
        public int backgroundTop;

        public string type;
        public string uri;
        public int duration;

        //xml
        public XmlNodeList mediaNodes;

        //rss options
        public string direction;
        public string text;
        public string documentTemplate;
        public string copyrightNotice;
        public string javaScript;
        public int updateInterval;
        public int scrollSpeed;

        //The identification for this region
        public string mediaid;
        public int layoutId;
        public string regionId;
        public int scheduleId;
        public int CurrentIndex;

        //general options
        public string backgroundImage;
        public string backgroundColor;

        public MediaDictionary Dictionary;

        public DateTime LayoutModifiedDate { get; set; }

        public override string ToString()
        {
            return String.Format("({0},{1},{2},{3},{4},{5})", width, height, top, left, type, uri);
        }
    }
}
