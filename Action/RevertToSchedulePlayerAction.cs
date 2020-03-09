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
