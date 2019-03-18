/**
 * Copyright (C) 2019 Xibo Signage Ltd
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace XiboClient
{
    class IeWebMedia : Media
    {
        private bool _disposed = false;
        protected string _filePath;
        private string _localWebPath;
        private RegionOptions _options;
        private WebBrowser _webBrowser;
        private int _documentCompletedCount = 0;
        private bool _reloadOnXmdsRefresh = false;

        public IeWebMedia(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            // Collect some options from the Region Options passed in
            // and store them in member variables.
            _options = options;

            // Set the file path/local web path
            if (IsNativeOpen())
            {
                // If we are modeid == 1, then just open the webpage without adjusting the file path
                _filePath = Uri.UnescapeDataString(_options.uri).Replace('+', ' ');
            }
            else
            {
                // Set the file path
                _filePath = ApplicationSettings.Default.LibraryPath + @"\" + _options.mediaid + ".htm";
                _localWebPath = ApplicationSettings.Default.EmbeddedServerAddress + _options.mediaid + ".htm";
            }
        }

        /// <summary>
        /// Is this a native open widget
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsNativeOpen()
        {
            string modeId = _options.Dictionary.Get("modeid");
            return modeId != string.Empty && modeId == "1";
        }

        /// <summary>
        /// Render Media
        /// </summary>
        public override void RenderMedia()
        {
            // Create the web view we will use
            _webBrowser = new WebBrowser();
            _webBrowser.DocumentCompleted += _webBrowser_DocumentCompleted;
            _webBrowser.Size = Size;
            _webBrowser.ScrollBarsEnabled = false;
            _webBrowser.ScriptErrorsSuppressed = true;
            _webBrowser.Visible = false;

            if (IsNativeOpen())
            {
                // Navigate directly
                _webBrowser.Navigate(_filePath);
            }
            else if (HtmlReady())
            {
                // Write to temporary file
                ReadControlMeta();

                // Navigate to temp file
                _webBrowser.Navigate(_localWebPath);
            }
            else
            {
                Debug.WriteLine("HTML Resource is not ready to be shown (meaning the file doesn't exist at all) - wait for the download the occur and then show");
            }

            Controls.Add(_webBrowser);

            // Render media shows the controls and starts timers, etc
            base.RenderMedia();
        }

        /// <summary>
        /// Web Browser finished loading document
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            _documentCompletedCount++;

            // Prevent double document completions
            if (_documentCompletedCount > 1)
                return;

            // Start the timer
            base.RestartTimer();

            // Don't do anything if we are already disposed
            if (_disposed)
                return;

            // Show the browser
            _webBrowser.Visible = true;
        }

        /// <summary>
        /// Is the cached HTML ready
        /// </summary>
        /// <returns>true if there is something to show, false if nothing</returns>
        private bool HtmlReady()
        {
            // Check for cached resource files in the library
            // We want to check the file exists first
            if (!File.Exists(_filePath))
            {
                // File doesn't exist at all.
                _reloadOnXmdsRefresh = true;

                // Refresh
                RefreshFromXmds();

                // Return false
                return false;
            }

            // It exists - therefore we want to get the last time it was updated
            DateTime lastWriteDate = File.GetLastWriteTime(_filePath);

            // Does it update every time?
            if (_options.updateInterval == 0)
            {
                // Comment in to force a re-request with each reload of the widget
                //_reloadOnXmdsRefresh = true;

                // File exists but needs updating
                RefreshFromXmds();

                // Comment in to force a re-request with each reload of the widget
                //return false;
            }
            // Compare the last time it was updated to the layout modified time (always refresh when the layout has been modified)
            // Also compare to the update interval (refresh if it has not been updated for longer than the update interval)
            else if (_options.LayoutModifiedDate.CompareTo(lastWriteDate) > 0 || DateTime.Now.CompareTo(lastWriteDate.AddMinutes(_options.updateInterval)) > 0)
            {
                // File exists but needs updating.
                RefreshFromXmds();
            }
            else
            {
                // File exists and is in-date - nothing to do                
            }

            // Refresh the local file cache with any new dimensions, etc.
            UpdateCacheIfNecessary();
            
            return true;
        }

        /// <summary>
        /// Pulls the duration out of the temporary file and sets the media Duration to the same
        /// </summary>
        private void ReadControlMeta()
        {
            // read the contents of the file
            using (StreamReader reader = new StreamReader(_filePath))
            {
                string html = reader.ReadToEnd();

                // Parse out the duration using a regular expression
                try
                {
                    Match match = Regex.Match(html, "<!-- DURATION=(.*?) -->");

                    if (match.Success)
                    {
                        // We have a match, so override our duration.
                        Duration = Convert.ToInt32(match.Groups[1].Value);
                    }
                }
                catch
                {
                    Trace.WriteLine(new LogMessage("Html - ReadControlMeta", "Unable to pull duration using RegEx").ToString());
                }
            }
        }

        /// <summary>
        /// Refresh the Local cache of the DataSetView HTML
        /// </summary>
        private void RefreshFromXmds()
        {
            xmds.xmds xmds = new XiboClient.xmds.xmds();
            xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=getResource";
            xmds.GetResourceCompleted += new XiboClient.xmds.GetResourceCompletedEventHandler(xmds_GetResourceCompleted);

            xmds.GetResourceAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, _options.layoutId, _options.regionId, _options.mediaid, ApplicationSettings.Default.Version);
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
                    Trace.WriteLine(new LogMessage("xmds_GetResource", "Unable to get Resource: " + e.Error.Message), LogType.Info.ToString());

                    // We have failed to update from XMDS
                    // id we have been asked to reload on XmdsRefresh, check to see if we have a file to load,
                    // if not expire on a short timer.
                    if (_reloadOnXmdsRefresh)
                    {
                        if (File.Exists(_filePath))
                        {
                            // Cached file to revert to
                            UpdateCacheIfNecessary();

                            // Navigate to the file
                            _webBrowser.Navigate(_localWebPath);
                        }
                        else
                        {
                            // No cache to revert to
                            Trace.WriteLine(new LogMessage("xmds_GetResource", "We haven't been able to download this widget and there isn't a pre-cached one to use. Skipping."), LogType.Info.ToString());

                            // Start the timer so that we expire
                            Duration = 2;
                            base.RenderMedia();
                        }
                    }
                }
                else
                {
                    // Ammend the resource file so that we can open it directly from the library (this is better than using a tempoary file)
                    string cachedFile = e.Result;

                    // Handle the background
                    String bodyStyle;
                    String backgroundColor = _options.Dictionary.Get("backgroundColor", _options.backgroundColor);

                    if (_options.backgroundImage == null || _options.backgroundImage == "")
                    {
                        bodyStyle = "background-color:" + backgroundColor + " ;";
                    }
                    else
                    {
                        bodyStyle = "background-image: url('" + _options.backgroundImage + "'); background-attachment:fixed; background-color:" + backgroundColor + "; background-repeat: no-repeat; background-position: " + _options.backgroundLeft + "px " + _options.backgroundTop + "px;";
                    }

                    string html = cachedFile.Replace("</head>", "<!--START_STYLE_ADJUST--><style type='text/css'>body {" + bodyStyle + " }</style><!--END_STYLE_ADJUST--></head>");
                    html = html.Replace("[[ViewPortWidth]]", _width.ToString());
                    html += "<!--VIEWPORT=" + _width.ToString() + "x" + _height.ToString() + "-->";
                    html += "<!--CACHEDATE=" + DateTime.Now.ToString() + "-->";

                    // Comment in to write out the update date at the end of the file (in the body)
                    // This is useful if you want to check how frequently the file is updating
                    //html = html.Replace("<body>", "<body><h1 style='color:white'>" + DateTime.Now.ToString() + "</h1>");

                    // Write to the library
                    using (FileStream fileStream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        using (StreamWriter sw = new StreamWriter(fileStream))
                        {
                            sw.Write(html);
                            sw.Close();
                        }
                    }

                    if (_reloadOnXmdsRefresh)
                    {
                        // Read the control meta back out
                        ReadControlMeta();

                        // Handle Navigate in here because we will not have done it during first load
                        _webBrowser.Navigate(_localWebPath);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Trace.WriteLine(new LogMessage("WebMedia", "Retrived the resource, stored the document but the media has already expired."), LogType.Error.ToString());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("WebMedia", "Unknown exception " + ex.Message), LogType.Error.ToString());

                // This should exipre the media
                Duration = 5;
                base.RenderMedia();
            }
        }

        /// <summary>
        /// Updates the Cache File with the necessary client side injected items
        /// </summary>
        private void UpdateCacheIfNecessary()
        {
            // Ammend the resource file so that we can open it directly from the library (this is better than using a tempoary file)
            string cachedFile = "";

            using (FileStream fileStream = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    cachedFile = reader.ReadToEnd();
                }
            }

            // Compare the cached dimensions in the file with the dimensions now, and 
            // regenerate if they are different.
            if (cachedFile.Contains("[[ViewPortWidth]]") || !ReadCachedViewPort(cachedFile).Equals(_width.ToString() + "x" + _height.ToString()))
            {
                // Regex out the existing replacement if present
                cachedFile = Regex.Replace(cachedFile, "<!--START_STYLE_ADJUST-->(.*)<!--END_STYLE_ADJUST-->", "");
                cachedFile = Regex.Replace(cachedFile, "<meta name=\"viewport\" content=\"width=(.*)\" />", "<meta name=\"viewport\" content=\"width=[[ViewPortWidth]]\" />");
                cachedFile = Regex.Replace(cachedFile, "<!--VIEWPORT=(.*)-->", "");
                cachedFile = Regex.Replace(cachedFile, "<!--CACHEDATE=(.*)-->", "");

                // Handle the background
                String bodyStyle;
                String backgroundColor = _options.Dictionary.Get("backgroundColor", _options.backgroundColor);

                if (_options.backgroundImage == null || _options.backgroundImage == "")
                {
                    bodyStyle = "background-color:" + backgroundColor + " ;";
                }
                else
                {
                    bodyStyle = "background-image: url('" + _options.backgroundImage + "'); background-attachment:fixed; background-color:" + backgroundColor + "; background-repeat: no-repeat; background-position: " + _options.backgroundLeft + "px " + _options.backgroundTop + "px;";
                }

                string html = cachedFile.Replace("</head>", "<!--START_STYLE_ADJUST--><style type='text/css'>body {" + bodyStyle + " }</style><!--END_STYLE_ADJUST--></head>");
                html = html.Replace("[[ViewPortWidth]]", _width.ToString());
                html += "<!--VIEWPORT=" + _width.ToString() + "x" + _height.ToString() + "-->";
                html += "<!--CACHEDATE=" + DateTime.Now.ToString() + "-->";

                // Write to the library
                using (FileStream fileStream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    using (StreamWriter sw = new StreamWriter(fileStream))
                    {
                        sw.Write(html);
                        sw.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Pulls the duration out of the temporary file and sets the media Duration to the same
        /// </summary>
        private string ReadCachedViewPort(string html)
        {
            // Parse out the duration using a regular expression
            try
            {
                Match match = Regex.Match(html, "<!--VIEWPORT=(.*?)-->");

                if (match.Success)
                {
                    // We have a match, so override our duration.
                    return match.Groups[1].Value;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Dispose of this text item
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            Debug.WriteLine("Disposing of " + _filePath, "IeWebMedia - Dispose");

            if (disposing)
            {
                // Remove the webbrowser control
                try
                {
                    // Remove the web browser control
                    Controls.Remove(_webBrowser);

                    // Workaround to remove COM object
                    PerformLayout();

                    // Detatch event and remove
                    if (_webBrowser != null && !_disposed)
                    {
                        _webBrowser.DocumentCompleted -= _webBrowser_DocumentCompleted;
                        _webBrowser.Navigate("about:blank");
                        _webBrowser.Dispose();

                        _disposed = true;
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("IeWebMedia - Dispose", "Cannot dispose of web browser. E = " + e.Message), LogType.Info.ToString());
                }
            }

            base.Dispose(disposing);
        }
    }
}
