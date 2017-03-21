using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XiboClient.Action
{
    class LayoutChangePlayerAction : PlayerActionInterface
    {
        public const string Name = "changeLayout";

        public int layoutId;
        public int duration;
        public bool downloadRequired;
        public string changeMode;
        public string createdDt;

        private int _playCount;
        private Guid _id;

        public LayoutChangePlayerAction()
        {
            _playCount = 0;
        }

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
        /// Set that this action has been played
        /// </summary>
        public void SetPlayed()
        {
            _playCount++;
        }

        /// <summary>
        /// Has this change action been serviced?
        /// </summary>
        /// <returns></returns>
        public bool IsServiced()
        {
            if (duration == 0)
            {
                return (_playCount > 0);
            }
            else
            {
                // Have we played for our entire duration.
                DateTime date = DateTime.Parse(createdDt);
                return (date.AddSeconds(duration) < DateTime.Now);
            }
        }
    }
}
