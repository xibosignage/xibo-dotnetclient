using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace XiboClient.Rendering
{
    class WebIe : WebMedia
    {
        private WebBrowser _webBrowser;

        public WebIe(RegionOptions options)
            : base(options)
        {
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
    }
}
