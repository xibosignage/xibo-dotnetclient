using GeoJSON.Net.Contrib.MsSqlSpatial;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Microsoft.SqlServer.Types;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Diagnostics;
using XiboClient.Helpers;

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

        /// <summary>
        /// The date/times this item is active from/to
        /// </summary>
        public DateTime FromDt;
        public DateTime ToDt;

        /// <summary>
        /// Share of Voice expressed in seconds per hour
        /// Interrupt Layouts
        /// </summary>
        public int ShareOfVoice;

        /// <summary>
        /// Is this schedule item an adspace exchange item
        /// </summary>
        public bool IsAdspaceExchange = false;

        /// <summary>
        /// The duration of this event
        /// </summary>
        public int Duration;

        // Geo Schedule
        public bool IsGeoAware = false;
        public bool IsGeoActive = false;
        public string GeoLocation = "";

        // Cycle Playback
        public bool IsCyclePlayback = false;
        public string CycleGroupKey = "";
        public int CyclePlayCount = 0;
        public List<ScheduleItem> CycleScheduleItems = new List<ScheduleItem>();

        // Max Plays
        public int MaxPlaysPerHour = 0;

        /// <summary>
        /// Dependent items
        /// </summary>
        public List<string> Dependents = new List<string>();

        /// <summary>
        /// Refresh this item - used for Overlays
        /// </summary>
        public bool Refresh = false;

        /// <summary>
        /// Point we have tested against for GeoSchedule
        /// </summary>
        private Point testedAgainst;

        /// <summary>
        /// Duration committed
        /// </summary>
        private int durationCommitted = 0;

        /// <summary>
        /// ToString
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "[" + id + "] "
                + (IsInterrupt() ? "(I) " : " ")
                + "P" + Priority
                ;
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
                scheduleid = 0,
                Duration = 10
            };
        }

        /// <summary>
        /// Create a schedule item for adspace exchange
        /// </summary>
        /// <param name="duration"></param>
        /// <param name="shareOfVoice"></param>
        /// <returns></returns>
        public static ScheduleItem CreateForAdspaceExchange(int duration, int shareOfVoice)
        {
            return new ScheduleItem
            {
                id = -1,
                IsAdspaceExchange = true,
                ShareOfVoice = shareOfVoice,
                Duration = duration,
                FromDt = DateTime.MinValue,
                ToDt = DateTime.MaxValue,
                layoutFile = "axe",
                IsCyclePlayback = false
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
                // Current location.
                Point current = new Point(new Position(geoCoordinate.Latitude, geoCoordinate.Longitude));

                // Have we already tested this?
                if (this.testedAgainst == null || !testedAgainst.Equals(current))
                {
                    // Not tested yet, or position changed.
                    this.testedAgainst = current;

                    // Test
                    IsGeoActive = GeoHelper.IsGeoInPoint(GeoLocation, current);
                }
            }

            return IsGeoActive;
        }

        /// <summary>
        /// Add to the committed duration
        /// </summary>
        /// <param name="duration"></param>
        public void AddCommittedDuration(int duration)
        {
            this.durationCommitted += duration;
        }

        /// <summary>
        /// Is the duration requested satisfied?
        /// </summary>
        /// <returns></returns>
        public bool IsDurationSatisfied()
        {
            return this.durationCommitted >= this.ShareOfVoice;
        }

        /// <summary>
        /// Reset the committed duration for another pass.
        /// </summary>
        public void ResetCommittedDuration()
        {
            this.durationCommitted = 0;
        }
    }
}
