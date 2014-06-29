/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2014 Daniel Garner and James Packer
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
using System.IO;
using System.Diagnostics;
using System.Globalization;
using XiboClient.Properties;

namespace XiboClient
{
    class Text : Media
    {
        private string _filePath;
        private string _direction;
        private string _backgroundImage;
        private string _backgroundColor;
        private WebBrowser _webBrowser;
        private string _documentText;
        private String _headText;
        private String _headJavaScript;

        private string _backgroundTop;
        private string _backgroundLeft;
        private double _scaleFactor;
        private int _scrollSpeed;
        private bool _fitText;
        private RegionOptions _options;

        private TemporaryHtml _tempHtml;
        private TemporaryFile _temporaryFile;

        /// <summary>
        /// Creates a Text display control
        /// </summary>
        /// <param name="options">Region Options for this control</param>
        public Text(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            // Collect some options from the Region Options passed in
            // and store them in member variables.
            _options = options;

            _backgroundImage = options.backgroundImage;
            _backgroundColor = options.backgroundColor;
            _backgroundTop = options.backgroundTop + "px";
            _backgroundLeft = options.backgroundLeft + "px";

            // Fire up a webBrowser control to display the completed file.
            _webBrowser = new WebBrowser();
            _webBrowser.Size = this.Size;
            _webBrowser.ScrollBarsEnabled = false;
            _webBrowser.ScriptErrorsSuppressed = true;
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

            // Non XMDS substitutes manually
            _filePath = options.uri;
            _direction = options.direction;
            _documentText = options.text;
            _scrollSpeed = options.scrollSpeed;
            _headJavaScript = options.javaScript;
            _fitText = (options.Dictionary.Get("fitText", "0") == "0" ? false : true);
            
            // Scale Factor
            _scaleFactor = options.scaleFactor;

            // Generate a temporary file to store the rendered object in.
            _tempHtml = new TemporaryHtml();

            // Generate the Head Html and store to file.
            GenerateHeadHtml();
            
            // Generate the Body Html and store to file.
            GenerateBodyHtml();

            // Navigate to temp file
            _webBrowser.Navigate(_tempHtml.Path);
        }

        #region Members

        /// <summary>
        /// Generates the Body Html for this Document
        /// </summary>
        private void GenerateBodyHtml()
        {
            string bodyContent = "";

            // Generate the body content
            bodyContent += "<div id=\"contentPane\" style=\"overflow: none; width:" + _width + "px; height:" + _height + "px;\">";
            bodyContent += "   <div id=\"" + _options.type + "\">";
            bodyContent += "       " + _documentText;
            bodyContent += "   </div>";
            bodyContent += "</div>";
            
            _tempHtml.BodyContent = bodyContent;
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
            headContent += "           type: 'text',";
            headContent += "           direction: '" + _direction + "',";
            headContent += "           duration: " + Duration + ",";
            headContent += "           durationIsPerItem: false,";
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

            _tempHtml.HeadContent = headContent + bodyStyle + _headJavaScript;
        }

        /// <summary>
        /// Render media
        /// </summary>
        public override void RenderMedia()
        {
            
        }

        /// <summary>
        /// Refresh the Local cache of the DataSetView HTML
        /// </summary>
        private void RefreshFromXmds()
        {
            xmds.xmds xmds = new XiboClient.xmds.xmds();
            xmds.GetResourceCompleted += new XiboClient.xmds.GetResourceCompletedEventHandler(xmds_GetResourceCompleted);

            xmds.GetResourceAsync(Settings.Default.ServerKey, Settings.Default.hardwareKey, _options.layoutId, _options.regionId, _options.mediaid, Settings.Default.Version);
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

                _temporaryFile = new TemporaryFile();
                _temporaryFile.FileContent = html;
            }
        }

        private bool HtmlReady()
        {
            // Pull the RSS feed, and put it in a temporary file cache
            // We want to check the file exists first
            if (!File.Exists(_filePath))
                return false;

            // It exists - therefore we want to get the last time it was updated
            DateTime lastWriteDate = System.IO.File.GetLastWriteTime(_filePath);
            
            if (_options.LayoutModifiedDate.CompareTo(lastWriteDate) > 0)
                return false;
            else
                return true;
        }

        #endregion

        #region Event Handlers

        void webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            // We have navigated to the temporary file.
            Controls.Add(_webBrowser);

            base.RenderMedia();
        }

        #endregion

        /// <summary>
        /// Dispose of this text item
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Remove the webbrowser control
                try
                {
                    _webBrowser.Dispose();
                }
                catch
                {
                    System.Diagnostics.Trace.WriteLine(new LogMessage("WebBrowser still in use.", String.Format("Dispose")));
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
