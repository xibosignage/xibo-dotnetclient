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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using XiboClient.Stats;

namespace XiboClient.Rendering
{
    /// <summary>
    /// Interaction logic for Layout.xaml
    /// </summary>
    public partial class Layout : UserControl
    {
        public Schedule Schedule;

        private double _layoutWidth;
        private double _layoutHeight;
        private double _scaleFactor;

        private bool isStatEnabled;
        private Stat _stat;

        /// <summary>
        /// Is this Layout Changing?
        /// </summary>
        public bool IsLayoutChanging = false;

        /// <summary>
        /// Regions for this Layout
        /// </summary>
        private List<Region> _regions;

        /// <summary>
        /// Last updated time of this Layout
        /// </summary>
        private DateTime layoutModifiedTime;

        private int _layoutId;
        private int _scheduleId;
        private bool isOverlay;

        public int ScheduleId { get { return _scheduleId; } }

        /// <summary>
        /// Event to signify that this Layout's duration has elapsed
        /// </summary>
        public delegate void DurationElapsedDelegate();

        public Layout()
        {
            InitializeComponent();

            // Create a new empty collection of Regions
            _regions = new List<Region>();
        }

        /// <summary>
        /// Load this Layout from its File
        /// </summary>
        /// <param name="layoutPath"></param>
        /// <param name="layoutId"></param>
        /// <param name="scheduleId"></param>
        /// <param name="isOverlay"></param>
        public void loadFromFile(string layoutPath, int layoutId, int scheduleId, bool isOverlay)
        {
            // Store the Schedule and LayoutIds
            this._scheduleId = scheduleId;
            this._layoutId = layoutId;
            this.isOverlay = isOverlay;

            // Get this layouts XML
            XmlDocument layoutXml = new XmlDocument();

            // try to open the layout file
            try
            {
                using (FileStream fs = File.Open(layoutPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

            layoutModifiedTime = File.GetLastWriteTime(layoutPath);

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

            // New region and region options objects
            RegionOptions options = new RegionOptions();

            options.LayoutModifiedDate = layoutModifiedTime;
            options.LayoutSize = new System.Drawing.Size()
            {
                Width = (int)this.Width,
                Height = (int)this.Height
            };

            // Deal with the color
            // unless we are an overlay, in which case don't put up a background at all
            if (!isOverlay)
            {
                Brush backgroundColour = Brushes.Black;
                try
                {
                    if (layoutAttributes["bgcolor"] != null && layoutAttributes["bgcolor"].Value != "")
                    {
                        var bc = new BrushConverter();
                        backgroundColour = (Brush)bc.ConvertFrom(layoutAttributes["bgcolor"].Value);
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
                        Background = backgroundColour;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("MainForm - PrepareLayout", "Unable to set background: " + ex.Message), LogType.Error.ToString());

                    // Assume there is no background image
                    Background = backgroundColour;
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

                if (Schedule.ActiveLayouts == 1)
                {
                    Trace.WriteLine(new LogMessage("PrepareLayout", "Only 1 layout scheduled and it has nothing to show."), LogType.Info.ToString());

                    throw new Exception("Only 1 layout schduled and it has nothing to show");
                }
                else
                {
                    Trace.WriteLine(new LogMessage("PrepareLayout",
                        string.Format(string.Format("An empty layout detected, will show for {0} seconds.", ApplicationSettings.Default.EmptyLayoutDuration.ToString()))), LogType.Info.ToString());

                    // Put a small dummy region in place, with a small dummy media node - which expires in 10 seconds.
                    XmlDocument dummyXml = new XmlDocument();
                    dummyXml.LoadXml(string.Format("<region id='blah' width='1' height='1' top='1' left='1'><media id='blah' type='text' duration='{0}'><raw><text></text></raw></media></region>",
                        ApplicationSettings.Default.EmptyLayoutDuration.ToString()));

                    // Replace the list of regions (they mean nothing as they are empty)
                    listRegions = dummyXml.SelectNodes("/region");
                }
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

                options.scheduleId = _scheduleId;
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

                // Account for scaling
                options.left = options.left + (int)leftOverX;
                options.top = options.top + (int)leftOverY;

                // All the media nodes for this region / layout combination
                options.mediaNodes = region.SelectNodes("media");

                Region temp = new Region();
                temp.DurationElapsedEvent += new Region.DurationElapsedDelegate(Region_DurationElapsedEvent);
                
                // ZIndex
                if (nodeAttibutes["zindex"] != null)
                {
                    temp.ZIndex = int.Parse(nodeAttibutes["zindex"].Value);
                }

                Debug.WriteLine("Created new region", "MainForm - Prepare Layout");

                // Dont be fooled, this innocent little statement kicks everything off
                temp.loadFromOptions(options);

                // Add to our list of Regions
                _regions.Add(temp);

                Debug.WriteLine("Adding region", "MainForm - Prepare Layout");
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

        public void Start()
        {
            // Create a start record for this layout
            _stat = new Stat();
            _stat.type = StatType.Layout;
            _stat.scheduleID = _scheduleId;
            _stat.layoutID = _layoutId;
            _stat.fromDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _stat.isEnabled = isStatEnabled;

            foreach (Region region in _regions)
            {
                region.Start();
            }
        }

        public void Stop()
        {
            if (_stat != null)
            {
                // Log the end of the currently running layout.
                _stat.toDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Record this stat event in the statLog object
                StatLog.Instance.RecordStat(_stat);
            }
        }

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
            Trace.WriteLine(new LogMessage("MainForm - DurationElapsedEvent", "Region Elapsed"), LogType.Audit.ToString());

            // Are we already changing the layout?
            if (IsLayoutChanging)
            {
                Trace.WriteLine(new LogMessage("MainForm - DurationElapsedEvent", "Already Changing Layout"), LogType.Audit.ToString());
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

            // If we are sure we have expired after checking all regions, then set the layout expired flag on them all
            // if we are an overlay, then don't raise this event
            if (isExpired && !this.isOverlay)
            {
                // Inform each region that the layout containing it has expired
                foreach (Region temp in _regions)
                {
                    temp.IsLayoutExpired = true;
                }

                Trace.WriteLine(new LogMessage("MainForm - DurationElapsedEvent", "All Regions have expired. Raising a Next layout event."), LogType.Audit.ToString());

                // We are changing the layout
                IsLayoutChanging = true;

                // Yield and restart
                Schedule.NextLayout();
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
    }
}
