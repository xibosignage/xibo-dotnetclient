using System;

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
