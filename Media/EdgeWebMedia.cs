/**
 * Copyright (C) 2019 Xibo Signage Ltd
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
using Microsoft.Toolkit.Forms.UI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiboClient
{
    class EdgeWebMedia : WebMedia
    {
        private bool _disposed;

        private WebView mWebView;

        public EdgeWebMedia(RegionOptions options)
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

            mWebView.Dock = System.Windows.Forms.DockStyle.Fill;
            mWebView.Size = Size;
            mWebView.Visible = false;
            mWebView.IsPrivateNetworkClientServerCapabilityEnabled = true;
            mWebView.NavigationCompleted += MWebView_NavigationCompleted;

            Controls.Add(mWebView);

            ((ISupportInitialize)mWebView).EndInit();

            // _webBrowser.ScrollBarsEnabled = false;
            // _webBrowser.ScriptErrorsSuppressed = true;

            HtmlUpdatedEvent += IeWebMedia_HtmlUpdatedEvent;

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

        private void MWebView_NavigationCompleted(object sender, Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT.WebViewControlNavigationCompletedEventArgs e)
        {

            Debug.WriteLine("Navigate Completed to " + e.Uri + " " + e.WebErrorStatus.ToString(), "EdgeWebView");

            if (e.IsSuccess)
            {
                DocumentCompleted();

                if (!IsDisposed)
                {
                    // Show the browser
                    mWebView.Visible = true;
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

        private void IeWebMedia_HtmlUpdatedEvent(string url)
        {
            if (mWebView != null)
            {
                mWebView.Navigate(url);
            }
        }

        /// <summary>
        /// Dispose of this text item
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            Debug.WriteLine("Disposing of " + _filePath, "IeWebMedia - Dispose");

            if (disposing)
            {
                // Remove the webbrowser control
                try
                {
                    // Remove the web browser control
                    Controls.Remove(mWebView);

                    // Workaround to remove COM object
                    PerformLayout();

                    // Detatch event and remove
                    if (mWebView != null && !_disposed)
                    {
                        mWebView.NavigationCompleted -= MWebView_NavigationCompleted;
                        mWebView.Dispose();

                        _disposed = true;
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("IeWebMedia - Dispose", "Cannot dispose of web browser. E = " + e.Message), LogType.Info.ToString());
                }
            }

            base.Dispose(disposing);
        }
    }
}
