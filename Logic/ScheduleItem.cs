using GeoJSON.Net.Contrib.MsSqlSpatial;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
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

        /// <summary>
        /// Is this an Overlay?
        /// </summary>
        public bool IsOverlay = false;

        public DateTime FromDt;
        public DateTime ToDt;

        /// <summary>
        /// Share of Voice expressed in seconds per hour
        /// Interrupt Layouts
        /// </summary>
        public int ShareOfVoice;

        /// <summary>
        /// Seconds Played
        /// </summary>
        public double SecondsPlayed;

        // Geo Schedule
        public bool IsGeoAware = false;
        public bool IsGeoActive = false;
        public string GeoLocation = "";

        /// <summary>
        /// Dependent items
        /// </summary>
        public List<string> Dependents = new List<string>();

        /// <summary>
        /// Refresh this item - used for Overlays
        /// </summary>
        public bool Refresh = false;

        /// <summary>
        /// Is this schedule item fulfilled - used for Interrupts
        /// </summary>
        public bool IsFulfilled = false;

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

        /// <summary>
        /// Hash Code
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return id + scheduleid;
        }

        /// <summary>
        /// Equals
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
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
        /// Splash Screen Schedule Item
        /// </summary>
        /// <returns></returns>
        public static ScheduleItem Splash()
        {
            return new ScheduleItem
            {
                id = 0,
                scheduleid = 0
            };
        }

        /// <summary>
        /// Is this the splash screen?
        /// </summary>
        /// <returns>true if splash</returns>
        public bool IsSplash()
        {
            return this.id == 0 && this.scheduleid == 0;
        }

        /// <summary>
        /// Is this an interrupt layout?
        /// </summary>
        /// <returns>true if shareOfVoice > 0</returns>
        public bool IsInterrupt()
        {
            return this.ShareOfVoice > 0;
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

        /// <summary>
        /// Calculate a Rank for this Item
        /// </summary>
        /// <param name="secondsToPeriodEnd"></param>
        /// <returns></returns>
        public double CalculateRank(int secondsToPeriodEnd)
        {
            if (ShareOfVoice <= 0 || SecondsPlayed >= ShareOfVoice)
            {
                return 0;
            }
            else
            {
                double completeDifficulty = (ShareOfVoice - SecondsPlayed) / Convert.ToDouble(ShareOfVoice);
                double scheduleDifficulty = (secondsToPeriodEnd - RemainingScheduledTime()) / secondsToPeriodEnd;

                return completeDifficulty + scheduleDifficulty;
            }
        }

        /// <summary>
        /// Get remaining scheduled time in seconds
        /// </summary>
        /// <returns></returns>
        public double RemainingScheduledTime()
        {
            DateTime now = DateTime.Now;
            DateTime endOfHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
            DateTime endOfScheduleOrHour = (endOfHour > ToDt) ? ToDt : endOfHour;

            return (endOfScheduleOrHour - now).TotalSeconds;
        }
    }
}
