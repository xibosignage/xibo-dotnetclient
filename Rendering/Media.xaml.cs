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
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml;
using XiboClient.Adspace;
using XiboClient.Logic;

namespace XiboClient.Rendering
{
    /// <summary>
    /// Interaction logic for Media.xaml
    /// </summary>
    public partial class Media : UserControl
    {

        /// <summary>
        /// Event for Duration Elapsed
        /// </summary>
        /// <param name="filesPlayed"></param>
        public delegate void DurationElapsedDelegate(int filesPlayed);
        public event DurationElapsedDelegate DurationElapsedEvent;
        protected int _filesPlayed = 1;

        /// <summary>
        /// Media has stopped
        /// </summary>
        public delegate void MediaStoppedDelegate(Media media);
        public event MediaStoppedDelegate MediaStoppedEvent;

        /// <summary>
        /// Trigger web hook
        /// </summary>
        /// <param name="triggerCode"></param>
        public delegate void TriggerWebhookDelegate(string triggerCode, int sourceId);
        public event TriggerWebhookDelegate TriggerWebhookEvent;

        /// <summary>
        /// Have we stopped?
        /// </summary>
        private bool _stopped = false;

        /// <summary>
        /// The Id of this Widget
        /// NB: this is the widgetId
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The ID of the media file (or widgetId if no file)
        /// </summary>
        public string FileId { get; set; }

        /// <summary>
        /// Gets or Sets the duration of this media. Will be 0 if ""
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Has this media exipred?
        /// </summary>
        public bool Expired { get; set; } = false;

        // Private Timer
        protected DispatcherTimer _timer;
        private bool _timerStarted = false;

        /// <summary>
        /// The Intended Width of this Media
        /// </summary>
        public int WidthIntended { get { return options.width; } }

        /// <summary>
        /// The Intended Height of this Media
        /// </summary>
        public int HeightIntended { get { return options.height; } }

        /// <summary>
        /// The Media Options
        /// </summary>
        private MediaOptions options;

        /// <summary>
        /// The time we started
        /// </summary>
        protected DateTime _startTick;

        /// <summary>
        /// A unique ID for this instance of media
        /// </summary>
        public Guid UniqueId { get; private set; }

        /// <summary>
        /// The ScheduleId
        /// </summary>
        public int ScheduleId { get; private set; }

        /// <summary>
        /// The LayoutId
        /// </summary>
        public int LayoutId { get; private set; }

        /// <summary>
        /// Are stats enabled.
        /// </summary>
        public bool StatsEnabled { get; private set; }

        /// <summary>
        /// Did this media item fail to play?
        /// </summary>
        public bool IsFailedToPlay { get; protected set; }

        /// <summary>
        /// A list of impression Urls to call on stop.
        /// </summary>
        public List<string> AdspaceExchangeImpressionUrls = new List<string>();

        /// <summary>
        /// Ad list of error ulrs to call on stop.
        /// </summary>
        public List<string> AdspaceExchangeErrorUrls = new List<string>();

        /// <summary>
        /// Is this an adspace exchange item?
        /// </summary>
        public bool IsAdspaceExchange = false;

        /// <summary>
        /// Media Object
        /// </summary>
        /// <param name="options"></param>
        public Media(MediaOptions options)
        {
            InitializeComponent();

            UniqueId = Guid.NewGuid();

            // Store the options.
            this.options = options;
            this.Id = options.mediaid;
            this.FileId = options.FileId > 0 ? options.FileId + "" : options.mediaid;
            ScheduleId = options.scheduleId;
            LayoutId = options.layoutId;
            StatsEnabled = options.isStatEnabled;

            // Start us off in a good state
            IsFailedToPlay = false;
        }

        /// <summary>
        /// Media Options for protected access
        /// </summary>
        protected MediaOptions Options { get { return this.options; } }

        /// <summary>
        /// Start the Timer for this Media
        /// </summary>
        protected void StartTimer(double position)
        {
            //start the timer
            if (!_timerStarted && Duration > 0)
            {
                double remainingSeconds = (Duration - position);

                Debug.WriteLine("StartTimer: duration = " + Duration + ", position = " + position + ", Delta = " + remainingSeconds, "Media");

                // a timer must run for some time at least
                // the fact we're here at all means that some other things on this Layout have time to run
                // so expire after the minimum sensible time.
                if (remainingSeconds <= 0)
                {
                    remainingSeconds = 1;
                }

                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(remainingSeconds);
                _timer.Start();

                _timer.Tick += new EventHandler(timer_Tick);

                _timerStarted = true;
            }
        }

