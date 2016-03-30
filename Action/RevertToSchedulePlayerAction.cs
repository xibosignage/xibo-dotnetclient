using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XiboClient.Action
{
    class RevertToSchedulePlayerAction : PlayerActionInterface
    {
        public const string Name = "revertToSchedule";
            
        public string GetActionName()
        {
            return Name;
        }
    }
}
