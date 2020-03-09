using System;

namespace XiboClient.Action
{
    class OverlayLayoutPlayerAction : PlayerActionInterface
    {
        public const string Name = "overlayLayout";

        public int layoutId;
        public int duration;
        public bool downloadRequired;
        public string createdDt;

        private Guid _id;

        /// <summary>
        /// Get Action Name
        /// </summary>
        /// <returns></returns>
        public string GetActionName()
        {
            return Name;
        }

        /// <summary>
        /// Layout change id
        /// </summary>
        /// <returns></returns>
        public Guid GetId()
        {
            if (_id == null || _id == Guid.Empty)
                _id = Guid.NewGuid();

            return _id;
        }

        /// <summary>
        /// Is a download required.
        /// </summary>
        /// <returns></returns>
        public bool IsDownloadRequired()
        {
            return downloadRequired;
        }

        /// <summary>
        /// Has this change action been serviced?
        /// </summary>
        /// <returns></returns>
        public bool IsServiced()
        {
            // Have we played for our entire duration.
            DateTime date = DateTime.Parse(createdDt);
            return (date.AddSeconds(duration) < DateTime.Now);
        }
    }
}
