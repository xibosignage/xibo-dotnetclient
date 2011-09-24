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
using System.IO;
using System.Diagnostics;

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
            _scaleFactor = options.scaleFactor;
            _backgroundTop = options.backgroundTop + "px";
            _backgroundLeft = options.backgroundLeft + "px";
            _documentText = options.text;
            _scrollSpeed = options.scrollSpeed;
            _headJavaScript = options.javaScript;
            
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
            String startPosition = "left";

            if (_direction == "right")
                startPosition = "right";

            // Generate the Body
            if (_direction == "none")
            {
                // Just use the RAW text that was in the XLF
                _tempHtml.BodyContent = _documentText;
            }
            else
            {
                // Format the text in some way
                String textRender = "";
                String textWrap = "";

                if (_direction == "left" || _direction == "right") textWrap = "white-space: nowrap";

                textRender += string.Format("<div id='text' style='position:relative;overflow:hidden;width:{0}px; height:{1}px;'>", this.width - 10, this.height);
                textRender += string.Format("<div id='innerText' style='position:absolute; {3}: 0px; top: 0px; width:{2}px; {0}'>{1}</div></div>", textWrap, _documentText, this.width - 10, startPosition);

                _tempHtml.BodyContent = textRender;
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

            if (_direction != "none")
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

            _headText = _headJavaScript + initFunction + "<style type='text/css'>body {" + bodyStyle + " font-size:" + _scaleFactor.ToString() + "em; }</style>";

            // Store the document text in the temporary HTML space
            _tempHtml.HeadContent = _headText;
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
            Application.DoEvents();
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
