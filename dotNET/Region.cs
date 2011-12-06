/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006,2007,2008,2009,2010,2011 Daniel Garner and James Packer
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
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;
using XiboClient.Properties;

namespace XiboClient
{
    //<summary>
    //A screen region control
    //</summary>
    class Region : Panel
    {
        private BlackList blackList;
        public delegate void DurationElapsedDelegate();
        public event DurationElapsedDelegate DurationElapsedEvent;

        private Media media;
        private RegionOptions options;
        public bool hasExpired = false;
        public bool layoutExpired = false;
        private int currentSequence = -1;

        // Stat objects
        private StatLog _statLog;
        private Stat _stat;

        // Cache Manager
        private CacheManager _cacheManager;

        public Region(ref StatLog statLog, ref CacheManager cacheManager)
        {
            // Store the statLog
            _statLog = statLog;

            // Store the cache manager
            // TODO: What happens if the cachemanger changes during the lifecycle of this region?
            _cacheManager = cacheManager;

            //default options
            options.width = 1024;
            options.height = 768;
            options.left = 0;
            options.top = 0;
            options.uri = null;

            this.Location = new System.Drawing.Point(options.left, options.top);
            this.Size = new System.Drawing.Size(options.width, options.height);
            this.BackColor = System.Drawing.Color.Transparent;

            if (Settings.Default.DoubleBuffering)
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
                SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            }

            // Create a new BlackList for us to use
            blackList = new BlackList();
        }

        public RegionOptions regionOptions
        {
            get 
            { 
                return this.options; 
            }
            set 
            { 
                this.options = value;

                EvalOptions();
            }
        }

        ///<summary>
        /// Evaulates the change in options
        ///</summary>
        private void EvalOptions() 
        {
            if (currentSequence == -1)
            {
                //evaluate the width, etc
                this.Location = new System.Drawing.Point(options.left, options.top);
                this.Size = new System.Drawing.Size(options.width, options.height);
            }

            int temp = currentSequence;
            
            //set the next media node for this panel
            if (!SetNextMediaNode())
            {
                // For some reason we cannot set a media node... so we need this region to become invalid
                hasExpired = true;
                DurationElapsedEvent();
                return;
            }

            // If the sequence hasnt been changed, OR the layout has been expired
            if (currentSequence == temp || layoutExpired)
            {
                //there has been no change to the sequence, therefore the media we have already created is still valid
                //or this media has actually been destroyed and we are working out way out the call stack
                return;
            }

            System.Diagnostics.Debug.WriteLine(String.Format("Creating new media: {0}, {1}", options.type, options.mediaid), "Region - EvalOptions");

            try
            {
                switch (options.type)
                {
                    case "image":
                        options.uri = Properties.Settings.Default.LibraryPath + @"\" + options.uri;
                        media = new ImagePosition(options);
                        break;

                    case "text":
                        media = new Text(options);
                        break;

                    case "powerpoint":
                        options.uri = Properties.Settings.Default.LibraryPath + @"\" + options.uri;
                        media = new WebContent(options);
                        break;

                    case "video":
                        options.uri = Properties.Settings.Default.LibraryPath + @"\" + options.uri;
                        media = new Video(options);
                        break;

                    case "webpage":
                        media = new WebContent(options);
                        break;

                    case "flash":
                        options.uri = Properties.Settings.Default.LibraryPath + @"\" + options.uri;
                        media = new Flash(options);
                        break;

                    case "ticker":
                        media = new Rss(options);
                        break;

                    case "embedded":
                        media = new Text(options);
                        break;

                    case "datasetview":
                        media = new DataSetView(options);
                        break;

                    default:
                        //do nothing
                        SetNextMediaNode();
                        return;
                }

                //sets up the timer for this media
                media.Duration = options.duration;

                //add event handler
                media.DurationElapsedEvent += new Media.DurationElapsedDelegate(media_DurationElapsedEvent);

                //any additional media specific render options (and starts the timer)
                media.RenderMedia();

                Controls.Add(media);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("EvalOptions", "Unable to start media. " + ex.Message), LogType.Error.ToString());
                SetNextMediaNode();
            }

            // This media has started and is being replaced
            _stat = new Stat();
            _stat.type = StatType.Media;
            _stat.fromDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _stat.scheduleID = options.scheduleId;
            _stat.layoutID = options.layoutId;
            _stat.mediaID = options.mediaid;

