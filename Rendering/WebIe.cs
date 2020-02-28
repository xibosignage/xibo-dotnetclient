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
using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;

namespace XiboClient.Rendering
{
    class WebIe : WebMedia
    {
        private WebBrowser _webBrowser;

        private string backgroundColor;
        private string backgroundImage;
        private int backgroundLeft;
        private int backgroundTop;

        public WebIe(RegionOptions options)
            : base(options)
        {
            this.backgroundColor = options.Dictionary.Get("backgroundColor", options.backgroundColor);
            this.backgroundImage = options.backgroundImage;
            this.backgroundLeft = options.backgroundLeft;
            this.backgroundTop = options.backgroundTop;
        }

        /// <summary>
        /// Render Media
        /// </summary>
        public override void RenderMedia()
        {
            // Create the web view we will use
            _webBrowser = new WebBrowser();
            _webBrowser.Navigated += _webBrowser_Navigated;
            _webBrowser.Width = Width;
            _webBrowser.Height = Height;
            _webBrowser.Visibility = System.Windows.Visibility.Hidden;

            HtmlUpdatedEvent += IeWebMedia_HtmlUpdatedEvent;

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

            this.MediaScene.Children.Add(_webBrowser);

            // Render media shows the controls and starts timers, etc
            base.RenderMedia();
        }

        /// <summary>
        /// Web Browser finished loading document
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _webBrowser_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            dynamic activeX = this._webBrowser.GetType().InvokeMember(
                "ActiveXInstance", 
                BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic, 
                null, 
                this._webBrowser, 
                new object[] { }
            );

            activeX.Silent = true;

            DocumentCompleted();

            if (!Expired)
            {
                // Show the browser
                _webBrowser.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void IeWebMedia_HtmlUpdatedEvent(string url)
        {
            if (_webBrowser != null)
            {
                _webBrowser.Navigate(url);
            }
        }

        public override void Stop()
        {
            HtmlUpdatedEvent -= IeWebMedia_HtmlUpdatedEvent;
            this._webBrowser.Navigated -= _webBrowser_Navigated;
            this._webBrowser.Dispose();

            base.Stop();
        }

        /// <summary>
        /// Override for Make File Substitutions
        /// For CEF we set Background to Transparent
        /// </summary>
        /// <param name="cachedFile"></param>
        /// <returns></returns>
        protected override string MakeHtmlSubstitutions(string cachedFile)
        {
            // Handle the background
            String bodyStyle;

            if (this.backgroundImage == null || this.backgroundImage == "")
            {
                bodyStyle = "background-color:" + backgroundColor + " ;";
            }
            else
            {
                bodyStyle = "background-image: url('" + this.backgroundImage + "'); background-attachment:fixed; background-color:" + this.backgroundColor 
                    + "; background-repeat: no-repeat; background-position: " + this.backgroundLeft + "px " + this.backgroundTop + "px;";
            }

            string html = cachedFile.Replace("</head>", "<!--START_STYLE_ADJUST--><style type='text/css'>body {" + bodyStyle + " }</style><!--END_STYLE_ADJUST--></head>");
            html = html.Replace("[[ViewPortWidth]]", WidthIntended.ToString());
            html += "<!--VIEWPORT=" + WidthIntended.ToString() + "x" + HeightIntended.ToString() + "-->";
            html += "<!--CACHEDATE=" + DateTime.Now.ToString() + "-->";
            return html;
        }
    }
}
