﻿/**
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
using System.IO;

namespace XiboClient.XmdsAgents
{
    class WidgetData
    {
        public int WidgetId;
        public int UpdateInterval;
        public bool ForceUpdate = false;
        public DateTime UpdatedDt;

        public string Path
        {
            get
            {
                return ApplicationSettings.Default.LibraryPath + @"\" + WidgetId + ".json";
            }
        }

        public bool IsUpToDate
        {
            get
            {
                // Does this data file already exist? and if so, is it sufficiently up to date.
                if (File.Exists(Path))
                {
                    UpdatedDt = File.GetLastWriteTime(Path);
                    return UpdatedDt > DateTime.Now.AddMinutes(-1 * UpdateInterval);
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