            System.Diagnostics.Debug.WriteLine("Showing new media", "Region - Eval Options");
        }

        /// <summary>
        /// Sets the next media node. Should be used either from a mediaComplete event, or an options reset from 
        /// the parent.
        /// </summary>
        private bool SetNextMediaNode()
        {
            int playingSequence = currentSequence;

            // What if there are no media nodes?
            if (options.mediaNodes.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No media nodes to display", "Region - SetNextMediaNode");
                hasExpired = true;
                return false;
            }
            
            if (options.mediaNodes.Count == 1 && currentSequence != -1)
            {
                //dont bother discarding this media, keep all the same details, but still trigger an expired event
                System.Diagnostics.Debug.WriteLine("Media Expired:" + options.ToString() + " . Nothing else to show", "Region - SetNextMediaNode");
                hasExpired = true;

                DurationElapsedEvent();
                return true;
            }

            // Move the sequence on
            currentSequence++;

            if (currentSequence >= options.mediaNodes.Count)
            {
                currentSequence = 0; //zero it
                
                hasExpired = true; //we have expired (want to raise an expired event to the parent)

                System.Diagnostics.Debug.WriteLine("Media Expired:" + options.ToString() + " . Reached the end of the sequence. Starting from the beginning.", "Region - SetNextMediaNode");

                DurationElapsedEvent();
              
                // We want to continue on to show the next media (unless the duration elapsed event triggers a region change)
                if (layoutExpired) return true;
            }

            //Zero out the options that are persisted
            options.text = "";
            options.documentTemplate = "";
            options.copyrightNotice = "";
            options.scrollSpeed = 30;
            options.updateInterval = 6;
            options.uri = "";
            options.direction = "none";
            options.javaScript = "";
            options.Dictionary = new MediaDictionary();

            // Get a media node
            bool validNode = false;
            int numAttempts = 0;
            
            while (!validNode)
            {
                numAttempts++;

                // Get the media node for this sequence
                XmlNode mediaNode = options.mediaNodes[currentSequence];

                XmlAttributeCollection nodeAttributes = mediaNode.Attributes;

                if (nodeAttributes["id"].Value != null) options.mediaid = nodeAttributes["id"].Value;

                // Check isnt blacklisted
                if (blackList.BlackListed(options.mediaid))
                {
                    System.Diagnostics.Debug.WriteLine(String.Format("The File [{0}] has been blacklisted", options.mediaid), "Region - SetNextMediaNode");

                    // Increment and Loop
                    currentSequence++;

                    if (currentSequence >= options.mediaNodes.Count)
                    {
                        currentSequence = 0; //zero it

                        hasExpired = true; //we have expired (want to raise an expired event to the parent)

                        System.Diagnostics.Debug.WriteLine("Media Expired:" + options.ToString() + " . Reached the end of the sequence. Starting from the beginning.", "Region - SetNextMediaNode");

                        DurationElapsedEvent();

                        // We want to continue on to show the next media (unless the duration elapsed event triggers a region change)
                        if (layoutExpired) return true;
                    }
                }
                else
                {
                    validNode = true;

                    // New version has a different schema - the right way to do it would be to pass the <options> and <raw> nodes to 
                    // the relevant media class - however I dont feel like engineering such a change so the alternative is to
                    // parse all the possible media type nodes here.

                    // Type and Duration will always be on the media node
                    options.type        = nodeAttributes["type"].Value;

                    //TODO: Check the type of node we have, and make sure it is supported.
                    
                    if (nodeAttributes["duration"].Value != "")
                    {
                        options.duration = int.Parse(nodeAttributes["duration"].Value);
                    }
                    else
                    {
                        options.duration = 60;
                        System.Diagnostics.Trace.WriteLine("Duration is Empty, using a default of 60.", "Region - SetNextMediaNode");
                    }

                    // We cannot have a 0 duration here... not sure why we would... but
                    if (options.duration == 0 && options.type != "video")
                    {
                        int emptyLayoutDuration = int.Parse(Properties.Settings.Default.emptyLayoutDuration.ToString());
                        options.duration = (emptyLayoutDuration == 0) ? 10 : emptyLayoutDuration;
                    }

                    // There will be some stuff on option nodes
                    XmlNode optionNode = mediaNode.FirstChild;                    

                    
                    foreach (XmlNode option in optionNode.ChildNodes)
                    {
                        if (option.Name == "direction")
                        {
                            options.direction = option.InnerText;
                        }
                        else if (option.Name == "uri")
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
                                System.Diagnostics.Trace.WriteLine("Non integer scrollSpeed in XLF", "Region - SetNextMediaNode");
                            }
                        }
                        else if (option.Name == "updateInterval")
                        {
                            try
                            {
                                options.updateInterval = int.Parse(option.InnerText);
                            }
                            catch
                            {
                                System.Diagnostics.Trace.WriteLine("Non integer updateInterval in XLF", "Region - SetNextMediaNode");
                            }
                        }

                        // Add this to the options object
                        options.Dictionary.Add(option.Name, option.InnerText);
                    }

                    // And some stuff on Raw nodes
                    XmlNode rawNode = mediaNode.LastChild;

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

                    // Is this a file based media node?
                    if (options.type == "video" || options.type == "flash" || options.type == "image" || options.type == "powerpoint")
                    {
                        // Use the cache manager to determine if the file is valid
                        validNode = _cacheManager.IsValidPath(options.uri);
                    }
                }

                if (numAttempts > options.mediaNodes.Count)
                {
                    // There are no valid nodes in this region, so just signify that the region is ending, and show nothing.
                    System.Diagnostics.Trace.WriteLine("No Valid media nodes to display", "Region - SetNextMediaNode");
                    
                    hasExpired = true;
                    return false;
                }
            }

            System.Diagnostics.Debug.WriteLine("New media detected " + options.type, "Region - SetNextMediaNode");

            // Remove the old one if we have found a valid node - otherwise keep it
            if ((validNode && playingSequence != -1) && playingSequence != currentSequence)
            {
                System.Diagnostics.Debug.WriteLine("Trying to dispose of the current media", "Region - SetNextMediaNode");
                // Dispose of the current media
                try
                {
                    media.Hide();
                    Application.DoEvents();

                    // Remove the controls
                    Controls.Remove(media);
                    
                    media.Dispose();
                    media = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("No media to remove");
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }

                try
                {
                    // Here we say that this media is expired
                    _stat.toDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    // Record this stat event in the statLog object
                    _statLog.RecordStat(_stat);
                }
                catch
                {
                    System.Diagnostics.Trace.WriteLine("No Stat record when one was expected", LogType.Error.ToString());
                }
            }

            return true;
        }

        /// <summary>
        /// The media has elapsed
        /// </summary>
        void media_DurationElapsedEvent()
        {
            System.Diagnostics.Debug.WriteLine(String.Format("Media Elapsed: {0}", options.uri), "Region - DurationElapsedEvent");

            // make some decisions about what to do next
            EvalOptions();
        }

        /// <summary>
        /// Clears the Region of anything that it shouldnt still have... 
        /// </summary>
        public void Clear()
        {
            try
            {
                // What happens if we are disposing this region but we have not yet completed the stat event?
                if (String.IsNullOrEmpty(_stat.toDate))
                {
                    // Say that this media has ended
                    _stat.toDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    // Record this stat event in the statLog object
                    _statLog.RecordStat(_stat);
                }
            }
            catch
            {
                System.Diagnostics.Trace.WriteLine(new LogMessage("Region - Clear", "Error closing off stat record"), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// Performs the disposal.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    media.Dispose();
                    media = null;

                    System.Diagnostics.Debug.WriteLine("Media Disposed by Region", "Region - Dispose");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine("There was no media to dispose", "Region - Dispose");
                }
                finally
                {
                    if (media != null) media = null;
                }
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// The options specific to a region
    /// </summary>
    struct RegionOptions
    {
        public double scaleFactor;
        public int width;
        public int height;
        public int top;
        public int left;

        public int backgroundLeft;
        public int backgroundTop;

        public string type;
        public string uri;
        public int duration;

        //xml
        public XmlNodeList mediaNodes;

        //rss options
        public string direction;
        public string text;
        public string documentTemplate;
        public string copyrightNotice;
        public string javaScript;
        public int updateInterval;
        public int scrollSpeed;
        
        //The identification for this region
        public string mediaid;
        public int layoutId;
        public string regionId;
        public int scheduleId;
       
        //general options
        public string backgroundImage;
        public string backgroundColor;

        public MediaDictionary Dictionary;

        public override string ToString()
        {
            return String.Format("({0},{1},{2},{3},{4},{5})", width, height, top, left, type, uri);
        }
    }
}
