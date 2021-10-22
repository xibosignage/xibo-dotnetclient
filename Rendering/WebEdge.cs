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
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Diagnostics;

namespace XiboClient.Rendering
{
    /// <summary>
    /// Web Media using Microsoft's "WebView2" control
    /// note that this control supports transparency now, but does not support layering
    /// this is referred to as an "airspace" issue
    /// https://github.com/MicrosoftEdge/WebView2Feedback/issues/356
    /// </summary>
    class WebEdge : WebMedia
    {
        private readonly WebView2 webView;
        private bool _webViewInitialised = false;
        private bool _webViewError = false;
        
        /// <summary>
        /// A flag to indicate whether we have loaded web content or not.
        /// </summary>
        private bool hasLoaded = false;

        private readonly bool hasBackgroundColor = false;
        private readonly bool isPinchToZoomEnabled = false;
        private bool _renderCalled = false;
        private double _position;

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="options"></param>
        public WebEdge(MediaOptions options) : base(options)
        {
            this.hasBackgroundColor = !string.IsNullOrEmpty(options.Dictionary.Get("backgroundColor", ""));

            this.webView = new WebView2
            {
                Width = Width,
                Height = Height,
                Visibility = System.Windows.Visibility.Visible,
                DefaultBackgroundColor = System.Drawing.Color.Transparent,
                Focusable = false,
            };
            this.webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
            this.webView.NavigationCompleted += WebView_NavigationCompleted;
            this.isPinchToZoomEnabled = options.IsPinchToZoomEnabled;

            // Initialise the web view
            InitialiseWebView();
        }

        private async void InitialiseWebView()
        {
            // Environment
            CoreWebView2EnvironmentOptions environmentOptions;

            // NTLM/Auth Server White Lists.
            if (!string.IsNullOrEmpty(ApplicationSettings.Default.AuthServerWhitelist))
            {
                string command = "--auth-server-whitelist " + ApplicationSettings.Default.AuthServerWhitelist;
                command += " --auth-negotiate-delegate-whitelist " + ApplicationSettings.Default.AuthServerWhitelist;

                environmentOptions = new CoreWebView2EnvironmentOptions(command);
            }
            else
            {
                environmentOptions = new CoreWebView2EnvironmentOptions();
            }

            await this.webView.EnsureCoreWebView2Async(
                await CoreWebView2Environment.CreateAsync(
                        null,
                        ApplicationSettings.Default.LibraryPath,
                        environmentOptions));

            // Proxy
            // Not yet supported https://github.com/MicrosoftEdge/WebView2Feedback/issues/132
            /*if (!string.IsNullOrEmpty(ApplicationSettings.Default.ProxyUser))
            {
                
            }*/
        }

        /// <summary>
        /// Render Media
        /// </summary>
        public override void RenderMedia(double position)
        {
            _renderCalled = true;
            _position = position;

            this.MediaScene.Children.Add(this.webView);

            if (_webViewInitialised || _webViewError)
            {
                Navigate();
            }
        }

        /// <summary>
        /// WebView has finished initialising.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebView_CoreWebView2InitializationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                webView.CoreWebView2.Settings.IsPinchZoomEnabled = isPinchToZoomEnabled;
                _webViewInitialised = true;
            }
            else
            {
                Trace.WriteLine(new LogMessage("WebView", "WebView_CoreWebView2InitializationCompleted: e = "
                    + e.InitializationException.Message), LogType.Error.ToString());

                _webViewError = true;
            }

            if (_renderCalled)
            {
                Navigate();
            }
        }

        /// <summary>
        /// Do navigation
        /// </summary>
        private void Navigate()
        {
            if (_webViewError)
            {
                // This should exipre the media
                Duration = 5;
                base.RestartTimer();
            }

            if (IsNativeOpen())
            {
                // Navigate directly
                this.webView.CoreWebView2.Navigate(_filePath);
            }
            else if (HtmlReady())
            {
                // Write to temporary file
                ReadControlMeta();

                // Navigate to temp file
                this.webView.CoreWebView2.Navigate(_localWebPath);
            }
            else
            {
                Debug.WriteLine("HTML Resource is not ready to be shown (meaning the file doesn't exist at all) - wait for the download the occur and then show");
            }

            // Render media shows the controls and starts timers, etc
            base.RenderMedia(_position);
        }

        /// <summary>
        /// Html updated
        /// </summary>
        /// <param name="url"></param>
        private void WebEdge_HtmlUpdatedEvent(string url)
        {
            if (this.webView != null && webView.CoreWebView2 != null)
            {
                this.webView.CoreWebView2.Navigate(url);
            }
        }

        /// <summary>
        /// Navigation Complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Debug.WriteLine("WebView_NavigationCompleted: Navigate Completed", "WebView");
                hasLoaded = true;

                DocumentCompleted();

                // Initialise Interactive Control
                webView.ExecuteScriptAsync("xiboIC.config({hostname:\"localhost\", port: "
                    + ApplicationSettings.Default.EmbeddedServerPort + "})");
            }
            else if (hasLoaded && e.WebErrorStatus == CoreWebView2WebErrorStatus.ConnectionAborted)
            {
                Trace.WriteLine(new LogMessage("WebView", "WebView_LoadError: Abort received, ignoring."), LogType.Audit.ToString());
            }
            else
            {
                Trace.WriteLine(new LogMessage("WebView", "WebView_NavigationCompleted: e = " + e.WebErrorStatus.ToString()), LogType.Error.ToString());

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
            this.webView.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
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
            // Check to see if the document already has a background-color, and if it does, leave it alone.
            string html = cachedFile;
            if (!this.hasBackgroundColor)
            {
                html = cachedFile.Replace("</head>", "<!--START_STYLE_ADJUST--><style type='text/css'>body { background: transparent; }</style><!--END_STYLE_ADJUST--></head>");
            }
            html = html.Replace("[[ViewPortWidth]]", WidthIntended.ToString());
            html += "<!--VIEWPORT=" + WidthIntended.ToString() + "x" + HeightIntended.ToString() + "-->";
            html += "<!--CACHEDATE=" + DateTime.Now.ToString() + "-->";
            return html;
        }
    }
}
