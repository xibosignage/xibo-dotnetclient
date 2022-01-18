/**
 * Copyright (C) 2021 Xibo Signage Ltd
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
using XiboClient.Helpers;

namespace XiboClient.Rendering
{
    class WebCef : WebMedia
    {
        private ChromiumWebBrowser webView;
        private readonly string regionId;
        private readonly bool hasBackgroundColor = false;

        /// <summary>
        /// A flag to indicate whether we have loaded web content or not.
        /// </summary>
        private bool hasLoaded = false;

        public WebCef(MediaOptions options)
            : base(options)
        {
            this.regionId = options.regionId;
            this.hasBackgroundColor = !string.IsNullOrEmpty(options.Dictionary.Get("backgroundColor", ""));
        }

        /// <summary>
        /// Render Media
        /// </summary>
        public override void RenderMedia(double position)
        {
            Debug.WriteLine("Created CEF Renderer for " + this.regionId, "WebCef");

            // Set a cache path
            string cachePath = ApplicationSettings.Default.LibraryPath + @"\CEF";
            var requestContextSettings = new CefSharp.RequestContextSettings { CachePath = cachePath };

            // Create the web view we will use
            webView = new ChromiumWebBrowser()
            {
                Name = "region_" + this.regionId
            };
            webView.RequestContext = new CefSharp.RequestContext(requestContextSettings);

            // Configure run time CEF settings?
            CefSharp.Cef.UIThreadTaskFactory.StartNew(() =>
            {
                try
                {
                    // Provide our own request handler.
                    webView.RequestHandler = new XiboRequestHandler(!string.IsNullOrEmpty(ApplicationSettings.Default.ProxyUser));

                    // NTLM/Auth Server White Lists.
                    if (!string.IsNullOrEmpty(ApplicationSettings.Default.AuthServerWhitelist))
                    {
                        if (!webView.RequestContext.SetPreference("auth.server_whitelist", ApplicationSettings.Default.AuthServerWhitelist, out string error))
                        {
                            Trace.WriteLine(new LogMessage("WebCef", "RenderMedia: auth.server_whitelist. e = " + error), LogType.Info.ToString());
                        }

                        if (!webView.RequestContext.SetPreference("auth.negotiate_delegate_whitelist", ApplicationSettings.Default.AuthServerWhitelist, out string error2))
                        {
                            Trace.WriteLine(new LogMessage("WebCef", "RenderMedia: auth.negotiate_delegate_whitelist. e = " + error2), LogType.Info.ToString());
                        }
                    }
                } 
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("WebCef", "RenderMedia: Exception setting auto policies on cef. e = " + e.Message), LogType.Info.ToString());
                }
            });

            webView.Visibility = System.Windows.Visibility.Hidden;
            webView.Loaded += WebView_Loaded;
            webView.LoadError += WebView_LoadError;
            webView.FrameLoadEnd += WebView_FrameLoadEnd;
            webView.JsDialogHandler = new CefJsDialogHandler();

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
            base.RenderMedia(position);
        }

        /// <summary>
        /// Main Frame finished loading.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebView_FrameLoadEnd(object sender, CefSharp.FrameLoadEndEventArgs e)
        {
            Debug.WriteLine(DateTime.Now.ToLongTimeString() + " Frame Loaded", "CefWebView");

            // Flag that we've opened.
            hasLoaded = true;

            // If we aren't expired yet, we should show it
            if (e.Frame.IsMain && !Expired && !IsNativeOpen())
            {
                // Initialise Interactive Control
                webView.GetBrowser().MainFrame.ExecuteJavaScriptAsync("xiboIC.config({hostname:\"localhost\", port: "
                    + ApplicationSettings.Default.EmbeddedServerPort + "})");
            }
        }

        /// <summary>
        /// Navigation completed event - this is the last event we get and signifies the control has loaded completely
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Debug.WriteLine(DateTime.Now.ToLongTimeString() + " Navigate Completed", "CefWebView");

            // Flag that we've opened.
            hasLoaded = true;

            // Show the browser after some time
            if (!Expired)
            {
                webView.Visibility = System.Windows.Visibility.Visible;

                //this.TransitionIn();
            }

            // We've finished rendering the control
            DocumentCompleted();
        }

        /// <summary>
        /// Load Error
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WebView_LoadError(object sender, CefSharp.LoadErrorEventArgs e)
        {
            // We are not interested in aborted errors.
            if (e.ErrorCode == CefSharp.CefErrorCode.Aborted && hasLoaded)
            {
                Trace.WriteLine(new LogMessage("WebCef", "WebView_LoadError: Abort received, ignoring."), LogType.Audit.ToString());
            }
            else
            {
                // This should expire the media in a short while
                Duration = 5;
                base.RestartTimer();

                // If we have a trigger to use, then fire it off (we will still expire if this isn't handled)
                if (!string.IsNullOrEmpty(PageLoadErrorTrigger))
                {
                    // Fire off the page load error trigger.
                    TriggerWebhook(PageLoadErrorTrigger);
                } 
                else
                {
                    // Unexpected, so log.
                    Trace.WriteLine(new LogMessage("WebCef", "WebView_LoadError: Cannot navigate. e = " + e.ErrorText + ", code = " + e.ErrorCode), LogType.Error.ToString());
                }
            }
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

        public override void Stopped()
        {
            HtmlUpdatedEvent -= WebMediaHtmlUdatedEvent;
            this.webView.Loaded -= WebView_Loaded;
            this.webView.LoadError -= WebView_LoadError;
            this.webView.FrameLoadEnd -= WebView_FrameLoadEnd;
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