        /// <summary>
        /// Set the duration to the new duration provided.
        /// </summary>
        /// <param name="duration"></param>
        public void SetDuration(int duration)
        {
            Duration = (int)(duration - CurrentPlaytime());
            RestartTimer();
        }

        /// <summary>
        /// Extend the duration by the provided amount
        /// </summary>
        /// <param name="duration"></param>
        public void ExtendDuration(int duration)
        {
            SetDuration(Duration + duration);
        }

        /// <summary>
        /// Reset the timer and start again
        /// </summary>
        protected void RestartTimer()
        {
            Debug.WriteLine("Restarting Timer to " + Duration, "Media");
            if (_timerStarted)
            {
                _timer.Stop();
                _timer.Interval = TimeSpan.FromSeconds(Duration);
                _timer.Start();
            }
            else
            {
                StartTimer(0);
            }
        }

        /// <summary>
        /// Render Media call
        /// </summary>
        public virtual void RenderMedia(double position)
        {
            // Record the start time.
            if (position <= 0 || this._startTick == null)
            {
                this._startTick = DateTime.Now;
            }

            // We haven't stopped
            this._stopped = false;

            // Start the timer for this media
            StartTimer(position);

            // Transition In
            TransitionIn();
        }

        /// <summary>
        /// Timer Tick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void timer_Tick(object sender, EventArgs e)
        {
            // Once it has expired we might as well stop the timer?
            _timer.Stop();

            // Signal that this Media Item's duration has elapsed
            SignalElapsedEvent();
        }

        /// <summary>
        /// Signals that an event is elapsed
        /// Will raise a DurationElapsedEvent
        /// </summary>
        public void SignalElapsedEvent()
        {
            Expired = true;

            Trace.WriteLine(new LogMessage("Media - SignalElapsedEvent", "Media Complete"), LogType.Audit.ToString());

            // We're complete
            DurationElapsedEvent?.Invoke(_filesPlayed);
        }

        /// <summary>
        /// Stop this Media
        /// <param name="isShouldTransition"/>
        /// </summary>
        public void Stop(bool isShouldTransition)
        {
            if (!isShouldTransition)
            {
                this._stopped = true;
                this.MediaStoppedEvent?.Invoke(this);
            }
            else
            {
                TransitionOut();
            }

            // Initiate any tidy up that is needed in here.
            // Dispose of the Timer
            if (_timer != null)
            {
                if (_timer.IsEnabled)
                {
                    _timer.Stop();
                }
                _timer = null;
            }
        }

        /// <summary>
        /// Final clean up
        /// </summary>
        public virtual void Stopped()
        {

        }

        /// <summary>
        /// Get the Current Tick
        /// </summary>
        /// <returns></returns>
        public double CurrentPlaytime()
        {
            return (DateTime.Now - this._startTick).TotalSeconds;
        }

        /// <summary>
        /// Is a region size change required
        /// </summary>
        /// <returns></returns>
        public virtual bool RegionSizeChangeRequired()
        {
            return false;
        }

        /// <summary>
        /// TransitionIn if necessary
        /// </summary>
        public void TransitionIn()
        {
            // Does this Media item have an inbound transition?
            string transIn = options.Dictionary.Get("transIn");
            if (!string.IsNullOrEmpty(transIn))
            {
                // Yes we do have one.
                int duration = options.Dictionary.Get("transInDuration", 1000);

                switch (transIn)
                {
                    case "fly":
                        FlyAnimation(options.Dictionary.Get("transInDirection", "W"), duration, true);
                        break;
                    case "fadeIn":
                        DoubleAnimation animation = new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(duration)
                        };
                        BeginAnimation(OpacityProperty, animation);
                        break;
                }
            }
        }

