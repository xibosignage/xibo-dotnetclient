namespace XiboClient.Rendering
{
    class PowerPoint : WebIe
    {
        public PowerPoint(RegionOptions options) : base(options)
        {
            // We are a normal WebIe control, opened natively
            options.Dictionary.Replace("modeid", "1");
        }
    }
}
