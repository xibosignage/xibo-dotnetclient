/**
 * Copyright (C) 2022 Xibo Signage Ltd
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
using GeoJSON.Net.Contrib.MsSqlSpatial;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Microsoft.SqlServer.Types;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Diagnostics;
using System.Linq;

namespace XiboClient.Adspace
{
    public class Ad
    {
        public string Id;
        public string AdId;
        public string Title;
        public string CreativeId;
        public string Duration;
        public string Type;
        public string XiboType;
        public int Width;
        public int Height;

        public string AdTagUri;
        public string Url;
        public List<string> ImpressionUrls = new List<string>();
        public List<string> ErrorUrls = new List<string>();

        // Wrapper settings
        //  many of these come from Xibo specific extensions.
        public bool IsWrapper;
        public bool IsWrapperResolved = false;
        public bool IsWrapperOpenImmediately = false;
        public bool IsWrapperResolving = false;
        public int CountWraps = 0;

        public List<string> WrapperAllowedTypes = new List<string>();
        public string WrapperAllowedDuration;
        public string WrapperPartner;
        public string WrapperFileScheme = "creativeId";
        public string WrapperExtendUrl = "";
        public string WrapperHttpMethod = "GET";
        public int WrapperMaxDuration = 0;
        public int WrapperRateLimit = 0;

        public bool IsGeoAware = false;
        public string GeoLocation = "";

        public double AspectRatio
        {
            get
            {
                return (double)Width / Height;
            }
        }

        /// <summary>
        /// Get the duration in seconds
        /// </summary>
        /// <returns></returns>
        public int GetDuration()
        {
            // Duration is a string
            return (int)TimeSpan.Parse(Duration).TotalSeconds;
        }

        /// <summary>
        /// Get the duration in seconds
        /// </summary>
        /// <returns></returns>
        public int GetWrapperAllowedDuration()
        {
            return (int)TimeSpan.Parse(WrapperAllowedDuration).TotalSeconds;
        }

        public string GetFileName()
        {
            if (WrapperFileScheme == "fileName")
            {
                return "axe_" + Url.Split('/').Last();
            }
            else if (Type.StartsWith("video"))
            {
                // Workaround for MediaElement not supporting videos without an extension.
                string[] types = Type.Split('/');
                return "axe_" + CreativeId + "." + types[1];
            }
            else
            { 
                return "axe_" + CreativeId;
            }
        }

        /// <summary>
        /// Set whether or not this GeoSchedule is active.
        /// </summary>
        /// <param name="geoCoordinate"></param>
        /// <returns></returns>
        public bool IsGeoActive(GeoCoordinate geoCoordinate)
        {
            if (!IsGeoAware)
            {
                return true;
            }
            else if (geoCoordinate == null || geoCoordinate.IsUnknown)
            {
                return false;
            }
            else
            {
                try
                {
                    // Current location.
                    Point current = new Point(new Position(geoCoordinate.Latitude, geoCoordinate.Longitude));

                    // Test against the geo location
                    var geo = JsonConvert.DeserializeObject<Feature>(GeoLocation);

                    // Use SQL spatial helper to calculate intersection or not
                    SqlGeometry polygon = (geo.Geometry as Polygon).ToSqlGeometry();

                    return current.ToSqlGeometry().STIntersects(polygon).Value;
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("ScheduleItem", "SetIsGeoActive: Cannot parse geo location: e = " + e.Message), LogType.Audit.ToString());
                }
            }

            return false;
        }
    }
}