        /// <summary>
        /// Transition Out
        /// </summary>
        public void TransitionOut()
        {
            // Does this Media item have an outbound transition?
            string transOut = options.Dictionary.Get("transOut");
            if (!string.IsNullOrEmpty(transOut))
            {
                // Yes we do have one.
                int duration = options.Dictionary.Get("transOutDuration", 1000);

                switch (transOut)
                {
                    case "fly":
                        FlyAnimation(options.Dictionary.Get("transOutDirection", "E"), duration, false);
                        break;
                    case "fadeOut":
                        DoubleAnimation animation = new DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = TimeSpan.FromMilliseconds(duration)
                        };
                        animation.Completed += Stop_Animation_Completed;
                        BeginAnimation(OpacityProperty, animation);
                        break;
                }
            }
            else if (!this._stopped)
            {
                this._stopped = true;
                this.MediaStoppedEvent?.Invoke(this);
            }
        }

        /// <summary>
        /// Override the out transition with a new one
        /// </summary>
        /// <param name="type"></param>
        /// <param name="duration"></param>
        /// <param name="direction"></param>
        public void OverrideTransitionOut(string type, int duration, string direction)
        {
            this.options.Dictionary.Replace("transOut", type);
            this.options.Dictionary.Replace("transOutDuration", "" + duration);
            this.options.Dictionary.Replace("transOutDirection", direction);
        }

        /// <summary>
        /// Animation completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Start_Animation_Completed(object sender, EventArgs e)
        {
            // Do we need to do anything in here?
            Debug.WriteLine("In", "Start_Animation_Completed");
        }

        /// <summary>
        /// Animation completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Stop_Animation_Completed(object sender, EventArgs e)
        {
            Debug.WriteLine("In", "Stop_Animation_Completed");

            // Indicate we have stopped (only once)
            if (!this._stopped)
            {
                this._stopped = true;
                this.MediaStoppedEvent?.Invoke(this);
            }
        }

        /// <summary>
        /// Fly Animation
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="duration"></param>
        /// <param name="isInbound"></param>
        private void FlyAnimation(string direction, double duration, bool isInbound)
        {
            // We might not need both of these, but we add them just in case we have a mid-way compass point
            var trans = new TranslateTransform();

            DoubleAnimation doubleAnimationX = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(duration)
            };
            DoubleAnimation doubleAnimationY = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(duration)
            };

            if (isInbound)
            {
                doubleAnimationX.Completed += Start_Animation_Completed;
                doubleAnimationY.Completed += Start_Animation_Completed;
            }
            else
            {
                doubleAnimationX.Completed += Stop_Animation_Completed;
                doubleAnimationY.Completed += Stop_Animation_Completed;
            }

            // Get the viewable window width and height
            int screenWidth = options.PlayerWidth;
            int screenHeight = options.PlayerHeight;

            int top = options.top;
            int left = options.left;

            // Where should we end up once we are done?
            if (isInbound)
            {
                // End up at the top/left
                doubleAnimationX.To = left;
                doubleAnimationY.To = top;
            }
            else
            {
                // End up off the screen
                doubleAnimationX.To = screenWidth;
                doubleAnimationY.To = screenHeight;
            }

            // Compass points
            switch (direction)
            {
                case "N":
                    if (isInbound)
                    {
                        // We come in from the bottom of the screen
                        doubleAnimationY.From = (screenHeight - top);
                    }
                    else
                    {
                        // We go out across the top
                        doubleAnimationY.From = top;
                    }

                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    break;

                case "NE":
                    if (isInbound)
                    {
                        doubleAnimationX.From = (screenWidth - left);
                        doubleAnimationY.From = (screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = top;
                    }


                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    break;

                case "E":
                    if (isInbound)
                    {
                        doubleAnimationX.From = -(screenWidth - left);
                    }
                    else
                    {
                        if (left == 0)
                        {
                            doubleAnimationX.From = -left;
                        }
                        else
                        {
                            doubleAnimationX.From = -(screenWidth - left);
                        }

                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    break;

                case "SE":
                    if (isInbound)
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = -(screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = (screenWidth - left);
                        doubleAnimationY.From = -(screenHeight - top);
                    }

                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    break;

                case "S":
                    if (isInbound)
                    {
                        doubleAnimationX.From = -(screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = -top;
                    }

                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationX);
                    break;

                case "SW":
                    if (isInbound)
                    {
                        doubleAnimationX.From = (screenWidth - left);
                        doubleAnimationY.From = -top;
                    }
                    else
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = -(screenHeight - left);
                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    break;

                case "W":
                    if (isInbound)
                    {
                        doubleAnimationX.From = (screenWidth - left);
                    }
                    else
                    {
                        doubleAnimationX.From = -left;
                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    break;

                case "NW":
                    if (isInbound)
                    {
                        doubleAnimationX.From = (screenWidth - left);
                        doubleAnimationY.From = (screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = top;
                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    break;
            }

            // Set this Media's render transform
            RenderTransform = trans;
        }

        /// <summary>
        /// Create a new media node
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Media Create(MediaOptions options)
        {
            Media media;
            switch (options.type)
            {
                case "image":
                    options.uri = ApplicationSettings.Default.LibraryPath + @"\" + options.uri;
                    media = new Image(options);
                    break;

                case "powerpoint":
                    options.uri = ApplicationSettings.Default.LibraryPath + @"\" + options.uri;
                    media = new PowerPoint(options);
                    break;

                case "video":
                    options.uri = ApplicationSettings.Default.LibraryPath + @"\" + options.uri;
                    media = new Video(options);
                    break;

                case "localvideo":
                    // Local video does not update the URI with the library path, it just takes what has been provided in the Widget.
                    media = new Video(options);
                    break;

                case "audio":
                    options.uri = ApplicationSettings.Default.LibraryPath + @"\" + options.uri;
                    media = new Audio(options);
                    break;

                case "embedded":
                    options.IsPinchToZoomEnabled = true;
                    media = WebMedia.GetConfiguredWebMedia(options, WebMedia.ReadBrowserType(options.text));
                    break;

                case "datasetview":
                case "ticker":
                case "text":
                    media = WebMedia.GetConfiguredWebMedia(options, true);
                    break;

                case "webpage":
                    options.IsPinchToZoomEnabled = true;
                    media = WebMedia.GetConfiguredWebMedia(options, false);
                    break;

                case "flash":
                    options.uri = ApplicationSettings.Default.LibraryPath + @"\" + options.uri;
                    media = new Flash(options);
                    break;

                case "shellcommand":
                    media = new ShellCommand(options);
                    break;

                case "htmlpackage":
                    options.IsPinchToZoomEnabled = true;
                    media = WebMedia.GetConfiguredWebMedia(options, false);
                    ((WebMedia)media).ConfigureForHtmlPackage();
                    break;

                case "spacer":
                    media = new Spacer(options);
                    break;

                case "hls":
                    media = new WebEdge(options);
                    break;

                default:
                    if (options.render == "html")
                    {
                        media = WebMedia.GetConfiguredWebMedia(options, true);
                    }
                    else
                    {
                        throw new InvalidOperationException("Not a valid media node type: " + options.type);
                    }
                    break;
            }

            // If this came from an ad, then set the impression/error URLs.
            if (options.GetAd() != null)
            {
                media.SetAdspaceExchangeImpressionUrls(options.GetAd().ImpressionUrls);
                media.SetAdspaceExchangeErrorUrls(options.GetAd().ErrorUrls);
            }

            return media;
        }

        /// <summary>
        /// Parse and return options
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static MediaOptions ParseOptions(XmlNode node, Schedule schedule, double width, double height)
        {
            // A brand new media options
            MediaOptions options = new MediaOptions
            {
                Dictionary = new MediaDictionary()
            };

            // Parse node attributes
            XmlAttributeCollection nodeAttributes = node.Attributes;

            // Start with core properties
            options.mediaid = nodeAttributes["id"].Value ?? Guid.NewGuid().ToString();
            options.type = nodeAttributes["type"].Value;

            bool isUriProvided = false;
            if (options.type == "ssp")
            {
                // Get the partner (if configured)
                string partner = null;
                foreach (XmlNode option in node.SelectSingleNode("options").ChildNodes)
                {
                    if (option.Name == "partner")
                    {
                        partner = option.InnerText;
                        break;
                    }
                }

                // Handle making an ad request and transform this into a new type if necessary.
                Ad ad = schedule.GetAd(width, height, true, partner);                
                options.type = ad.XiboType;
                options.duration = ad.GetDuration();

                // URI
                isUriProvided = true;
                options.uri = ad.GetFileName();

                // Impressions/Errors (these will be injected into Media later)
                options.SetAd(ad);
            }
            else
            {
                if (nodeAttributes["duration"].Value != "")
                {
                    options.duration = int.Parse(nodeAttributes["duration"].Value);
                }
                else
                {
                    options.duration = 60;
                    Debug.WriteLine("Duration is Empty, using a default of 60.", "Region - SetNextMediaNode");
                }
            }

            // Set the file id
            if (nodeAttributes["fileId"] != null)
            {
                options.FileId = int.Parse(nodeAttributes["fileId"].Value);

                if (CacheManager.Instance.IsUnsafeMedia(options.FileId + ""))
                {
                    Trace.WriteLine(new LogMessage("Media", string.Format("ParseOptions: MediaID [{0}] has been blacklisted.", options.mediaid)), LogType.Info.ToString());
                    throw new Exception("Unsafe Media");
                }
            }
            else
            {
                // No fileId, this could be an old XLF so we ought to check the fallback mediaId.
                if (CacheManager.Instance.IsUnsafeMedia(options.mediaid))
                {
                    Trace.WriteLine(new LogMessage("Media", string.Format("ParseOptions: MediaID [{0}] has been blacklisted.", options.mediaid)), LogType.Info.ToString());
                    throw new Exception("Unsafe Media");
                }
            }

            // mediaId on options is actually the widgetId
            if (CacheManager.Instance.IsUnsafeWidget(options.mediaid))
            {
                Trace.WriteLine(new LogMessage("Media", string.Format("ParseOptions: widgetId [{0}] has been blacklisted.", options.mediaid)), LogType.Info.ToString());
                throw new Exception("Unsafe Widget");
            }

            // Stats enabled?
            options.isStatEnabled = (nodeAttributes["enableStat"] == null) ? true : (int.Parse(nodeAttributes["enableStat"].Value) == 1);

            // Pinch to Zoom enabled?
            options.IsPinchToZoomEnabled = false;

            // Render as
            if (nodeAttributes["render"] != null)
                options.render = nodeAttributes["render"].Value;

            // Widget From/To dates (v2 onward)
            try
            {
                if (nodeAttributes["fromDt"] != null)
                {
                    options.FromDt = DateTime.Parse(nodeAttributes["fromDt"].Value, CultureInfo.InvariantCulture);
                }
                else
                {
                    options.FromDt = DateTime.MinValue;
                }

                if (nodeAttributes["toDt"] != null)
                {
                    options.ToDt = DateTime.Parse(nodeAttributes["toDt"].Value, CultureInfo.InvariantCulture);
                }
                else
                {
                    options.ToDt = DateTime.MaxValue;
                }
            }
            catch
            {
                Trace.WriteLine(new LogMessage("Region", "ParseOptionsForMediaNode: Unable to parse widget from/to dates."), LogType.Error.ToString());
            }

            // We cannot have a 0 duration here... not sure why we would... but
            if (options.duration == 0 && options.type != "video" && options.type != "localvideo" && options.type != "audio")
            {
                int emptyLayoutDuration = int.Parse(ApplicationSettings.Default.EmptyLayoutDuration.ToString());
                options.duration = (emptyLayoutDuration == 0) ? 10 : emptyLayoutDuration;
            }

            // There will be some stuff on option nodes
            XmlNode optionNode = node.SelectSingleNode("options");

            // Track if an update interval has been provided in the XLF
            bool updateIntervalProvided = false;

            // Loop through each option node
            foreach (XmlNode option in optionNode.ChildNodes)
            {
                if (option.Name == "direction")
                {
                    options.direction = option.InnerText;
                }
                else if (option.Name == "uri" && !isUriProvided)
                {
                    options.uri = option.InnerText;
                }
                else if (option.Name == "copyright")
                {
                    options.copyrightNotice = option.InnerText;
                }
                else if (option.Name == "scrollSpeed")
                {
                    try
                    {
                        options.scrollSpeed = int.Parse(option.InnerText);
                    }
                    catch
                    {
                        Debug.WriteLine("Non integer scrollSpeed in XLF", "Region - SetNextMediaNode");
                    }
                }
                else if (option.Name == "updateInterval")
                {
                    updateIntervalProvided = true;

                    try
                    {
                        options.updateInterval = int.Parse(option.InnerText);
                    }
                    catch
                    {
                        // Update interval not defined, so assume a high value
                        options.updateInterval = 3600;

                        Debug.WriteLine("Non integer updateInterval in XLF", "Region - SetNextMediaNode");
                    }
                }

                // Add this to the options object
                options.Dictionary.Add(option.Name, option.InnerText);
            }

            // And some stuff on Raw nodes
            XmlNode rawNode = node.SelectSingleNode("raw");

            if (rawNode != null)
            {
                foreach (XmlNode raw in rawNode.ChildNodes)
                {
                    if (raw.Name == "text")
                    {
                        options.text = raw.InnerText;
                    }
                    else if (raw.Name == "template")
                    {
                        options.documentTemplate = raw.InnerText;
                    }
                    else if (raw.Name == "embedHtml")
                    {
                        options.text = raw.InnerText;
                    }
                    else if (raw.Name == "embedScript")
                    {
                        options.javaScript = raw.InnerText;
                    }
                }
            }

            // Is this a file based media node?
            if (options.type == "video"
                || options.type == "flash"
                || options.type == "image"
                || options.type == "powerpoint"
                || options.type == "audio"
                || options.type == "htmlpackage")
            {
                // Use the cache manager to determine if the file is valid
                if (!CacheManager.Instance.IsValidPath(options.uri) && !CacheManager.Instance.IsUnsafeMedia(options.uri))
                {
                    throw new Exception("Invalid Media");
                }
            }

            // Audio Nodes?
            XmlNode audio = node.SelectSingleNode("audio");

            if (audio != null)
            {
                foreach (XmlNode audioNode in audio.ChildNodes)
                {
                    MediaOptions audioOptions = new MediaOptions
                    {
                        Dictionary = new MediaDictionary(),
                        duration = 0,
                        uri = ApplicationSettings.Default.LibraryPath + @"\" + audioNode.InnerText
                    };

                    if (audioNode.Attributes["loop"] != null)
                    {
                        audioOptions.Dictionary.Add("loop", audioNode.Attributes["loop"].Value);

                        if (audioOptions.Dictionary.Get("loop", 0) == 1)
                        {
                            // Set the media duration to be equal to the duration of the parent media
                            audioOptions.duration = (options.duration == 0) ? int.MaxValue : options.duration;
                        }
                    }

                    if (audioNode.Attributes["volume"] != null)
                    {
                        options.Dictionary.Add("volume", audioNode.Attributes["volume"].Value);
                    }

                    options.Audio.Add(new Audio(audioOptions));
                }
            }

            // Media Types without an update interval should have a sensible default (xibosignage/xibo#404)
            // This means that items which do not provide an update interval will still refresh.
            if (!updateIntervalProvided)
            {
                // Special handling for text/webpages because we know they should never have a default update interval applied
                if (options.type == "webpage" || options.type == "text")
                {
                    // Very high (will expire eventually, but shouldn't cause a routine request for a new resource
                    options.updateInterval = int.MaxValue;
                }
                else
                {
                    // Default to 5 minutes for those items that do not provide an update interval
                    options.updateInterval = 5;
                }
            }

            return options;
        }

        /// <summary>
        /// Trigger a web hook
        /// </summary>
        /// <param name="triggerCode"></param>
        /// <param name="sourceId"></param>
        protected void TriggerWebhook(string triggerCode)
        {
            int id;
            try
            {
                id = int.Parse(Id);
            }
            catch
            {
                id = 0;
            }

            TriggerWebhookEvent?.Invoke(triggerCode, id);
        }

        /// <summary>
        /// Set any adspace exchange impression urls.
        /// </summary>
        /// <param name="urls"></param>
        public void SetAdspaceExchangeImpressionUrls(List<string> urls)
        {
            IsAdspaceExchange = true;
            AdspaceExchangeImpressionUrls = urls;
        }

        /// <summary>
        /// Set any adspace exchange error urls.
        /// </summary>
        /// <param name="urls"></param>
        public void SetAdspaceExchangeErrorUrls(List<string> urls)
        {
            AdspaceExchangeErrorUrls = urls;
        }
    }
}
