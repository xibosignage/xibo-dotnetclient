/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006,2007,2008 Daniel Garner and James Packer
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

namespace XiboClient
{
    //<summary>
    //A screen region control
    //</summary>
    class Region : Panel
    {
        private BlackList blackList;

        public Region()
        {
            //default options
            options.width = 1024;
            options.height = 768;
            options.left = 0;
            options.top = 0;
            options.uri = null;

            this.Location = new System.Drawing.Point(options.left, options.top);
            this.Size = new System.Drawing.Size(options.width, options.height);
            this.BackColor = System.Drawing.Color.Transparent;

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
            SetNextMediaNode();

            // If the sequence hasnt been changed, OR the layout has been expired
            if (currentSequence == temp || layoutExpired)
            {
                //there has been no change to the sequence, therefore the media we have already created is still valid
                //or this media has actually been destroyed and we are working out way out the call stack
                return;
            }

            System.Diagnostics.Debug.WriteLine(String.Format("Creating new media: {0}, {1}", options.type, options.mediaid), "Region - EvalOptions");
            
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

            // This media has started and is being replaced
            XmlLog.AppendStat("Media Start", Catagory.Stat, StatType.MediaStart, options.scheduleId, options.layoutId, options.mediaid);

            //media.Opacity = 0F; // Completely Opaque

            this.Controls.Add(media);

            System.Diagnostics.Debug.WriteLine("Showing new media", "Region - Eval Options");
            
        }

        /// <summary>
        /// Sets the next media node. Should be used either from a mediaComplete event, or an options reset from 
        /// the parent.
        /// </summary>
        void SetNextMediaNode()
        {
            int playingSequence = currentSequence;

            // What if there are no media nodes?
            if (options.mediaNodes.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No media nodes to display", "Region - SetNextMediaNode");
                hasExpired = true;
                return;
            }
            
            if (options.mediaNodes.Count == 1 && currentSequence != -1)
            {
                //dont bother discarding this media, keep all the same details, but still trigger an expired event
                System.Diagnostics.Debug.WriteLine("Media Expired:" + options.ToString() + " . Nothing else to show", "Region - SetNextMediaNode");
                hasExpired = true;

                DurationElapsedEvent();
                return;
            }


            currentSequence++;

            if (currentSequence >= options.mediaNodes.Count)
            {
                currentSequence = 0; //zero it
                
                hasExpired = true; //we have expired (want to raise an expired event to the parent)

                System.Diagnostics.Debug.WriteLine("Media Expired:" + options.ToString() + " . Reached the end of the sequence. Starting from the beginning.", "Region - SetNextMediaNode");

                DurationElapsedEvent();
              
                // We want to continue on to show the next media (unless the duration elapsed event triggers a region change)
                if (layoutExpired) return;
            }

            //Zero out the options that are persisted
            options.text = "";
            options.documentTemplate = "";
            options.copyrightNotice = "";
            options.uri = "";

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
                    
                    if (nodeAttributes["duration"].Value != "")
                    {
                        options.duration = int.Parse(nodeAttributes["duration"].Value);
                    }
                    else
                    {
                        options.duration = 60;
                        XmlLog.Append("Duration is Empty, using a default of 60.", Catagory.Error);
                        System.Diagnostics.Debug.WriteLine("Duration is Empty, using a default of 60.", "Region - SetNextMediaNode");
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
                    }

                    // That should cover all the new options
                }

                if (numAttempts > options.mediaNodes.Count)
                {
                    // There are no valid nodes in this region, so just signify that the region is ending, and show nothing.
                    System.Diagnostics.Debug.WriteLine("No Valid media nodes to display", "Region - SetNextMediaNode");

                    XmlLog.Append("No valid media nodes to display - they are all Blacklisted", Catagory.Error);
                    
                    hasExpired = true;
                    return;
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
                    // Remove the controls
                    this.Controls.Remove(media);
                    media.Dispose();

                    media = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("No media to remove");
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }

                // This media has expired and is being replaced
                XmlLog.AppendStat("Media Expired", Catagory.Stat, StatType.MediaEnd, options.scheduleId, options.layoutId, options.mediaid);
            }
        }

        /// <summary>
        /// The media has elapsed
        /// </summary>
        void media_DurationElapsedEvent()
        {
            System.Diagnostics.Debug.WriteLine(String.Format("Media Elapsed: {0}", options.uri), "Region - DurationElapsedEvent");

            //make some decisions about what to do next
            EvalOptions();
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

        public delegate void DurationElapsedDelegate();
        public event DurationElapsedDelegate DurationElapsedEvent;

        private Media media;
        private RegionOptions options;
        public bool hasExpired = false;
        public bool layoutExpired = false;
        private int currentSequence = -1;

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
        
        //The identification for this region
        public string mediaid;
        public int layoutId;
        public int scheduleId;
       
        //general options
        public string backgroundImage;
        public string backgroundColor;

        public override string ToString()
        {
            return String.Format("({0},{1},{2},{3},{4},{5})", width, height, top, left, type, uri);
        }
    }
}
