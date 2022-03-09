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
using System.Xml;
using XiboClient.Logic;
using XiboClient.Rendering;

namespace XiboClient
{
    /// <summary>
    /// The options specific to a region
    ///     NOTE: Don't change this to a class
    /// </summary>
    public struct RegionOptions
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

        // Region Loop
        public bool RegionLoop;

        //The identification for this region
        public int layoutId;
        public string regionId;
        public int scheduleId;

        //general options
        public string backgroundImage;
        public string backgroundColor;

        public DateTime LayoutModifiedDate { get; set; }

        public int PlayerWidth { get; set; }
        public int PlayerHeight { get; set; }

        // Region out transition
        public string TransitionType;
        public int TransitionDuration;
        public string TransitionDirection;

        public override string ToString()
        {
            return string.Format("({0},{1},{2},{3},{4},{5})", width, height, top, left, layoutId, regionId);
        }
    }
}
