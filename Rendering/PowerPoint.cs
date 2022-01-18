using System;
using System.IO;

namespace XiboClient.Rendering
{
    class PowerPoint : WebIe
    {
        public PowerPoint(MediaOptions options) : base(options)
        {
            // Check if PowerPoint is enabled
            if (!ApplicationSettings.Default.PowerpointEnabled)
            {
                CacheManager.Instance.AddUnsafeItem(UnsafeItemType.Media, UnsafeFaultCodes.PowerPointNotAvailable, options.layoutId, options.mediaid, "PowerPoint not enabled on this Display", 300);
                throw new Exception("PowerPoint not enabled on this Display");
            }

            // We are a normal WebIe control, opened natively
            options.Dictionary.Replace("modeid", "1");
            
            _filePath = ApplicationSettings.Default.EmbeddedServerAddress + Path.GetFileName(_filePath);
        }

        /// <summary>
        /// Is this a native open widget
        /// </summary>
        /// <returns></returns>
        protected override bool IsNativeOpen()
        {
            return true;
        }
    }
}
