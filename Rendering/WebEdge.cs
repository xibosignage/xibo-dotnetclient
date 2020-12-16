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
using Microsoft.Toolkit.Wpf.UI.Controls;
using System;
using System.Diagnostics;

namespace XiboClient.Rendering
{
    class WebEdge : WebMedia
    {
        private WebView webView;

        private string backgroundColor;
        private string backgroundImage;
        private int backgroundLeft;
        private int backgroundTop;

        public WebEdge(RegionOptions options) : base(options)
        {
            this.backgroundColor = options.Dictionary.Get("backgroundColor", options.backgroundColor);
            this.backgroundImage = options.backgroundImage;
            this.backgroundLeft = options.backgroundLeft;
            this.backgroundTop = options.backgroundTop;
        }

        /// <summary>
        /// Render Media
        /// </summary>
        public override void RenderMedia(double position)
        {
            this.webView = new WebView();
            this.webView.Width = Width;
            this.webView.Height = Height;
            this.webView.IsPrivateNetworkClientServerCapabilityEnabled = true;
            this.webView.Visibility = System.Windows.Visibility.Hidden;
            this.webView.NavigationCompleted += WebView_NavigationCompleted;

            HtmlUpdatedEvent += WebEdge_HtmlUpdatedEvent;

            this.MediaScene.Children.Add(this.webView);

            if (IsNativeOpen())
            {
                // Navigate directly
                this.webView.Navigate(_filePath);
            }
            else if (HtmlReady())
            {
                // Write to temporary file
                ReadControlMeta();

                // Navigate to temp file
                this.webView.Navigate(_localWebPath);
            }
            else
            {
                Debug.WriteLine("HTML Resource is not ready to be shown (meaning the file doesn't exist at all) - wait for the download the occur and then show");
            }

            // Render media shows the controls and starts timers, etc
            base.RenderMedia(position);
        }

        /// <summary>
        /// Html updated
        /// </summary>
        /// <param name="url"></param>
        private void WebEdge_HtmlUpdatedEvent(string url)
        {
            if (this.webView != null)
            {
                this.webView.Navigate(url);
            }
        }

        /// <summary>
        /// Navigation Complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebView_NavigationCompleted(object sender, Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.WebViewControlNavigationCompletedEventArgs e)
        {
            Debug.WriteLine("Navigate Completed to " + e.Uri + " " + e.WebErrorStatus.ToString(), "WebEdge");

            if (e.IsSuccess)
            {
                DocumentCompleted();

                if (!Expired)
                {
                    // Show the browser
                    this.webView.Visibility = System.Windows.Visibility.Visible;
                }
            }
            else
            {
                Trace.WriteLine(new LogMessage("WebEdge", "Cannot navigate to " + e.Uri + ". e = " + e.WebErrorStatus.ToString()), LogType.Error.ToString());

                // This should exipre the media
                Duration = 5;
                base.RestartTimer();
            }
        }

        /// <summary>
        /// Stop
        /// </summary>
        public override void Stopped()
        {
            HtmlUpdatedEvent -= WebEdge_HtmlUpdatedEvent;
            this.webView.NavigationCompleted -= WebView_NavigationCompleted;
            this.webView.Dispose();

            base.Stopped();
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
                bodyStyle = "background-image: url('" + this.backgroundImage + "'); background-attachment:fixed; background-color:" + this.backgroundColor + "; background-repeat: no-repeat; background-position: " + this.backgroundLeft + "px " + this.backgroundTop + "px;";
            }

            string html = cachedFile.Replace("</head>", "<!--START_STYLE_ADJUST--><style type='text/css'>body {" + bodyStyle + " }</style><!--END_STYLE_ADJUST--></head>");
            html = html.Replace("[[ViewPortWidth]]", WidthIntended.ToString());
            html += "<!--VIEWPORT=" + WidthIntended.ToString() + "x" + HeightIntended.ToString() + "-->";
            html += "<!--CACHEDATE=" + DateTime.Now.ToString() + "-->";
            return html;
        }
    }
}
