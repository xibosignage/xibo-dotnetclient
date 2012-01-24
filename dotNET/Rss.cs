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
using System.Collections.ObjectModel;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Net;
using System.Diagnostics;
using System.ServiceModel.Syndication;
using System.Globalization;
using System.Linq;

namespace XiboClient
{
    class Rss 
        : Media
    {
        private int _scheduleId;
        private int _layoutId;
        private string _filePath;
        private string _direction;
        private string _backgroundImage;
        private string _backgroundColor;
        private WebBrowser _webBrowser;
        private string _copyrightNotice;
        private string _mediaid;
        private int _updateInterval;
        private int _scrollSpeed;
        private double _scaleFactor;
        private int _duration;

        private int _numItems;
        private string _takeItemsFrom;
        private int _durationIsPerItem;

        private string _rssFilePath;

        // Build up the RSS feed
        private string _documentText;
        private string _documentTemplate;

        private string _backgroundTop;
        private string _backgroundLeft;
        private WebClient _wc;

        private bool _rssReady;

        private String _headText;
        private String _bodyText;
        private TemporaryHtml _tempHtml;

        /// <summary>
        /// Creates an RSS position with the RegionOptions parameter
        /// </summary>
        /// <param name="options"></param>
        public Rss(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            if (String.IsNullOrEmpty(options.uri))
            {
                throw new ArgumentNullException("Uri", "The Uri for the RSS feed can not be empty");
            }

            // Try to make a URI out of the file path
            try
            {
                _filePath = Uri.UnescapeDataString(options.uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "Rss");

                throw new ArgumentNullException("Uri", "The URI is invalid.");
            }

            Debug.WriteLine("Ticker URL: " + _filePath + ". Options count: " + options.Dictionary.Count.ToString());

            // Set the parameters based on the RegionOptions
            _direction = options.direction;
            _backgroundImage = options.backgroundImage;
            _backgroundColor = options.backgroundColor;
            _copyrightNotice = options.copyrightNotice;
            _mediaid = options.mediaid;
            _scheduleId = options.scheduleId;
            _layoutId = options.layoutId;
            _scaleFactor = options.scaleFactor;
            _duration = options.duration;

            // Update interval and scrolling speed
            _updateInterval = options.updateInterval;
            _scrollSpeed = options.scrollSpeed;

            Debug.WriteLine(String.Format("Scrolling Speed: {0}, Update Interval: {1})", _scrollSpeed.ToString(), _updateInterval.ToString()), "Rss - Constructor");

            // Items to show and duration
            _numItems = Convert.ToInt32(options.Dictionary.Get("numItems", "0"));
            _durationIsPerItem = Convert.ToInt32(options.Dictionary.Get("durationIsPerItem", "0"));
            _takeItemsFrom = options.Dictionary.Get("takeItemsFrom", "start");

            // Generate a temporary file to store the rendered object in.
            _tempHtml = new TemporaryHtml();

            // Set up the backgrounds
            _backgroundTop = options.backgroundTop + "px";
            _backgroundLeft = options.backgroundLeft + "px";

            _documentText = options.text;
            _documentTemplate = options.documentTemplate;

            // Generate the HTML for the HEAD of the document
            GenerateHeadHtml();

            // Prepare the RSS
            PrepareRSS();
            
            // Create a webbrowser to take the temp file loc
            _webBrowser = new WebBrowser();
            _webBrowser.ScriptErrorsSuppressed = true;
            _webBrowser.Size = this.Size;
            _webBrowser.ScrollBarsEnabled = false;
            _webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(webBrowser_DocumentCompleted);

            // Is the RSS ready to be loaded into the temp location?
            if (_rssReady)
            {
                // Load the RSS
                LoadRssIntoTempFile();

                // Navigate to temp file
                _webBrowser.Navigate(_tempHtml.Path);
            }
        }

        /// <summary>
        /// Prepares the RSS
        /// </summary>
        private void PrepareRSS()
        {
            // If we get to the end of this method and _rssReady is still false
            // the media node will wait until we have refreshed the local rss before 
            // calling LoadRss.
            _rssReady = false;

            // Pull the RSS feed, and put it in a temporary file cache
            // We want to check the file exists first
            _rssFilePath = Properties.Settings.Default.LibraryPath + @"\" + _mediaid + ".xml";

            if (!System.IO.File.Exists(_rssFilePath) || _updateInterval == 0)
            {
                // File doesnt exist - or we always refresh therefore go get the RSS.
                RefreshLocalRss();
            }
            else
            {
                // It exists - therefore we want to get the last time it was updated
                DateTime lastWriteDate = System.IO.File.GetLastWriteTime(_rssFilePath);

                if (DateTime.Now.CompareTo(lastWriteDate.AddHours(_updateInterval * 1.0 / 60.0)) > 0)
                {
                    RefreshLocalRss();
                }
                else
                {
                    // Just use the RSS we already have.
                    _rssReady = true;
                }
            }
        }

        /// <summary>
        /// Generates the Head Html for this Document
        /// </summary>
        private void GenerateHeadHtml()
        {
            // Handle the background
            String bodyStyle;

            if (_backgroundImage == null || _backgroundImage == "")
            {
                bodyStyle = "background-color:" + _backgroundColor + " ;";
            }
            else
            {
                bodyStyle = "background-image: url('" + _backgroundImage + "'); background-attachment:fixed; background-color:" + _backgroundColor + " background-repeat: no-repeat; background-position: " + _backgroundLeft + " " + _backgroundTop + ";";
            }

            // Do we need to include the init function to kick off the text render?
            String initFunction = "";

            if (_direction == "single")
            {
                initFunction = @"
<script type='text/javascript'>
function init() 
{
    var totalDuration = " + _duration.ToString() + @" * 1000;
    var itemCount = $('.XiboRssItem').size();
    var durationIsPerItem = " + _durationIsPerItem.ToString() + @"
    
    if (durationIsPerItem == 0)
        var itemTime = totalDuration / itemCount;
    else
        var itemTime = totalDuration;

    if (itemTime < 2000) itemTime = 2000;

   // Try to get the itemTime from an element we expect to be in the HTML 
   $('#text').cycle({fx: 'fade', timeout:itemTime});
}
</script>";
            }
            else if (_direction != "none")
            {
                initFunction = @"
<script type='text/javascript'>
function init() 
{ 
    tr = new TextRender('text', 'innerText', '" + _direction + @"', " + Properties.Settings.Default.scrollStepAmount.ToString() + @");

    var timer = 0;
    timer = setInterval('tr.TimerTick()', " + _scrollSpeed.ToString() + @");
}
</script>";
            }

            _headText = initFunction + "<style type='text/css'>body {" + bodyStyle + " font-size:" + _scaleFactor.ToString() + "em; }</style>";

            // Store the document text in the temporary HTML space
            _tempHtml.HeadContent = _headText;
        }

        /// <summary>
        /// Refreshes the Local Rss copy from the Remote source
        /// Uses a Async call
        /// </summary>
        private void RefreshLocalRss()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Created at WebClient", "RSS - Refresh local RSS");

                _wc = new System.Net.WebClient();
                _wc.UseDefaultCredentials = true;
                
                _wc.OpenReadCompleted += new System.Net.OpenReadCompletedEventHandler(wc_OpenReadCompleted);
                
                _wc.OpenReadAsync(new Uri(_filePath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", ex.Message, _scheduleId, _layoutId, _mediaid));
            }
        }

        /// <summary>
        /// Load the feed, and substitute the relevant templated fields for each item
        /// Save to a temporary file
        /// </summary>
        private void LoadRssIntoTempFile()
        {
            try
            {
                string localFeedUrl = Properties.Settings.Default.LibraryPath + @"\" + _mediaid + ".xml";

                using (XmlReader reader = XmlReader.Create(localFeedUrl))
                {
                    SyndicationFeed feed = SyndicationFeed.Load(reader);

                    int countItems = 0;
                    int currentItem = 0;

                    foreach (SyndicationItem item in feed.Items)
                        countItems++;

                    // What if the _numItems we want is 0? Take them all.
                    if (_numItems == 0)
                        _numItems = countItems;

                    // Make sure we dont have a count higher than the actual number of items returned
                    if (_numItems > countItems)
                        _numItems = countItems;

                    // Go through each item and construct the HTML for the feed
                    foreach(SyndicationItem item in feed.Items)
                    {
                        currentItem++;

                        if (_takeItemsFrom == "end")
                        {
                            if (currentItem < _numItems)
                                continue;
                        }
                        else
                        {
                            // Take items from the start of the feed
                            if (currentItem > _numItems)
                                continue;
                        }

                        // Load the template into a temporary variable
                        string temp = _documentTemplate;

                        temp = temp.Replace("[Title]", item.Title.Text.ToString());
                        
                        string description;

                        if (item.Summary == null)
                            description = item.ElementExtensions.ReadElementExtensions<string>("encoded", "http://purl.org/rss/1.0/modules/content/")[0].ToString();
                        else
                            description = item.Summary.Text;

                        temp = temp.Replace("[Description]", description);
                        temp = temp.Replace("[Date]", item.PublishDate.ToString("F"));

                        if (item.Links.Count > 0)
                            temp = temp.Replace("[Link]", item.Links[0].Uri.ToString());                       

                        // Assemble the RSS items based on the direction we are displaying
                        if (_direction == "left" || _direction == "right")
                        {
                            // Remove all <p></p> from the temp
                            temp = temp.Replace("<p>", "");
                            temp = temp.Replace("</p>", "");

                            // Sub in the temp to the format string
                            _documentText += string.Format("<span class='article' style='padding-left:4px;'>{0}</span>", temp);
                        }
                        else
                        {
                            _documentText += string.Format("<div class='XiboRssItem' style='display:block;padding:4px;width:{1}'>{0}</div>", temp, this.width - 10);
                        }
                    }

                    // Add the Copyright Notice
                    _documentText += CopyrightNotice;

                    // Decide whether we need a marquee or not
                    if (_direction == "none")
                    {
                        // we dont
                        // set the body of the webBrowser to the document text (altered by the RSS feed)
                        _bodyText = _documentText;
                    }
                    else
                    {

                        String textRender = "";
                        String textWrap = "";
                        if (_direction == "left" || _direction == "right")
                        {
                            // Make sure the text does not wrap when going from left to right.
                            textWrap = "white-space: nowrap";
                            _documentText = String.Format("<nobr>{0}</nobr>", _documentText);
                        }
                        else
                        {
                            // Up and Down
                            textWrap = String.Format("width: {0}px;", this.width - 50);
                        }


                        // If we are displaying a single item at a time we do not need to mask out the inner text
                        if (_direction == "single")
                        {
                            textRender += string.Format("<div id='text'>{0}</div>", _documentText);
                        }
                        else
                        {
                            String startPosition = "left";

                            if (_direction == "right")
                                startPosition = "right";

                            textRender += string.Format("<div id='text' style='position:relative;overflow:hidden;width:{0}px; height:{1}px;'>", this.width - 10, this.height);
                            textRender += string.Format("<div id='innerText' style='position:absolute; {2}: 0px; top: 0px; {0}'>{1}</div></div>", textWrap, _documentText, startPosition);
                        }

                        _bodyText = textRender;
                    }

                    _tempHtml.BodyContent = _bodyText;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", ex.Message, _scheduleId, _layoutId, _mediaid));

                _bodyText = "<h1>Unable to load feed</h1>";
                _tempHtml.BodyContent = _bodyText;

                // Delete the temporary file we have saved - it is clearly not working.
                File.Delete(Properties.Settings.Default.LibraryPath + @"\" + _mediaid + ".xml");
            }
        }

        /// <summary>
        /// The Formatted Copyright notice
        /// </summary>
        private String CopyrightNotice
        {
            get
            {
                if (_direction == "left" || _direction == "right")
                {
                    return String.Format("<span style='font-family: Arial; font-size: 8px;'>{0}</span>", _copyrightNotice);
                }
                else
                {
                    return String.Format("<div style='display:block;font-family: Arial; font-size: 8px;'>{0}</div>", _copyrightNotice); ;
                }
            }
        }

        #region Event Handlers

        /// <summary>
        /// Handles the document completed event.
        /// Resets the Background color to be options.backgroundImage
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            // Show the control
            Show();
            Controls.Add(_webBrowser);
            Application.DoEvents();
        }

        /// <summary>
        /// Refreshes the Local RSS
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wc_OpenReadCompleted(object sender, System.Net.OpenReadCompletedEventArgs e)
        {
            using (WebClient wc = (System.Net.WebClient)sender)
            {
                if (e.Error != null)
                {
                    Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", e.Error, _scheduleId, _layoutId, _mediaid));
                    return;
                }

                try
                {
                    using (StreamReader sr = new StreamReader(e.Result, wc.Encoding))
                    {
                        using (StreamWriter sw = new StreamWriter(File.Open(_rssFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), wc.Encoding))
                        {
                            Debug.WriteLine("Retrieved RSS - about to write it", "RSS - wc_OpenReadCompleted");

                            sw.Write(sr.ReadToEnd());
                        }

                        _rssReady = true;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", ex.Message, _scheduleId, _layoutId, _mediaid));
                }
            }

            try
            {
                if (_rssReady)
                {
                    LoadRssIntoTempFile();

                    // Navigate to temp file
                    _webBrowser.Navigate(_tempHtml.Path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                // The control might have expired by the time this returns
            }
        }

        #endregion

        /// <summary>
        /// Renders the media timers
        /// </summary>
        public override void RenderMedia()
        {
            // Override the duration if necessary
            if (_durationIsPerItem == 1)
                Duration = Duration * _numItems;

            // Start the timer
            base.StartTimer();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _webBrowser.Dispose();

                    System.Diagnostics.Debug.WriteLine("Disposed of the Web Browser control", "Rss - Dispose");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Web browser control already disposed", "Rss - Dispose");
                }

                try
                {
                    _wc.Dispose();
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Web Client control already disposed", "Rss - Dispose");
                }

                // Remove the temporary file we created
                try
                {
                    _tempHtml.Dispose();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("Dispose", String.Format("Unable to dispose TemporaryHtml with exception {0}", ex.Message)));
                }
            }

            base.Dispose(disposing);
        }
    }
}
