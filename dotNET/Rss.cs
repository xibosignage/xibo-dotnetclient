/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2013 Daniel Garner
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
using System.Globalization;
using System.Linq;
using FeedDotNet;
using FeedDotNet.Common;
using System.Net.Mime;
using XiboClient.Properties;
using System.Text.RegularExpressions;

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
        private bool _fitText;

        private RegionOptions _options;

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

        private String _bodyText;
        private TemporaryHtml _tempHtml;
        private TemporaryFile _temporaryFile;

        /// <summary>
        /// Creates an RSS position with the RegionOptions parameter
        /// </summary>
        /// <param name="options"></param>
        public Rss(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            // Store the options
            _options = options;

            _layoutId = options.layoutId;
            _duration = options.duration;
            _mediaid = options.mediaid;

            // Set up the backgrounds
            _backgroundTop = options.backgroundTop + "px";
            _backgroundLeft = options.backgroundLeft + "px";
            _backgroundImage = options.backgroundImage;
            _backgroundColor = options.backgroundColor;

            // Update interval
            _updateInterval = options.updateInterval;

            // Create a webbrowser to take the temp file loc
            _webBrowser = new WebBrowser();
            _webBrowser.ScriptErrorsSuppressed = true;
            _webBrowser.Size = this.Size;
            _webBrowser.ScrollBarsEnabled = false;
            _webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(webBrowser_DocumentCompleted);

            // XMDS feed?
            if (options.Dictionary.Get("xmds", "0") == "1")
            {
                // Set the file path
                _filePath = Settings.Default.LibraryPath + @"\" + _options.mediaid + ".htm";

                // Check to see if the HTML is ready for us.
                if (HtmlReady())
                {
                    // Write to temporary file
                    SaveToTemporaryFile();

                    // Navigate to temp file
                    _webBrowser.Navigate(_temporaryFile.Path);
                }
                else
                {
                    RefreshFromXmds();
                }

                return;
            }

            // Get the URI
            if (String.IsNullOrEmpty(options.uri))
            {
                throw new ArgumentNullException("Uri", "The Uri for the RSS feed can not be empty");
            }

            // Try to make a URI out of the file path
            try
            {
                _filePath = Uri.UnescapeDataString(options.uri).Replace('+',' ');
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message, "Rss");

                throw new ArgumentNullException("Uri", "The URI is invalid.");
            }

            Debug.WriteLine("Ticker URL: " + _filePath + ". Options count: " + options.Dictionary.Count.ToString());

            // Set the parameters based on the RegionOptions
            _direction = options.direction;
            _copyrightNotice = options.copyrightNotice;
            _scheduleId = options.scheduleId;
            _fitText = (options.Dictionary.Get("fitText", "0") == "0" ? false : true);

            // Scale factor
            _scaleFactor = options.scaleFactor;
            _scrollSpeed = options.scrollSpeed;

            Debug.WriteLine(String.Format("Scrolling Speed: {0}, Update Interval: {1})", _scrollSpeed.ToString(), _updateInterval.ToString()), "Rss - Constructor");

            // Items to show and duration
            _numItems = Convert.ToInt32(options.Dictionary.Get("numItems", "0"));
            _durationIsPerItem = Convert.ToInt32(options.Dictionary.Get("durationIsPerItem", "0"));
            _takeItemsFrom = options.Dictionary.Get("takeItemsFrom", "start");

            // Generate a temporary file to store the rendered object in.
            _tempHtml = new TemporaryHtml();

            _documentText = options.text;
            _documentTemplate = options.documentTemplate;

            // Generate the HTML for the HEAD of the document
            GenerateHeadHtml();

            // Prepare the RSS
            PrepareRSS();
            
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

            if (!File.Exists(_rssFilePath) || _updateInterval == 0)
            {
                // File doesnt exist - or we always refresh therefore go get the RSS.
                RefreshLocalRss();
            }
            else
            {
                // It exists - therefore we want to get the last time it was updated
                DateTime lastWriteDate = File.GetLastWriteTime(_rssFilePath);

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
            string bodyStyle = "";

            if (_backgroundImage == null || _backgroundImage == "")
                bodyStyle = "<style type='text/css'>body { background-color:" + _backgroundColor + " ; } </style>";
            else
                bodyStyle = "<style type='text/css'>body { background-image: url('" + _backgroundImage + "'); background-attachment:fixed; background-color:" + _backgroundColor + " background-repeat: no-repeat; background-position: " + _backgroundLeft + " " + _backgroundTop + "; } </style>";

            string headContent = "<script type='text/javascript'>";
            headContent += "   function init() { ";
            headContent += "       $('#text').xiboRender({ ";
            headContent += "           type: 'ticker',";
            headContent += "           direction: '" + _direction + "',";
            headContent += "           duration: " + _duration + ",";
            headContent += "           durationIsPerItem: " + ((_durationIsPerItem == 0) ? "true" : "false") + ",";
            headContent += "           numItems: 0,";
            headContent += "           width: " + _width + ",";
            headContent += "           height: " + _height + ",";
            headContent += "           originalWidth: " + _options.originalWidth + ",";
            headContent += "           originalHeight: " + _options.originalHeight + ",";
            headContent += "           scrollSpeed: " + _scrollSpeed + ",";
            headContent += "           fitText: " + ((!_fitText) ? "false" : "true") + ",";
            headContent += "           scaleText: " + ((_fitText) ? "false" : "true") + ",";
            headContent += "           scaleFactor: " + _scaleFactor.ToString(CultureInfo.InvariantCulture);
            headContent += "       });";
            headContent += "   } ";
            headContent += "</script>";

            _tempHtml.HeadContent = headContent + bodyStyle;
        }

        /// <summary>
        /// Refreshes the Local Rss copy from the Remote source
        /// Uses a Async call
        /// </summary>
        private void RefreshLocalRss()
        {
            Debug.WriteLine("Created at WebClient", "RSS - Refresh local RSS");

            _wc = new System.Net.WebClient();
            _wc.UseDefaultCredentials = true;

            _wc.OpenReadCompleted += new System.Net.OpenReadCompletedEventHandler(wc_OpenReadCompleted);
            _wc.OpenReadAsync(new Uri(_filePath));
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

                using (StreamReader sr = new StreamReader(localFeedUrl))
                {
                    Feed feed = FeedReader.Read(sr.BaseStream, _filePath, new FeedReaderSettings());

                    if (FeedReader.HasErrors || feed == null)
                    {
                        throw new Exception("Error reading feed. " + FeedReader.GetErrors()[0]);
                    }

                    // What if the _numItems we want is 0? Take them all.
                    if (_numItems == 0)
                        _numItems = feed.Items.Count;

                    // Make sure we dont have a count higher than the actual number of items returned
                    if (_numItems > feed.Items.Count)
                        _numItems = feed.Items.Count;

                    // Read items
                    int currentItem = 0;

                    // Go through each item and construct the HTML for the feed
                    foreach (FeedItem item in feed.Items)
                    {
                        if (_takeItemsFrom == "end")
                        {
                            if (currentItem < (feed.Items.Count - _numItems))
                            {
                                currentItem++;
                                continue;
                            }
                        }
                        else
                        {
                            // Take items from the start of the feed
                            if (currentItem >= _numItems)
                                break;
                        }

                        // Load the template into a temporary variable
                        string temp = _documentTemplate;

                        // Get some generic items
                        temp = temp.Replace("[Title]", item.Title);
                        temp = temp.Replace("[Summary]", item.Summary);

                        string date = "";
                        if (item.Published.HasValue)
                            date = item.Published.Value.ToString("F");
                        else if (item.Updated.HasValue)
                            date = item.Updated.Value.ToString("F");

                        temp = temp.Replace("[Date]", (item.Published.HasValue) ? item.Published.Value.ToString("F") : "");
                        temp = temp.Replace("[Description]", item.ContentOrSummary);

                        string links = "";
                        foreach (FeedUri uri in item.WebUris)
                            links += " " + uri.Uri;

                        temp = temp.Replace("[Link]", links);

                        // Assemble the RSS items based on the direction we are displaying
                        if (_direction == "left" || _direction == "right")
                        {
                            // Sub in the temp to the format string
                            _documentText += string.Format("<span class='article'>{0}</span>", temp);
                        }
                        else
                        {
                            _documentText += string.Format("<div class='XiboRssItem'>{0}</div>", temp);
                        }

                        // Move onto the next item
                        currentItem++;
                    }

                    // What happens if we have 0 items?
                    if (currentItem == 0)
                        throw new Exception("No items in feed");

                    // Add the Copyright Notice
                    _documentText += CopyrightNotice;

                    if (_direction == "left" || _direction == "right")
                        _documentText = "<nobr>" + _documentText + "</nobr>";

                    string bodyContent = "";

                    // Generate the body content
                    bodyContent += "<div id=\"contentPane\" style=\"overflow: none; width:" + _width + "px; height:" + _height + "px;\">";
                    bodyContent += "   <div id=\"text\">";
                    bodyContent += "       " + _documentText;
                    bodyContent += "   </div>";
                    bodyContent += "</div>";

                    _tempHtml.BodyContent = bodyContent;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("Rss - LoadRssIntoTempFile", string.Format("Message:{0}. ScheduleID:{1},LayoutID:{2},MediaID:{3}", ex.Message, _scheduleId, _layoutId, _mediaid)), LogType.Error.ToString());

                _bodyText = "<h1>Unable to load feed</h1>";
                _tempHtml.BodyContent = _bodyText;

                // Make sure the duration is 10 (i.e. we don't sit on that page for ages)
                _duration = 10;
                Duration = 10;

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

            // No matter what, start the timer now (passing our calculated duration instead of the provided one)
            Duration = _duration;

            // Start the timer
            base.StartTimer();
        }

        /// <summary>
        /// Refreshes the Local RSS
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wc_OpenReadCompleted(object sender, System.Net.OpenReadCompletedEventArgs e)
        {
            try
            {
                using (WebClient wc = (System.Net.WebClient)sender)
                {
                    // Throw a coms error
                    if (e.Error != null)
                        throw e.Error;

                    // Get the encoding for the feed.
                    Encoding encoding;
                    try
                    {
                        ContentType contentType = new ContentType(wc.ResponseHeaders[HttpResponseHeader.ContentType]);
                        encoding = Encoding.GetEncoding(contentType.CharSet);
                    }
                    catch
                    {
                        // Default to UTF-8
                        encoding = Encoding.UTF8;
                    }

                    // Load the feed into a stream and save it to disk
                    using (StreamReader sr = new StreamReader(e.Result, encoding))
                    {
                        using (StreamWriter sw = new StreamWriter(File.Open(_rssFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), encoding))
                        {
                            Debug.WriteLine("Retrieved RSS - about to write it", "RSS - wc_OpenReadCompleted");

                            sw.Write(sr.ReadToEnd());
                        }

                        _rssReady = true;
                    }
                }

                // Load RSS
                LoadRssIntoTempFile();

                // Navigate to temp file
                _webBrowser.Navigate(_tempHtml.Path);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", ex.Message, _scheduleId, _layoutId, _mediaid));

                // Expire this media in 10 seconds
                Duration = 10;
                base.StartTimer();
            }
        }

        #endregion

        /// <summary>
        /// Renders the media timers
        /// </summary>
        public override void RenderMedia()
        {
            // Do nothing in here (we might not have an accurate duration by the time this is called)
        }

        /// <summary>
        /// Refresh the Local cache of the DataSetView HTML
        /// </summary>
        private void RefreshFromXmds()
        {
            xmds.xmds xmds = new XiboClient.xmds.xmds();
            xmds.GetResourceCompleted += new XiboClient.xmds.GetResourceCompletedEventHandler(xmds_GetResourceCompleted);

            xmds.GetResourceAsync(Settings.Default.ServerKey, Settings.Default.hardwareKey, _layoutId, _options.regionId, _options.mediaid, Settings.Default.Version);
        }

        /// <summary>
        /// Refresh Complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void xmds_GetResourceCompleted(object sender, XiboClient.xmds.GetResourceCompletedEventArgs e)
        {
            try
            {
                // Success / Failure
                if (e.Error != null)
                {
                    Trace.WriteLine(new LogMessage("xmds_GetResource", "Unable to get Resource: " + e.Error.Message), LogType.Error.ToString());

                    // Start the timer so that we expire
                    base.RenderMedia();
                }
                else
                {
                    // Write to the library
                    using (StreamWriter sw = new StreamWriter(File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                    {
                        sw.Write(e.Result);
                        sw.Close();
                    }

                    // Write to temporary file
                    SaveToTemporaryFile();

                    // Handle Navigate in here because we will not have done it during first load
                    _webBrowser.Navigate(_temporaryFile.Path);
                }
            }
            catch (ObjectDisposedException)
            {
                Trace.WriteLine(new LogMessage("Rss", "Retrived the data set, stored the document but the media has already expired."), LogType.Error.ToString());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("Rss", "Unknown exception " + ex.Message), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// Updates the position of the background and saves to a temporary file
        /// </summary>
        private void SaveToTemporaryFile()
        {
            // read the contents of the file
            using (StreamReader reader = new StreamReader(_filePath))
            {
                string cachedFile = reader.ReadToEnd();

                // Handle the background
                String bodyStyle;

                if (_backgroundImage == null || _backgroundImage == "")
                {
                    bodyStyle = "background-color:" + _backgroundColor + " ;";
                }
                else
                {
                    bodyStyle = "background-image: url('" + _backgroundImage.Replace('\\', '/') + "'); background-attachment:fixed; background-color:" + _backgroundColor + " background-repeat: no-repeat; background-position: " + _backgroundLeft + " " + _backgroundTop + ";";
                }

                string html = cachedFile.Replace("</head>", "<style type='text/css'>body {" + bodyStyle + " }</style></head>");

                // We also want to parse out the duration using a regular expression
                try
                {
                    Match match = Regex.Match(html, "<!-- DURATION=(.*?) -->");

                    if (match.Success)
                    {
                        // We have a match, so override our duration.
                        _duration = Convert.ToInt32(match.Groups[1].Value);
                    }
                }
                catch
                {
                    Trace.WriteLine(new LogMessage("Rss - SaveToTemporaryFile", "Unable to pull duration using RegEx").ToString());
                }

                _temporaryFile = new TemporaryFile();
                _temporaryFile.FileContent = html;
            }
        }

        private bool HtmlReady()
        {
            // Pull the RSS feed, and put it in a temporary file cache
            // We want to check the file exists first
            if (!File.Exists(_filePath) || _updateInterval == 0)
                return false;

            // It exists - therefore we want to get the last time it was updated
            DateTime lastWriteDate = System.IO.File.GetLastWriteTime(_filePath);

            if (DateTime.Now.CompareTo(lastWriteDate.AddHours(_updateInterval * 1.0 / 60.0)) > 0)
                return false;
            else
                return true;
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

                // Delete the temporary file
                try
                {
                    if (_temporaryFile != null)
                        _temporaryFile.Dispose();
                }
                catch
                {
                    Debug.WriteLine("Unable to delete temporary file for dataset", "DataSetView - Dispose");
                }
            }

            base.Dispose(disposing);
        }
    }
}
