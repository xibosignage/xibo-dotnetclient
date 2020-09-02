namespace XiboClient.Rendering
{
    class PowerPoint : WebIe
    {
        public PowerPoint(RegionOptions options) : base(options)
        {
            // We are a normal WebIe control, opened natively
            options.Dictionary.Replace("modeid", "1");
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
