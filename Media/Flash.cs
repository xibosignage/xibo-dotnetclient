/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2011 Daniel Garner and James Packer
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
using System.Diagnostics;

namespace XiboClient
{
    class Flash 
        : Media
    {
        private TemporaryHtml _tempHtml;
        private WebBrowser _webBrowser; 
        private string _backgroundImage;
        private string _backgroundColor;
        private string _backgroundTop;
        private string _backgroundLeft;
        private bool _disposed = false;
        
        public Flash (RegionOptions options)
            : base(options.width, options.height, options.top, options.left) 
        {
            _tempHtml = new TemporaryHtml();

            _backgroundImage = options.backgroundImage;
            _backgroundColor = options.backgroundColor; 
            _backgroundTop = options.backgroundTop + "px";
            _backgroundLeft = options.backgroundLeft + "px";

            // Create the HEAD of the document
            GenerateHeadHtml();

            // Set the body
            string html = @"
                <object classid='clsid:d27cdb6e-ae6d-11cf-96b8-444553540000' codebase='http://fpdownload.macromedia.com/pub/shockwave/cabs/flash/swflash.cab#version=7,0,0,0' width='{2}' height='{3}' id='analog_clock' align='middle'>
                    <param name='allowScriptAccess' value='sameDomain' />
                    <param name='movie' value='{1}' />
                    <param name='quality' value='high' />
                    <param name='bgcolor' value='#000' />
                    <param name='WMODE' value='transparent' />
                    <embed src='{1}' quality='high' wmode='transparent' bgcolor='#ffffff' width='{2}' height='{3}' name='analog_clock' align='middle' allowScriptAccess='sameDomain' type='application/x-shockwave-flash' pluginspage='http://www.macromedia.com/go/getflashplayer' />
                </object>
            ";

            _tempHtml.BodyContent = string.Format(html, options.uri, options.uri, options.width.ToString(), options.height.ToString());

            // Fire up a webBrowser control to display the completed file.
            _webBrowser = new WebBrowser();
            _webBrowser.Size = this.Size;
            _webBrowser.ScrollBarsEnabled = false;
            _webBrowser.ScriptErrorsSuppressed = true;
            _webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(_webBrowser_DocumentCompleted);

            // Navigate to temp file
            _webBrowser.Navigate(_tempHtml.Path);
            Controls.Add(_webBrowser);

            // Show the control
            Show();
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

            // Store the document text in the temporary HTML space
            _tempHtml.HeadContent = "<style type='text/css'>body {" + bodyStyle + " }</style>"; ;
        }

        /// <summary>
        /// Web browser completed event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            base.StartTimer();

            if (_disposed)
                return;

            _webBrowser.Visible = true;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            _disposed = true;
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
