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

        public bool Priority;
        public bool Override;

        public DateTime FromDt;
        public DateTime ToDt;

        public List<string> Dependents = new List<string>();
    }
}
