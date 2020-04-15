using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using GeoJSON.Net.Contrib.MsSqlSpatial;
using Microsoft.SqlServer.Types;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Diagnostics;

namespace XiboClient.Logic
{
    /// <summary>
    /// A LayoutSchedule
    /// </summary>
    [Serializable]
    public class ScheduleItem
    {
        public string NodeName;
        public string layoutFile;
        public int id;
        public int scheduleid;
        public Guid actionId;

        public int Priority;
        public bool Override;

        public DateTime FromDt;
        public DateTime ToDt;

        // Geo Schedule
        public bool IsGeoAware = false;
        public bool IsGeoActive = false;
        public string GeoLocation = "";

        public List<string> Dependents = new List<string>();

        public bool Refresh = false;

        /// <summary>
        /// Point we have tested against for GeoSchedule
        /// </summary>
        private Point testedAgainst;

        /// <summary>
        /// ToString
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("[{0}] From {1} to {2} with priority {3}. {4} dependents.", id, FromDt.ToString(), ToDt.ToString(), Priority, Dependents.Count);
        }

        public override int GetHashCode()
        {
            return id + scheduleid;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            ScheduleItem compare = obj as ScheduleItem;

            return id == compare.id &&
                scheduleid == compare.scheduleid &&
                FromDt.Ticks == compare.FromDt.Ticks &&
                ToDt.Ticks == compare.ToDt.Ticks &&
                actionId == compare.actionId
                ;
        }

        /// <summary>
        /// Set whether or not this GeoSchedule is active.
        /// </summary>
        /// <param name="geoCoordinate"></param>
        /// <returns></returns>
        public bool SetIsGeoActive(GeoCoordinate geoCoordinate)
        {
            if (!IsGeoAware)
            {
                IsGeoActive = false;
            } 
            else if (geoCoordinate == null || geoCoordinate.IsUnknown)
            {
                IsGeoActive = false;
            }
            else
            {
                try
                {
                    // Current location.
                    Point current = new Point(new Position(geoCoordinate.Latitude, geoCoordinate.Longitude));

                    // Have we already tested this?
                    if (this.testedAgainst == null || !testedAgainst.Equals(current))
                    {
                        // Not tested yet, or position changed.
                        this.testedAgainst = current;

                        // Test against the geo location
                        var geo = JsonConvert.DeserializeObject<Feature>(GeoLocation);

                        // Use SQL spatial helper to calculate intersection or not
                        SqlGeometry polygon = (geo.Geometry as Polygon).ToSqlGeometry();

                        IsGeoActive = current.ToSqlGeometry().STIntersects(polygon).Value;
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("ScheduleItem", "SetIsGeoActive: Cannot parse geo location: e = " + e.Message), LogType.Audit.ToString());
                }
            }

            return IsGeoActive;
        }
    }
}
