/**
 * Copyright (C) 2022 Xibo Signage Ltd
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
using XiboClient.Adspace;
using XiboClient.Error;
using XiboClient.Log;
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
        /// Actions for this Layout
        /// </summary>
        private List<Action.Action> _actions;

        /// <summary>
        /// The Drawer of interactive widgets
        /// </summary>
        private XmlNodeList _drawer;

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
        public bool IsExpired { get; private set; }

        /// <summary>
        /// Event to indicate that a Layout has stopped.
        /// </summary>
        public delegate void OnLayoutStoppedDelegate(Layout layout);
        public event OnLayoutStoppedDelegate OnLayoutStopped;

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
        /// Load this Layout from its file
        /// </summary>
        /// <param name="scheduleItem"></param>
        public void LoadFromFile(ScheduleItem scheduleItem)
        {
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

            LoadFromFile(scheduleItem, layoutXml, File.GetLastWriteTime(scheduleItem.layoutFile));
        }

        /// <summary>
        /// Load this Layout from its XML
        /// </summary>
        /// <param name="scheduleItem"></param>
        public void LoadFromFile(ScheduleItem scheduleItem, XmlDocument layoutXml, DateTime modifiedDt)
        {
            // Store the Schedule and LayoutIds
            this.ScheduleItem = scheduleItem;
            this.ScheduleId = scheduleItem.scheduleid;
            this._layoutId = scheduleItem.id;
            this.isOverlay = scheduleItem.IsOverlay;
            this.isInterrupt = scheduleItem.IsInterrupt();
            layoutModifiedTime = modifiedDt;

            // Attributes of the main layout node
            XmlNode layoutNode = layoutXml.SelectSingleNode("/layout");

            XmlAttributeCollection layoutAttributes = layoutNode.Attributes;

            // Set the background and size of the form
            _layoutWidth = int.Parse(layoutAttributes["width"].Value, CultureInfo.InvariantCulture);
            _layoutHeight = int.Parse(layoutAttributes["height"].Value, CultureInfo.InvariantCulture);

            // Are stats enabled for this Layout?
            isStatEnabled = (layoutAttributes["enableStat"] == null) || (int.Parse(layoutAttributes["enableStat"].Value) == 1);

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

            // Parse any Actions
            try
            {
                _actions = Action.Action.CreateFromXmlNodeList(layoutXml.SelectNodes("/layout/action"));
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Layout", "loadFromFile: unable to load actions. e = " + e.Message), LogType.Info.ToString());
            }

            // New region and region options objects
            RegionOptions options = new RegionOptions
            {
                PlayerWidth = (int)Width,
                PlayerHeight = (int)Height,
                LayoutModifiedDate = layoutModifiedTime
            };

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
            int countMedia = layoutXml.SelectNodes("/layout/region/media").Count;
            _drawer = layoutXml.SelectNodes("/layout/drawer/media");

            // Drawer actions
            try
            {
                foreach (XmlNode drawerItem in _drawer)
                {
                    _actions.AddRange(Action.Action.CreateFromXmlNodeList(drawerItem.SelectNodes("action"), true));
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Layout", "loadFromFile: unable to load drawer actions. e = " + e.Message), LogType.Info.ToString());
            }

            // Check to see if there are any regions on this layout.
            if (listRegions.Count == 0 || countMedia == 0)
            {
                Trace.WriteLine(new LogMessage("PrepareLayout",
                    string.Format("A layout with {0} regions and {1} media has been detected.", listRegions.Count.ToString(), countMedia)),
                    LogType.Info.ToString());

                // Add this to our unsafe list.
                CacheManager.Instance.AddUnsafeItem(UnsafeItemType.Layout, UnsafeFaultCodes.XlfNoContent, _layoutId, ""+_layoutId, "No Regions or Widgets");

                throw new LayoutInvalidException("Layout without any Regions or Widgets");
            }

            // Parse the regions
            int maxLayer = 0;
            foreach (XmlNode region in listRegions)
            {
                // Is there any media
                if (region.ChildNodes.Count == 0)
                {
                    Debug.WriteLine("A region with no media detected");
                    continue;
                }

                // Region options: loop, transitions.
                options.RegionLoop = false;

                XmlNode regionOptionsNode = region.SelectSingleNode("options");

                if (regionOptionsNode != null)
                {
                    foreach (XmlNode option in regionOptionsNode.ChildNodes)
                    {
                        if (option.Name == "loop" && option.InnerText == "1")
                        {
                            options.RegionLoop = true;
                        }
                        else if (option.Name == "transitionType")
                        {
                            options.TransitionType = option.InnerText;
                        }
                        else if (option.Name == "transitionDuration" && !string.IsNullOrEmpty(option.InnerText))
                        {
                            try
                            {
                                options.TransitionDuration = int.Parse(option.InnerText);
                            }
                            catch
                            {
                                options.TransitionDuration = 2000;
                            }
                        }
                        else if (option.Name == "transitionDirection")
                        {
                            options.TransitionDirection = option.InnerText;
                        }
                    }
                }

                // Each region
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

                // Adjust our left/top to take into account any centering we've done (options left/top are with respect to this layout control,
                // which has already been moved).
                int actionLeft = options.left + (int)leftOverX;
                int actionTop = options.top + (int)leftOverY;

                // All the media nodes for this region / layout combination
                Dictionary<string, List<XmlNode>> parsedMedia = new Dictionary<string, List<XmlNode>>
                {
                    { "flat", new List<XmlNode>() }
                };

                // Cycle based playback
                // --------------------
                // Are any of this Region's media nodes enabled for cycle playback, and if so, which of the media nodes should we
                // add to the node list we provide to the region.
                foreach (XmlNode media in region.SelectNodes("media"))
                {
                    bool isCyclePlayback = XmlHelper.GetAttrib(media, "cyclePlayback", "0") == "1";

                    if (isCyclePlayback)
                    {
                        string groupKey = XmlHelper.GetAttrib(media, "parentWidgetId", "0");
                        
                        if (!parsedMedia.ContainsKey(groupKey))
                        {
                            parsedMedia.Add(groupKey, new List<XmlNode>());

                            // Add the first one of these to retain our place
                            // this will get swapped out
                            parsedMedia["flat"].Add(media);
                        }
                        parsedMedia[groupKey].Add(media);
                    }
                    else
                    {
                        parsedMedia["flat"].Add(media);
                    }
                }

                List<XmlNode> mediaNodes = new List<XmlNode>();

                // Process the resulting flat list
                foreach (XmlNode media in parsedMedia["flat"])
                {
                    // Is this a cycle based playback node?
                    bool isCyclePlayback = XmlHelper.GetAttrib(media, "cyclePlayback", "0") == "1";

                    if (isCyclePlayback)
                    {
                        // Yes, so replace it with the correct node from our corresponding list
                        string groupKey = XmlHelper.GetAttrib(media, "parentWidgetId", "0");
                        bool isRandom = XmlHelper.GetAttrib(media, "isRandom", "0") == "1";
                        int playCount = int.Parse(XmlHelper.GetAttrib(media, "playCount", "1"));

                        // This defaults to 0 if we're the first time here.
                        int sequence = ClientInfo.Instance.GetWidgetGroupSequence(groupKey);

                        if (ClientInfo.Instance.GetWidgetGroupPlaycount(groupKey) >= playCount)
                        {
                            // Plays of the current widget have been met, so pick a new one.
                            if (isRandom)
                            {
                                // If we are random, then just pick a random number between 0 and the number of widgets
                                sequence = new Random().Next(0, (parsedMedia[groupKey].Count - 1));
                            }
                            else
                            {
                                // Sequential
                                sequence++;
                                if (sequence >= parsedMedia[groupKey].Count)
                                {
                                    sequence = 0;
                                }
                            }

                            // Set the group sequence (also sets the play count to 1)
                            ClientInfo.Instance.SetWidgetGroupSequence(groupKey, sequence);
                        }
                        else
                        {
                            // Take the same one again (do not adjust sequence)
                            // Bump plays
                            ClientInfo.Instance.IncrementWidgetGroupPlaycount(groupKey);
                        }

                        // Pull out the appropriate widget
                        mediaNodes.Add(parsedMedia[groupKey][sequence]);
                    }
                    else
                    {
                        // Take it as is.
                        mediaNodes.Add(media);
                    }
                }

                // Pull out any actions
                try
                {
                    // Region Actions
                    _actions.AddRange(Action.Action.CreateFromXmlNodeList(region.SelectNodes("action"),
                        actionTop, actionLeft, options.width, options.height));

                    // Widget Actions
                    foreach (XmlNode media in mediaNodes)
                    {
                        List<Action.Action> mediaActions = Action.Action.CreateFromXmlNodeList(media.SelectNodes("action"),
                            actionTop, actionLeft, options.width, options.height);

                        if (mediaActions.Count > 0)
                        {
                            _actions.AddRange(mediaActions);
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("Layout", "loadFromFile: unable to load media actions. e = " + e.Message), LogType.Info.ToString());
                }

                Region temp = new Region();
                temp.DurationElapsedEvent += new Region.DurationElapsedDelegate(Region_DurationElapsedEvent);
                temp.MediaExpiredEvent += Region_MediaExpiredEvent;
                temp.OnRegionStopped += Region_OnRegionStopped;
                temp.TriggerWebhookEvent += Region_TriggerWebhookEvent;

                // ZIndex
                try
                {
                    temp.ZIndex = int.Parse(XmlHelper.GetAttrib(region, "zindex", "0"));
                }
                catch
                {
                    // Use the ordering of this region as the z-index
                    temp.ZIndex = maxLayer + 1;
                }
                maxLayer = Math.Max(temp.ZIndex, maxLayer);

                Debug.WriteLine("loadFromFile: Created new region", "Layout");

                // Load our region
                temp.LoadFromOptions(options.regionId, options, mediaNodes, actionTop, actionLeft);

                // Add to our list of Regions
                _regions.Add(temp);

                Debug.WriteLine("loadFromFile: Adding region", "Layout");
            }

            // Order all Actions by their Source
            _actions.Sort((l, r) => Action.Action.PriorityForActionSource(l.Source) < Action.Action.PriorityForActionSource(r.Source) ? -1 : 1);

            // Order all Regions by their ZIndex
            _regions.Sort((l, r) => l.ZIndex.CompareTo(r.ZIndex));

            // Add all Regions to the Scene
            foreach (Region temp in _regions)
            {
                // Add this Region to our Scene
                LayoutScene.Children.Add(temp);
            }

            // Null stuff
            listRegions = null;
        }

        /// <summary>
        /// Load this Layout from the Ad provided.
        /// </summary>
        /// <param name="scheduleItem"></param>
        /// <param name="ad"></param>
        public void LoadFromAd(ScheduleItem scheduleItem, Ad ad)
        {
            // Create an XLF representing this ad.
            XmlDocument document = new XmlDocument();
            XmlElement layout = document.CreateElement("layout");
            XmlElement region = document.CreateElement("region");
            XmlElement media = document.CreateElement("media");
            XmlElement mediaOptions = document.CreateElement("options");
            XmlElement urlOption = document.CreateElement("uri");

            // Layout properties
            layout.SetAttribute("width", "" + Width);
            layout.SetAttribute("height", "" + Height);
            layout.SetAttribute("bgcolor", "#000000");
            layout.SetAttribute("enableStat", "0");

            // Region properties
            region.SetAttribute("id", "axe");
            region.SetAttribute("width", "" + Width);
            region.SetAttribute("height", "" + Height);
            region.SetAttribute("top", "0");
            region.SetAttribute("left", "0");

            // Media properties
            media.SetAttribute("type", ad.XiboType);
            media.SetAttribute("id", Guid.NewGuid().ToString());
            media.SetAttribute("duration", "" + ad.GetDuration());
            media.SetAttribute("enableStat", "0");

            // Url
            urlOption.InnerText = ad.GetFileName();

            // Add all these nodes to the docs
            mediaOptions.AppendChild(urlOption);
            media.AppendChild(mediaOptions);
            region.AppendChild(media);
            layout.AppendChild(region);
            document.AppendChild(layout);

            // Pass this XML document to our usual load method
            LoadFromFile(scheduleItem, document, DateTime.Now);

            // Set our impression URLs which we will call on stop.
            _regions[0].SetAdspaceExchangeImpressionUrls(ad.ImpressionUrls);
            _regions[0].SetAdspaceExchangeErrorUrls(ad.ErrorUrls);
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
        }

        /// <summary>
        /// Stop Layout
        /// </summary>
        public void Stop()
        {
            LogMessage.Trace("Layout", "Stop", "Stopping: " + UniqueId);

            // Stat stop
            double duration = StatManager.Instance.LayoutStop(UniqueId, ScheduleId, _layoutId, this.isStatEnabled);

            // Record final duration of this layout in memory cache
            CacheManager.Instance.RecordLayoutDuration(_layoutId, (int)Math.Ceiling(duration));

            // Stop each region and let their transitions play out (if any)
            lock (_regions)
            {
                foreach (Region region in _regions)
                {
                    try
                    {
                        region.Stop();
                    }
                    catch (Exception e)
                    {
                        // If we can't dispose we should log to understand why
                        Trace.WriteLine(new LogMessage("Layout", "Remove: " + e.Message), LogType.Info.ToString());

                        this.LayoutScene.Children.Remove(region);
                    }
                }
            }

            IsRunning = false;

            // Record max plays per hour
            if (ScheduleItem.MaxPlaysPerHour > 0)
            {
                CacheManager.Instance.IncrementPlaysPerHour(ScheduleId);

                if (CacheManager.Instance.GetPlaysPerHour(ScheduleId) >= ScheduleItem.MaxPlaysPerHour)
                {
                    LogMessage.Trace("Layout", "Stop", "Waking up schedule manager as max players per hour exceeded");
                    Schedule.WakeUpScheduleManager();
                }
            }
        }

        /// <summary>
        /// Remove tidies everything up.
        /// </summary>
        public void Remove()
        {
            Debug.WriteLine("Remove: " + UniqueId, "Layout");

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
                        region.DurationElapsedEvent -= Region_DurationElapsedEvent;
                        region.MediaExpiredEvent -= Region_MediaExpiredEvent;
                        region.OnRegionStopped -= Region_OnRegionStopped;
                        region.TriggerWebhookEvent -= Region_TriggerWebhookEvent;

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
        }

        /// <summary>
        /// Move to the previous item in the named Region
        /// </summary>
        /// <param name="regionId"></param>
        public void RegionPrevious(string regionId)
        {
            GetRegionById(regionId).Previous();
        }

        /// <summary>
        /// Move to the next item in the named Region
        /// </summary>
        /// <param name="regionId"></param>
        public void RegionNext(string regionId)
        {
            GetRegionById(regionId).Next();
        }

        /// <summary>
        /// Extend the current widget's duration
        /// </summary>
        /// <param name="regionId"></param>
        /// <param name="duration"></param>
        public void RegionExtend(string regionId, int duration)
        {
            GetRegionById(regionId).ExtendCurrentWidgetDuration(duration);
        }

        /// <summary>
        /// Set the current widget's duration
        /// </summary>
        /// <param name="regionId"></param>
        /// <param name="duration"></param>
        public void RegionSetDuration(string regionId, int duration)
        {
            GetRegionById(regionId).SetCurrentWidgetDuration(duration);
        }

        /// <summary>
        /// Change the Widget in the provided region
        /// </summary>
        /// <param name="regionId"></param>
        /// <param name="widgetId"></param>
        public void RegionChangeToWidget(string regionId, int widgetId)
        {
            // Get the XmlNode associated with this Widget.
            Region region = GetRegionById(regionId);
            region.NavigateToWidget(GetWidgetFromDrawer(widgetId));

            // Update any actions sourced from the widgetId we've just swapped to
            foreach (Action.Action action in _actions)
            {
                if (action.IsDrawer && action.Source == "widget" && action.SourceId == widgetId)
                {
                    action.Rect = region.DimensionsForActions;
                }
            }
        }

        /// <summary>
        /// Get the regionId for an active widget
        /// </summary>
        /// <param name="widgetId"></param>
        /// <returns>The regionId or null</returns>
        public string GetRegionIdByActiveWidgetId(string widgetId)
        {
            foreach (Region region in _regions)
            {
                if (region.GetCurrentWidgetId() == widgetId)
                {
                    return region.Id;
                }
            }
            return null;
        }

        /// <summary>
        /// Is the provided widgetId playing
        /// </summary>
        /// <param name="widgetId"></param>
        /// <returns></returns>
        public bool IsWidgetIdPlaying(string widgetId)
        {
            foreach (Region region in _regions)
            {
                if (region.GetCurrentWidgetId() == widgetId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get Current Widget Id for the provided Region
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public List<string> GetCurrentWidgetIdForRegion(Point point)
        {
            List<string> activeWidgets = new List<string>();
            foreach (Region region in _regions)
            {
                if (region.DimensionsForActions.Contains(point))
                {
                    activeWidgets.Add(region.GetCurrentWidgetId());
                }
            }

            return activeWidgets;
        }

        /// <summary>
        /// Execute a Widget
        /// </summary>
        /// <param name="widgetId"></param>
        public void ExecuteWidget(int widgetId)
        {
            // We should check that this widget is a shell command, and if not, back out.
            // if it is a shell command, we should execute it without interrupting what we're doing.
            XmlNode widget = GetWidgetFromDrawer(widgetId);

            // Check the widgets type
            if (widget.Attributes["type"] == null || widget.Attributes["type"].Value != "shellcommand")
            {
                throw new Exception("Widget not a shell command. widgetId: " + widgetId);
            }

            // Create the new node
            Media media = Media.Create(Media.ParseOptions(widget));

            // UI thread
            Dispatcher.Invoke(new System.Action(() => {
                // Execute this media node immediately.
                media.RenderMedia(0);

                // Stop it (no transition)
                media.Stop(false);
                media = null;
            }));
        }

        /// <summary>
        /// Get Region by Id
        /// </summary>
        /// <param name="regionId"></param>
        /// <returns></returns>
        private Region GetRegionById(string regionId)
        {
            foreach (Region region in _regions)
            {
                if (region.Id == regionId)
                {
                    return region;
                }
            }

            throw new Exception("Region not found with Id: " + regionId + " on layoutId: " + _layoutId);
        }

        /// <summary>
        /// Get a Widget from the Drawer
        /// </summary>
        /// <param name="widgetId"></param>
        /// <returns></returns>
        private XmlNode GetWidgetFromDrawer(int widgetId)
        {
            // Get our node from the drawer
            foreach (XmlNode node in _drawer)
            {
                if (node.Attributes["id"] != null && int.Parse(node.Attributes["id"].Value) == widgetId)
                {
                    // Found it, create the media node
                    return node;
                }
            }

            throw new Exception("Drawer does not contain a Widget with widgetId " + widgetId);
        }

        /// <summary>
        /// Does this layout have a widget in its drawer matching the provided ID
        /// </summary>
        /// <param name="widgetId"></param>
        /// <returns></returns>
        public bool HasWidgetIdInDrawer(int widgetId)
        {
            try
            {
                GetWidgetFromDrawer(widgetId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get Actions
        /// </summary>
        /// <returns></returns>
        public List<Action.Action> GetActions()
        {
            return _actions;
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
        }

        /// <summary>
        /// A region has stopped.
        /// </summary>
        private void Region_OnRegionStopped()
        {
            Debug.WriteLine("Region_OnRegionStopped: Region stopped", "Layout");

            foreach (Region temp in _regions)
            {
                if (!temp.IsStopped)
                {
                    return;
                }
            }

            Debug.WriteLine("Region_OnRegionStopped: All regions stopped", "Layout");

            // All regions have stopped.
            // Yield and then call stop.
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                OnLayoutStopped?.Invoke(this);
            }));
        }

        /// <summary>
        /// Someone wants to trigger a web hook.
        /// </summary>
        /// <param name="triggerCode"></param>
        private void Region_TriggerWebhookEvent(string triggerCode, int sourceId)
        {
            Schedule.EmbeddedServerOnTriggerReceived(triggerCode, sourceId);
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
