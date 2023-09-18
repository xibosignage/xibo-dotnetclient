using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiboClient.Action
{
    class DataUpdatePlayerAction : PlayerActionInterface
    {
        public const string Name = "dataUpdate";

        public int widgetId;

        public string GetActionName()
        {
            return Name;
        }
    }
}
