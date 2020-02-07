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
using CefSharp.Wpf;
using System;
using System.Diagnostics;
using System.Windows.Media.Animation;

namespace XiboClient.Rendering
{
    class WebCef : WebMedia
    {
        private ChromiumWebBrowser webView;
        private string regionId;

        public WebCef(RegionOptions options)
            : base(options)
        {
            this.regionId = options.regionId;
        }

        /// <summary>
        /// Render Media
        /// </summary>
        public override void RenderMedia()
        {
            // Create the web view we will use
            webView = new ChromiumWebBrowser()
            {
                Name = "region_" + this.regionId
            };

            webView.Visibility = System.Windows.Visibility.Hidden;
            webView.Loaded += WebView_Loaded;
            webView.LoadError += WebView_LoadError;

            this.MediaScene.Children.Add(webView);

            HtmlUpdatedEvent += WebMediaHtmlUdatedEvent;

            if (IsNativeOpen())
            {
                // Navigate directly
                webView.Address = _filePath;
            }
            else if (HtmlReady())
            {
                // Write to temporary file
                ReadControlMeta();

                // Navigate to temp file
                webView.Address = _localWebPath;
            }
            else
            {
                Debug.WriteLine("HTML Resource is not ready to be shown (meaning the file doesn't exist at all) - wait for the download the occur and then show");
            }

            // Render media shows the controls and starts timers, etc
            base.RenderMedia();
        }

        /// <summary>
        /// Navigation completed event - this is the last event we get and signifies the page has loaded completely
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Debug.WriteLine(DateTime.Now.ToLongTimeString() + " Navigate Completed", "EdgeWebView");

            // We've finished rendering the document
            DocumentCompleted();

            // If we aren't expired yet, we should show it
            if (!Expired)
            {
                // Show the browser after some time
                webView.Visibility = System.Windows.Visibility.Visible;

                //this.TransitionIn();
            }
        }

        private void WebView_LoadError(object sender, CefSharp.LoadErrorEventArgs e)
        {
            Trace.WriteLine(new LogMessage("EdgeWebMedia", "Cannot navigate. e = " + e.ToString()), LogType.Error.ToString());

            // This should exipre the media
            Duration = 5;
            base.RenderMedia();
        }

        /// <summary>
        /// The HTML for this Widget has been updated
        /// </summary>
        /// <param name="url"></param>
        private void WebMediaHtmlUdatedEvent(string url)
        {
            if (webView != null && !Expired)
            {
                webView.Address = url;
            }
        }

        public override void Stop()
        {
            this.webView.Loaded -= WebView_Loaded;
            this.webView.LoadError -= WebView_LoadError;
            this.webView.Dispose();

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
            string html = cachedFile.Replace("</head>", "<!--START_STYLE_ADJUST--><style type='text/css'>body { background: transparent; }</style><!--END_STYLE_ADJUST--></head>");
            html = html.Replace("[[ViewPortWidth]]", WidthIntended.ToString());
            html += "<!--VIEWPORT=" + WidthIntended.ToString() + "x" + HeightIntended.ToString() + "-->";
            html += "<!--CACHEDATE=" + DateTime.Now.ToString() + "-->";
            return html;
        }
    }
}
