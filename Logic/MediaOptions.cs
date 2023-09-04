/**
 * Copyright (C) 2023 Xibo Signage Ltd
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
using XiboClient.Adspace;
using XiboClient.Logic;
using XiboClient.Rendering;

namespace XiboClient
{
    public struct MediaOptions
    {
        public double scaleFactor;
        public int width;
        public int height;
        public int top;
        public int left;
        public int originalWidth;
        public int originalHeight;

        // Widget From/To dates
        public DateTime FromDt { get; set; }
        public DateTime ToDt { get; set; }

        public int backgroundLeft;
        public int backgroundTop;

        public string render;
        public string type;
        public string uri;
        public int duration;

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
        public int FileId { get; set; }

        //general options
        public string backgroundImage;
        public string backgroundColor;

        public MediaDictionary Dictionary;

        public DateTime LayoutModifiedDate { get; set; }

        public int PlayerWidth { get; set; }
        public int PlayerHeight { get; set; }

        private Ad ad;

        /// <summary>
        /// Audio associated with the widget
        /// </summary>
        public List<Media> Audio
        {
            get
            {
                if (_audio == null)
                    _audio = new List<Media>();

                return _audio;
            }
            set
            {
                _audio = value;
            }
        }
        private List<Media> _audio;

        /// <summary>
        /// Are statistics enabled
        /// </summary>
        public bool isStatEnabled;

        public bool IsPinchToZoomEnabled { get; set; }

        /// <summary>
        /// Decorate this Media Options with Region Options.
        /// </summary>
        /// <param name="regionOptions"></param>
        public void DecorateWithRegionOptions(RegionOptions regionOptions)
        {
            layoutId = regionOptions.layoutId;
            regionId = regionOptions.regionId;
            scheduleId = regionOptions.scheduleId;
            scaleFactor = regionOptions.scaleFactor;
            width = regionOptions.width;
            height = regionOptions.height;
            top = regionOptions.top;
            left = regionOptions.left;
            originalWidth = regionOptions.originalWidth;
            originalHeight = regionOptions.originalHeight;
            backgroundTop = regionOptions.backgroundTop;
            backgroundLeft = regionOptions.backgroundLeft;
            backgroundImage = regionOptions.backgroundImage;
            backgroundColor = regionOptions.backgroundColor;
            PlayerWidth = regionOptions.PlayerWidth;
            PlayerHeight = regionOptions.PlayerHeight;
            LayoutModifiedDate = regionOptions.LayoutModifiedDate;
        }

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("({0},{1},{2},{3},{4},{5})", width, height, top, left, type, uri);
        }

        public void SetAd(Ad ad)
        {
            this.ad = ad;
        }

        public Ad GetAd()
        {
            return this.ad;
        }
    }

    struct MediaOption
    {
        public string Name;
        public string Value;
    }
}