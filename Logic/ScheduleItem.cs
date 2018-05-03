using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public List<string> Dependents = new List<string>();

        public bool Refresh = false;

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
    }
}
