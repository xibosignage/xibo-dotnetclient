/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2012 Daniel Garner and James Packer
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

        private TemporaryHtml _tempHtml;

        /// <summary>
        /// Creates a Text display control
        /// </summary>
        /// <param name="options">Region Options for this control</param>
        public Text(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            // Collect some options from the Region Options passed in
            // and store them in member variables.
            _filePath = options.uri;
            _direction = options.direction;
            _backgroundImage = options.backgroundImage;
            _backgroundColor = options.backgroundColor;
            _backgroundTop = options.backgroundTop + "px";
            _backgroundLeft = options.backgroundLeft + "px";
            _documentText = options.text;
            _scrollSpeed = options.scrollSpeed;
            _headJavaScript = options.javaScript;
            _fitText = (options.Dictionary.Get("fitText", "0") == "0" ? false : true);
            
            // Adjust the scale factor
            // Scale factor is always slightly over stated, we need to reduce it.
            _scaleFactor = options.scaleFactor * 0.85;

            // Generate a temporary file to store the rendered object in.
            _tempHtml = new TemporaryHtml();

            // Generate the Head Html and store to file.
            GenerateHeadHtml();
            
            // Generate the Body Html and store to file.
            GenerateBodyHtml();

            // Fire up a webBrowser control to display the completed file.
            _webBrowser = new WebBrowser();
            _webBrowser.Size = this.Size;
            _webBrowser.ScrollBarsEnabled = false;
            _webBrowser.ScriptErrorsSuppressed = true;
            _webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(webBrowser_DocumentCompleted);

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
            bodyContent += "   <div id=\"text\">";
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
            base.StartTimer();
        }

        #endregion

        #region Event Handlers

        void webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            // We have navigated to the temporary file.
            Show();
            Controls.Add(_webBrowser);
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
            }

            base.Dispose(disposing);
        }
    }
}
