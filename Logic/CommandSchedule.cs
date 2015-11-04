using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XiboClient.Logic
{
    public class CommandSchedule
    {
        public DateTime Date { get; set; }
        public String Code { get; set; }
        public Command Command { get; set; }
        public int ScheduleId { get; set; }

        private bool _run = false;
        public bool HasRun
        {
            get
            {
                return _run;
            }
            set
            {
                _run = value;
            }
        }
    }
}
