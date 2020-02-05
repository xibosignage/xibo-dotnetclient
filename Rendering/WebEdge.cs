using Microsoft.Toolkit.Wpf.UI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiboClient.Rendering
{
    class WebEdge : WebMedia
    {
        private WebView mWebView;

        public WebEdge(RegionOptions options)
            : base(options)
        {
        }

        /// <summary>
        /// Render Media
        /// </summary>
        public override void RenderMedia()
        {
            // Create the web view we will use
            mWebView = new WebView();

            ((ISupportInitialize)mWebView).BeginInit();

            mWebView.Width = Width;
            mWebView.Height = Height;
            mWebView.Visibility = System.Windows.Visibility.Hidden;
            mWebView.IsPrivateNetworkClientServerCapabilityEnabled = true;
            mWebView.NavigationCompleted += MWebView_NavigationCompleted;
            mWebView.DOMContentLoaded += MWebView_DOMContentLoaded;

            this.MediaScene.Children.Add(mWebView);

            ((ISupportInitialize)mWebView).EndInit();

            // _webBrowser.ScrollBarsEnabled = false;
            // _webBrowser.ScriptErrorsSuppressed = true;

            HtmlUpdatedEvent += WebMediaHtmlUdatedEvent;

            if (IsNativeOpen())
            {
                // Navigate directly
                mWebView.Navigate(_filePath);
            }
            else if (HtmlReady())
            {
                // Write to temporary file
                ReadControlMeta();

                // Navigate to temp file
                mWebView.Navigate(_localWebPath);
            }
            else
            {
                Debug.WriteLine("HTML Resource is not ready to be shown (meaning the file doesn't exist at all) - wait for the download the occur and then show");
            }

            // Render media shows the controls and starts timers, etc
            base.RenderMedia();
        }

        /// <summary>
        /// DOM content loaded event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MWebView_DOMContentLoaded(object sender, Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.WebViewControlDOMContentLoadedEventArgs e)
        {
            Debug.WriteLine(DateTime.Now.ToLongTimeString() + " DOM content loaded", "EdgeWebView");
        }

        /// <summary>
        /// Navigation completed event - this is the last event we get and signifies the page has loaded completely
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MWebView_NavigationCompleted(object sender, Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.WebViewControlNavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Debug.WriteLine(DateTime.Now.ToLongTimeString() + " Navigate Completed to " + e.Uri, "EdgeWebView");

                DocumentCompleted();

                if (!Expired)
                {
                    // Show the browser
                    mWebView.Visibility = System.Windows.Visibility.Visible;
                }
            }
            else
            {
                Trace.WriteLine(new LogMessage("EdgeWebMedia", "Cannot navigate to " + e.Uri + ". e = " + e.WebErrorStatus.ToString()), LogType.Error.ToString());

                // This should exipre the media
                Duration = 5;
                base.RenderMedia();
            }
        }

        /// <summary>
        /// The HTML for this Widget has been updated
        /// </summary>
        /// <param name="url"></param>
        private void WebMediaHtmlUdatedEvent(string url)
        {
            if (mWebView != null)
            {
                mWebView.Navigate(url);
            }
        }
    }
}
