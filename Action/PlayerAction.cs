using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XiboClient.Action
{
    class PlayerAction : PlayerActionInterface
    {
        public string action;

        public DateTime createdDt;
        public int ttl;

        public String GetActionName()
        {
            return action;
        }
    }
}
