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
using System.Device.Location;
using System.Diagnostics;

namespace XiboClient.Helpers
{
    class GeoHelper
    {
        /// <summary>
        /// Is the provided geoJson inside the provided point
        /// </summary>
        /// <param name="geoJson"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static bool IsGeoInPoint(string geoJson, Point point)
        {
            try
            {
                // Test against the geo location
                var geo = JsonConvert.DeserializeObject<Feature>(geoJson);

                // Use SQL spatial helper to calculate intersection or not
                SqlGeometry polygon = (geo.Geometry as Polygon).ToSqlGeometry();

                return point.ToSqlGeometry().STIntersects(polygon).Value;
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("GeoHelper", "IsGeoInPoint: Cannot parse geo location: e = " + e.Message), LogType.Audit.ToString());

                return false;
            }
        }

        /// <summary>
        /// Is the provided geoJson inside the provided point, denoted by a location
        /// </summary>
        /// <param name="geoJson"></param>
        /// <param name="geoCoordinate"></param>
        /// <returns></returns>
        public static bool IsGeoInPoint(string geoJson, GeoCoordinate geoCoordinate)
        {
            return IsGeoInPoint(geoJson, new Point(new Position(geoCoordinate.Latitude, geoCoordinate.Longitude)));
        }
    }
}
