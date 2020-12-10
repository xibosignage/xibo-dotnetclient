/**
 * Copyright (C) 2020 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - http://www.xibo.org.uk
 *
 * This file is part of Xibo.
 *
 * Xibo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version.
 *
 * Xibo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with Xibo.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using XiboClient.Error;
using XiboClient.Logic;
using XiboClient.Stats;

namespace XiboClient.Rendering
{
    /// <summary>
    /// Interaction logic for Layout.xaml
    /// </summary>
    public partial class Layout : UserControl
    {
        /// <summary>
        /// The Schedule Object
        /// </summary>
        public Schedule Schedule;

        private double _layoutWidth;
        private double _layoutHeight;
        private double _scaleFactor;

        private bool isStatEnabled;

        /// <summary>
        /// Is this Layout Changing?
        /// </summary>
        private bool _isLayoutChanging = false;

        /// <summary>
        /// Regions for this Layout
        /// </summary>
        private List<Region> _regions;

        /// <summary>
        /// Last updated time of this Layout
        /// </summary>
        private DateTime layoutModifiedTime;

        /// <summary>
        /// The Background Color
        /// </summary>
        public Brush BackgroundColor { get; private set; }

        private int _layoutId;
        private bool isOverlay;
        private bool isInterrupt;

        public Guid UniqueId { get; private set; }
        public int ScheduleId { get; private set; }

        /// <summary>
        /// The schedule item representing this Layout
        /// </summary>
        public ScheduleItem ScheduleItem { get; private set; }

        // Layout state
        public bool IsRunning { get; set; }
        public bool IsPaused { get; set; }
        public bool IsExpired { get; private set; }

        // Interrupts
        private bool isPausePending = false;

        public delegate void OnReportLayoutPlayDuration(int scheduleId, int layoutId, double duration);
        public event OnReportLayoutPlayDuration OnReportLayoutPlayDurationEvent;

        /// <summary>
        /// Layout
        /// </summary>
        public Layout()
        {
            InitializeComponent();

            // Create a new empty collection of Regions
            _regions = new List<Region>();

            // Generate a new GUID
            UniqueId = Guid.NewGuid();
        }

        /// <summary>
        /// Load this Layout from its File
        /// </summary>
        /// <param name="scheduleItem"></param>
        public void loadFromFile(ScheduleItem scheduleItem)
        {
            // Store the Schedule and LayoutIds
            this.ScheduleItem = scheduleItem;
            this.ScheduleId = scheduleItem.scheduleid;
            this._layoutId = scheduleItem.id;
            this.isOverlay = scheduleItem.IsOverlay;
            this.isInterrupt = scheduleItem.IsInterrupt();

            // Get this layouts XML
            XmlDocument layoutXml = new XmlDocument();

            // try to open the layout file
            try
            {
                using (FileStream fs = File.Open(scheduleItem.layoutFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (XmlReader reader = XmlReader.Create(fs))
                    {
                        layoutXml.Load(reader);

                        reader.Close();
                    }
                    fs.Close();
                }
            }
            catch (IOException ioEx)
            {
                Trace.WriteLine(new LogMessage("MainForm - PrepareLayout", "IOException: " + ioEx.ToString()), LogType.Error.ToString());
                throw;
            }

            layoutModifiedTime = File.GetLastWriteTime(scheduleItem.layoutFile);

            // Attributes of the main layout node
            XmlNode layoutNode = layoutXml.SelectSingleNode("/layout");

            XmlAttributeCollection layoutAttributes = layoutNode.Attributes;

            // Set the background and size of the form
            _layoutWidth = int.Parse(layoutAttributes["width"].Value, CultureInfo.InvariantCulture);
            _layoutHeight = int.Parse(layoutAttributes["height"].Value, CultureInfo.InvariantCulture);

            // Are stats enabled for this Layout?
            isStatEnabled = (layoutAttributes["enableStat"] == null) ? true : (int.Parse(layoutAttributes["enableStat"].Value) == 1);

            // Scaling factor, will be applied to all regions
            _scaleFactor = Math.Min(Width / _layoutWidth, Height / _layoutHeight);

            // Want to be able to center this shiv - therefore work out which one of these is going to have left overs
            int backgroundWidth = (int)(_layoutWidth * _scaleFactor);
            int backgroundHeight = (int)(_layoutHeight * _scaleFactor);

            double leftOverX;
            double leftOverY;

            try
            {
                leftOverX = Math.Abs(Width - backgroundWidth);
                leftOverY = Math.Abs(Height - backgroundHeight);

                if (leftOverX != 0) leftOverX = leftOverX / 2;
                if (leftOverY != 0) leftOverY = leftOverY / 2;
            }
            catch
            {
                leftOverX = 0;
                leftOverY = 0;
            }

            // We know know what our Layout controls dimensions should be
            SetDimensions((int)leftOverX, (int)leftOverY, backgroundWidth, backgroundHeight);

            // New region and region options objects
            RegionOptions options = new RegionOptions();

            options.PlayerWidth = (int)Width;
            options.PlayerHeight = (int)Height;
            options.LayoutModifiedDate = layoutModifiedTime;

            // Deal with the color
            // unless we are an overlay, in which case don't put up a background at all
            if (!isOverlay)
            {
                this.BackgroundColor = Brushes.Black;
                try
                {
                    if (layoutAttributes["bgcolor"] != null && layoutAttributes["bgcolor"].Value != "")
                    {
                        var bc = new BrushConverter();
                        this.BackgroundColor = (Brush)bc.ConvertFrom(layoutAttributes["bgcolor"].Value);
                        options.backgroundColor = layoutAttributes["bgcolor"].Value;
                    }
                }
                catch
                {
                    // Default black
                    options.backgroundColor = "#000000";
                }

                // Get the background
                try
                {
                    if (layoutAttributes["background"] != null && !string.IsNullOrEmpty(layoutAttributes["background"].Value))
                    {
                        string bgFilePath = ApplicationSettings.Default.LibraryPath + @"\backgrounds\" + backgroundWidth + "x" + backgroundHeight + "_" + layoutAttributes["background"].Value;

                        // Create a correctly sized background image in the temp folder
                        if (!File.Exists(bgFilePath))
                        {
                            GenerateBackgroundImage(layoutAttributes["background"].Value, backgroundWidth, backgroundHeight, bgFilePath);
                        }

                        Background = new ImageBrush(new BitmapImage(new Uri(bgFilePath)));
                        options.backgroundImage = @"/backgrounds/" + backgroundWidth + "x" + backgroundHeight + "_" + layoutAttributes["background"].Value;
                    }
                    else
                    {
                        // Assume there is no background image
                        options.backgroundImage = "";
                        Background = this.BackgroundColor;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("MainForm - PrepareLayout", "Unable to set background: " + ex.Message), LogType.Error.ToString());

                    // Assume there is no background image
                    Background = this.BackgroundColor;
                    options.backgroundImage = "";
                }
            }

            // Get the regions
            XmlNodeList listRegions = layoutXml.SelectNodes("/layout/region");
            XmlNodeList listMedia = layoutXml.SelectNodes("/layout/region/media");

            // Check to see if there are any regions on this layout.
            if (listRegions.Count == 0 || listMedia.Count == 0)
            {
                Trace.WriteLine(new LogMessage("PrepareLayout",
                    string.Format("A layout with {0} regions and {1} media has been detected.", listRegions.Count.ToString(), listMedia.Count.ToString())),
                    LogType.Info.ToString());

                // Add this to our unsafe list.
                CacheManager.Instance.AddUnsafeItem(UnsafeItemType.Layout, _layoutId, ""+_layoutId, "No Regions or Widgets");

                throw new LayoutInvalidException("Layout without any Regions or Widgets");
            }

            // Parse the regions
            foreach (XmlNode region in listRegions)
            {
                // Is there any media
                if (region.ChildNodes.Count == 0)
                {
                    Debug.WriteLine("A region with no media detected");
                    continue;
                }

                // Region loop setting
                options.RegionLoop = false;

                XmlNode regionOptionsNode = region.SelectSingleNode("options");

                if (regionOptionsNode != null)
                {
                    foreach (XmlNode option in regionOptionsNode.ChildNodes)
                    {
                        if (option.Name == "loop" && option.InnerText == "1")
                            options.RegionLoop = true;
                    }
                }

                //each region
                XmlAttributeCollection nodeAttibutes = region.Attributes;

                options.scheduleId = ScheduleId;
                options.layoutId = _layoutId;
                options.regionId = nodeAttibutes["id"].Value.ToString();
                options.width = (int)(Convert.ToDouble(nodeAttibutes["width"].Value, CultureInfo.InvariantCulture) * _scaleFactor);
                options.height = (int)(Convert.ToDouble(nodeAttibutes["height"].Value, CultureInfo.InvariantCulture) * _scaleFactor);
                options.left = (int)(Convert.ToDouble(nodeAttibutes["left"].Value, CultureInfo.InvariantCulture) * _scaleFactor);
                options.top = (int)(Convert.ToDouble(nodeAttibutes["top"].Value, CultureInfo.InvariantCulture) * _scaleFactor);

                options.scaleFactor = _scaleFactor;

                // Store the original width and original height for scaling
                options.originalWidth = (int)Convert.ToDouble(nodeAttibutes["width"].Value, CultureInfo.InvariantCulture);
                options.originalHeight = (int)Convert.ToDouble(nodeAttibutes["height"].Value, CultureInfo.InvariantCulture);

                // Set the backgrounds (used for Web content offsets)
                options.backgroundLeft = options.left * -1;
                options.backgroundTop = options.top * -1;

                // All the media nodes for this region / layout combination
                options.mediaNodes = region.SelectNodes("media");

                Region temp = new Region();
                temp.DurationElapsedEvent += new Region.DurationElapsedDelegate(Region_DurationElapsedEvent);
                temp.MediaExpiredEvent += Region_MediaExpiredEvent;

                // ZIndex
                if (nodeAttibutes["zindex"] != null)
                {
                    temp.ZIndex = int.Parse(nodeAttibutes["zindex"].Value);
                }

                Debug.WriteLine("loadFromFile: Created new region", "Layout");

                // Dont be fooled, this innocent little statement kicks everything off
                temp.loadFromOptions(options);

                // Add to our list of Regions
                _regions.Add(temp);

                Debug.WriteLine("loadFromFile: Adding region", "Layout");
            }

            // Order all Regions by their ZIndex
            _regions.Sort((l, r) => l.ZIndex < r.ZIndex ? -1 : 1);

            // Add all Regions to the Scene
            foreach (Region temp in _regions)
            {
                // Add this Region to our Scene
                LayoutScene.Children.Add(temp);
            }

            // Null stuff
            listRegions = null;
            listMedia = null;
        }

        /// <summary>
        /// Start this Layout
        /// </summary>
        public void Start()
        {
            // Stat Start
            StatManager.Instance.LayoutStart(UniqueId, ScheduleId, _layoutId);

            // Start all regions
            foreach (Region region in _regions)
            {
                region.Start();
            }

            // We are running
            IsRunning = true;
            IsPaused = false;
        }

        /// <summary>
        /// Pause this Layout
        /// </summary>
        public void Pause()
        {
            // Pause each Region
            foreach (Region region in _regions)
            {
                region.Pause();
            }

            // Pause no-longer pending
            isPausePending = false;
            IsPaused = true;

            // Close and dispatch any stat records
            double duration = StatManager.Instance.LayoutStop(UniqueId, ScheduleId, _layoutId, this.isStatEnabled);

            // Report Play Duration
            if (this.isInterrupt)
            {
                OnReportLayoutPlayDurationEvent?.Invoke(ScheduleId, _layoutId, duration);
            }
        }

        /// <summary>
        /// Set Pause Pending, so that next expiry we pause.
        /// </summary>
        public void PausePending()
        {
            isPausePending = true;

            foreach(Region region in _regions)
            {
                region.PausePending();
            }
        }

        /// <summary>
        /// Resume this Layout
        /// </summary>
        public void Resume()
        {
            StatManager.Instance.LayoutStart(UniqueId, ScheduleId, _layoutId);

            // Resume each region
            foreach (Region region in _regions)
            {
                region.Resume(this.isInterrupt);
            }

            IsPaused = false;
        }

        /// <summary>
        /// Stop Layout
        /// </summary>
        public void Stop()
        {
            // Stat stop
            double duration = StatManager.Instance.LayoutStop(UniqueId, ScheduleId, _layoutId, this.isStatEnabled);

            // If we are an interrupt layout, then report our duration.
            if (this.isInterrupt)
            {
                OnReportLayoutPlayDurationEvent?.Invoke(ScheduleId, this._layoutId, duration);
            }

            // Stop
            IsPaused = false;
            IsRunning = false;
        }

        /// <summary>
        /// Remove tidies everything up.
        /// </summary>
        public void Remove()
        {
            if (_regions == null)
                return;

            lock (_regions)
            {
                foreach (Region region in _regions)
                {
                    try
                    {
                        // Clear the region
                        region.Clear();

                        // Remove the region from the list of controls
                        this.LayoutScene.Children.Remove(region);
                    }
                    catch (Exception e)
                    {
                        // If we can't dispose we should log to understand why
                        Trace.WriteLine(new LogMessage("Layout", "Remove: " + e.Message), LogType.Info.ToString());
                    }
                }

                _regions.Clear();
            }

            _regions = null;
        }

        /// <summary>
        /// The duration of a Region has been reached
        /// </summary>
        private void Region_DurationElapsedEvent()
        {
            Trace.WriteLine(new LogMessage("Layout", "DurationElapsedEvent: Region Elapsed"), LogType.Audit.ToString());

            // Are we already changing the layout?
            if (_isLayoutChanging)
            {
                Trace.WriteLine(new LogMessage("Layout", "DurationElapsedEvent: Already Changing Layout"), LogType.Audit.ToString());
                return;
            }

            // If we are paused, don't do anything
            if (IsPaused)
            {
                Debug.WriteLine("Region_DurationElapsedEvent: On Paused Layout, ignoring.", "Layout");
                return;
            }

            bool isExpired = true;

            // Check the other regions to see if they are also expired.
            foreach (Region temp in _regions)
            {
                if (!temp.IsExpired)
                {
                    isExpired = false;
                    break;
                }
            }

            // Set the Layout to expired
            if (isExpired)
            {
                this.IsExpired = true;
            }

            // If we are sure we have expired after checking all regions, then set the layout expired flag on them all
            // if we are an overlay, then don't raise this event
            if (isExpired && !this.isOverlay)
            {
                // Inform each region that the layout containing it has expired
                foreach (Region temp in _regions)
                {
                    temp.IsLayoutExpired = true;
                }

                Trace.WriteLine(new LogMessage("Region", "DurationElapsedEvent: All Regions have expired. Raising a Next layout event."), LogType.Audit.ToString());

                // We are changing the layout
                _isLayoutChanging = true;

                // Yield and restart
                Schedule.NextLayout();
            }
        }

        /// <summary>
        /// A media item has expired.
        /// </summary>
        private void Region_MediaExpiredEvent()
        {
            Trace.WriteLine(new LogMessage("Region", "MediaExpiredEvent: Media Elapsed"), LogType.Audit.ToString());

            // Are we supposed to be pausing?
            if (this.isPausePending)
            {
                Schedule.SetInterruptMediaPlayed();
            }
        }

        /// <summary>
        /// Generates a background image and saves it in the library for use later
        /// </summary>
        /// <param name="layoutAttributes"></param>
        /// <param name="backgroundWidth"></param>
        /// <param name="backgroundHeight"></param>
        /// <param name="bgFilePath"></param>
        private static void GenerateBackgroundImage(string sourceFile, int backgroundWidth, int backgroundHeight, string bgFilePath)
        {
            Trace.WriteLine(new LogMessage("MainForm - GenerateBackgroundImage", "Trying to generate a background image. It will be saved: " + bgFilePath), LogType.Audit.ToString());

            using (System.Drawing.Image img = System.Drawing.Image.FromFile(ApplicationSettings.Default.LibraryPath + @"\" + sourceFile))
            {
                using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(img, backgroundWidth, backgroundHeight))
                {
                    EncoderParameters encoderParameters = new EncoderParameters(1);
                    EncoderParameter qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
                    encoderParameters.Param[0] = qualityParam;

                    ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");

                    bmp.Save(bgFilePath, jpegCodec, encoderParameters);
                }
            }
        }

        /// <summary> 
        /// Returns the image codec with the given mime type 
        /// </summary> 
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            // Get image codecs for all image formats 
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

            // Find the correct image codec 
            for (int i = 0; i < codecs.Length; i++)
                if (codecs[i].MimeType == mimeType)
                    return codecs[i];
            return null;
        }

        /// <summary>
        /// Set Dimeniosn of this Control 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="top"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        private void SetDimensions(int left, int top, int width, int height)
        {
            Debug.WriteLine("Setting Dimensions to W:" + width + ", H:" + height + ", (" + left + "," + top + ")", "Layout");

            // Evaluate the width, etc
            Width = width;
            Height = height;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Margin = new Thickness(left, top, 0, 0);
        }
    }
}
