/**
 * Copyright (C) 2026 Xibo Signage Ltd
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
using System.Threading.Tasks;

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
        private static Task<CoreWebView2Environment> _sharedEnvironmentTask;
        private static readonly object _environmentLock = new object();

        private readonly WebView2 webView;
        private bool _webViewInitialised = false;
        private bool _webViewError = false;
        private CoreWebView2DevToolsProtocolEventReceiver _devToolsReceiver;

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

            // Start with System.Windows.Visibility.Visible due to an issue loading content if started Hidden
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

        private static Task<CoreWebView2Environment> GetOrCreateSharedEnvironmentAsync(
            string userDataFolder, CoreWebView2EnvironmentOptions options)
        {
            if (_sharedEnvironmentTask == null)
            {
                lock (_environmentLock)
                {
                    if (_sharedEnvironmentTask == null)
                    {
                        _sharedEnvironmentTask = CoreWebView2Environment.CreateAsync(
                            null, userDataFolder, options);
                    }
                }
            }
            return _sharedEnvironmentTask;
        }

        private async void InitialiseWebView()
        {
            try
            {
                // Environment options
                CoreWebView2EnvironmentOptions environmentOptions;

                // Where should we store user data?
                string userDataFolder = ApplicationSettings.Default.LibraryPath;

                // Workaround for paths which do not have a trailing slash and are therefore not detected as absolute
                // e.g. E:
                if (!userDataFolder.EndsWith("\\") && !userDataFolder.EndsWith("/"))
                {
                    userDataFolder += "\\";
                }

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

                // Single Sign On?
                if (ApplicationSettings.Default.AllowSingleSignOnUsingOSPrimaryAccount)
                {
                    environmentOptions.AllowSingleSignOnUsingOSPrimaryAccount = true;
                }

                await this.webView.EnsureCoreWebView2Async(
                    await GetOrCreateSharedEnvironmentAsync(userDataFolder, environmentOptions));

                // Proxy
                // Not yet supported https://github.com/MicrosoftEdge/WebView2Feedback/issues/132
                /*if (!string.IsNullOrEmpty(ApplicationSettings.Default.ProxyUser))
                {

                }*/

                // Console logs
                _devToolsReceiver = this.webView.CoreWebView2.GetDevToolsProtocolEventReceiver("Log.entryAdded");
                _devToolsReceiver.DevToolsProtocolEventReceived += OnConsoleMessage;
                await this.webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Log.enable", "{}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("WebEdge", "InitialiseWebView: Exception. e = "
                    + ex.Message), LogType.Error.ToString());

                _webViewError = true;

                if (_renderCalled)
                {
                    Navigate();
                }
            }
        }

        /// <summary>
        /// Render Media
        /// </summary>
        public override void RenderMedia(double position)
        {
            _renderCalled = true;
            _position = position;

            this.MediaScene.Children.Add(this.webView);

            HtmlUpdatedEvent += WebEdge_HtmlUpdatedEvent;

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
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.ProcessFailed += WebView_ProcessFailed;
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
        /// WebView2 renderer or GPU process crashed (shows sad-face page).
        /// Expire the widget so the layout manager can reload it.
        /// </summary>
        private void WebView_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            Trace.WriteLine(new LogMessage("WebEdge",
                "WebView_ProcessFailed: kind=" + e.ProcessFailedKind
                + ", reason=" + e.Reason), LogType.Error.ToString());

            // Detach now so a subsequent failure on the defunct CoreWebView2 cannot
            // re-enter this handler on a WebView2 that is about to be disposed.
            try
            {
                if (this.webView != null && this.webView.CoreWebView2 != null)
                {
                    this.webView.CoreWebView2.ProcessFailed -= WebView_ProcessFailed;
                }
            }
            catch
            {
                // CoreWebView2 may already be torn down; ignore.
            }

            // For a browser-process exit the shared environment is now defunct; reset it
            // so the next WebEdge instance recreates it.
            if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
            {
                lock (_environmentLock)
                {
                    _sharedEnvironmentTask = null;
                }
            }

            // Expire this widget so the layout manager reloads it.
            Duration = 5;
            base.RestartTimer();
        }

        /// <summary>
        /// Do navigation
        /// </summary>
        private void Navigate()
        {
            if (_webViewError)
            {
                // This should expire the media
                Duration = 5;
                base.RestartTimer();
                return;
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
                // This should expire the media
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
                    Trace.WriteLine(new LogMessage("WebView", "WebView_NavigationCompleted: e = " + e.WebErrorStatus.ToString()), LogType.Error.ToString());
                }
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
            if (this.webView.CoreWebView2 != null)
            {
                this.webView.CoreWebView2.ProcessFailed -= WebView_ProcessFailed;
            }
            if (_devToolsReceiver != null)
            {
                _devToolsReceiver.DevToolsProtocolEventReceived -= OnConsoleMessage;
                _devToolsReceiver = null;
            }

            // Mirror of the CEF fix for xibosignage/xibo-dotnetclient#348: pause any active
            // media and navigate to about:blank so the WebView2 host tears down the media
            // element through the document's own unload path before Dispose().
            try
            {
                if (this.webView.CoreWebView2 != null)
                {
                    this.webView.CoreWebView2.ExecuteScriptAsync(
                        "try{document.querySelectorAll('audio,video').forEach(function(m){m.pause();m.removeAttribute('src');m.load();});}catch(e){}");
                    this.webView.CoreWebView2.Navigate("about:blank");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("WebEdge", "Stopped: pre-dispose media cleanup failed. e = " + ex.Message), LogType.Audit.ToString());
            }

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

        /// <summary>
        /// Log console messages
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConsoleMessage(object sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            if (e != null && e.ParameterObjectAsJson != null)
            {
                Trace.WriteLine("WebView2:" + e.ParameterObjectAsJson);
            }
        }
    }
}
