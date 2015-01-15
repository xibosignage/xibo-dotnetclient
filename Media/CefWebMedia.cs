using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using XiboClient.Control;
using Xilium.CefGlue;
using Xilium.CefGlue.WindowsForms;

namespace XiboClient
{
    class CefWebMedia : Media
    {
        private bool _disposed = false;
        private bool _startWhenReady = false;
        private string _filePath;
        private RegionOptions _options;
        private CefWebBrowser _webView;

        public CefWebMedia(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            // Collect some options from the Region Options passed in
            // and store them in member variables.
            _options = options;

            // Set the file path
            _filePath = ApplicationSettings.Default.LibraryPath + @"\" + _options.mediaid + ".htm";
            
            Color backgroundColor = ColorTranslator.FromHtml(_options.backgroundColor);

            CefBrowserSettings settings = new CefBrowserSettings();
            settings.BackgroundColor = new CefColor(backgroundColor.A, backgroundColor.R, backgroundColor.G, backgroundColor.B);

            // Create the web view we will use
            _webView = new CefWebBrowser();
            _webView.BrowserSettings = settings;
            _webView.Dock = DockStyle.Fill;
            _webView.BrowserCreated += _webView_BrowserCreated;
            _webView.LoadEnd += _webView_LoadEnd;
            _webView.Size = Size;

            // Check to see if the HTML is ready for us.
            if (HtmlReady())
            {
                // Write to temporary file
                ReadControlMeta();

                _startWhenReady = true;
            }
            else
            {
                RefreshFromXmds();
            }

            // We need to come up with a way of setting this control to Visible = false here and still kicking
            // off the webbrowser.
            // I think we can do this by hacking some bits into the Cef.WinForms dll.
            // Currently if we set this to false a browser isn't initialised by the library because it initializes it in OnHandleCreated
            // We also need a way to protect against the web browser never being created for some reason.
            // If it isn't then the control will never exipre (we might need to start the timer and then reset it).
            // Maybe:
            // Start the timer and then base.RestartTimer() in _webview_LoadEnd
            //base.StartTimer();
            
            //_webView.Visible = false;

            Controls.Add(_webView);

            // Show the control
            Show();
        }

        void _webView_BrowserCreated(object sender, EventArgs e)
        {
            if (_disposed)
                return;

            if (_startWhenReady)
                // Navigate to temp file
                _webView.Browser.GetMainFrame().LoadUrl(_filePath);
        }

        void _webView_LoadEnd(object sender, LoadEndEventArgs e)
        {
            if (_disposed)
                return;

            // Start the timer
            base.StartTimer();
        }

        public override void RenderMedia()
        {
            // We don't do anything in here as we want to start the timer from when the web view has loaded
        }

        private bool HtmlReady()
        {
            // Pull the RSS feed, and put it in a temporary file cache
            // We want to check the file exists first
            if (!File.Exists(_filePath) || _options.updateInterval == 0)
                return false;

            // It exists - therefore we want to get the last time it was updated
            DateTime lastWriteDate = File.GetLastWriteTime(_filePath);

            if (_options.LayoutModifiedDate.CompareTo(lastWriteDate) > 0 || DateTime.Now.CompareTo(lastWriteDate.AddHours(_options.updateInterval * 1.0 / 60.0)) > 0)
                return false;
            else
            {
                UpdateCacheIfNecessary();
                return true;
            }
        }

        /// <summary>
        /// Updates the position of the background and saves to a temporary file
        /// </summary>
        private void ReadControlMeta()
        {
            // read the contents of the file
            using (StreamReader reader = new StreamReader(_filePath))
            {
                string html = reader.ReadToEnd();

                // We also want to parse out the duration using a regular expression
                try
                {
                    Match match = Regex.Match(html, "<!-- DURATION=(.*?) -->");

                    if (match.Success)
                    {
                        // We have a match, so override our duration.
                        Duration = Convert.ToInt32(match.Groups[1].Value);
                    }
                }
                catch
                {
                    Trace.WriteLine(new LogMessage("Html - SaveToTemporaryFile", "Unable to pull duration using RegEx").ToString());
                }
            }
        }

        /// <summary>
        /// Refresh the Local cache of the DataSetView HTML
        /// </summary>
        private void RefreshFromXmds()
        {
            xmds.xmds xmds = new XiboClient.xmds.xmds();
            xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds;
            xmds.GetResourceCompleted += new XiboClient.xmds.GetResourceCompletedEventHandler(xmds_GetResourceCompleted);

            xmds.GetResourceAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, _options.layoutId, _options.regionId, _options.mediaid, ApplicationSettings.Default.Version);
        }

        /// <summary>
        /// Refresh Complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void xmds_GetResourceCompleted(object sender, XiboClient.xmds.GetResourceCompletedEventArgs e)
        {
            try
            {
                // Success / Failure
                if (e.Error != null)
                {
                    Trace.WriteLine(new LogMessage("xmds_GetResource", "Unable to get Resource: " + e.Error.Message), LogType.Error.ToString());

                    // Start the timer so that we expire
                    base.RenderMedia();
                }
                else
                {
                    // Ammend the resource file so that we can open it directly from the library (this is better than using a tempoary file)
                    string cachedFile = e.Result;

                    // Handle the background
                    String bodyStyle;

                    if (_options.backgroundImage == null || _options.backgroundImage == "")
                    {
                        bodyStyle = "background-color:" + _options.backgroundColor + " ;";
                    }
                    else
                    {
                        bodyStyle = "background-image: url('" + _options.backgroundImage.Replace('\\', '/') + "'); background-attachment:fixed; background-color:" + _options.backgroundColor + "; background-repeat: no-repeat; background-position: " + _options.backgroundLeft + "px " + _options.backgroundTop + "px;";
                    }

                    string html = cachedFile.Replace("</head>", "<style type='text/css'>body {" + bodyStyle + " }</style></head>");
                    html = html.Replace("[[ViewPortWidth]]", _width.ToString());

                    // Write to the library
                    using (StreamWriter sw = new StreamWriter(File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                    {
                        sw.Write(html);
                        sw.Close();
                    }

                    // Read the control meta back out
                    ReadControlMeta();

                    // Handle Navigate in here because we will not have done it during first load
                    _webView.Browser.GetMainFrame().LoadUrl(_filePath);
                }
            }
            catch (ObjectDisposedException)
            {
                Trace.WriteLine(new LogMessage("WebMedia", "Retrived the data set, stored the document but the media has already expired."), LogType.Error.ToString());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("WebMedia", "Unknown exception " + ex.Message), LogType.Error.ToString());

                // This should exipre the media
                Duration = 5;
                base.RenderMedia();
            }
        }

        /// <summary>
        /// Updates the Cache File with the necessary client side injected items
        /// </summary>
        private void UpdateCacheIfNecessary()
        {
            // Ammend the resource file so that we can open it directly from the library (this is better than using a tempoary file)
            string cachedFile = "";

            using (StreamReader reader = new StreamReader(File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                cachedFile = reader.ReadToEnd();
            }

            if (cachedFile.Contains("[[ViewPortWidth]]"))
            {
                // Handle the background
                String bodyStyle;

                if (_options.backgroundImage == null || _options.backgroundImage == "")
                {
                    bodyStyle = "background-color:" + _options.backgroundColor + " ;";
                }
                else
                {
                    bodyStyle = "background-image: url('" + _options.backgroundImage.Replace('\\', '/') + "'); background-attachment:fixed; background-color:" + _options.backgroundColor + "; background-repeat: no-repeat; background-position: " + _options.backgroundLeft + "px " + _options.backgroundTop + "px;";
                }

                string html = cachedFile.Replace("</head>", "<style type='text/css'>body {" + bodyStyle + " }</style></head>");
                html = html.Replace("[[ViewPortWidth]]", _width.ToString());

                // Write to the library
                using (StreamWriter sw = new StreamWriter(File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    sw.Write(html);
                    sw.Close();
                }
            }
        }

        /// <summary>
        /// Dispose of this text item
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
                    if (_webView != null)
                        _webView.Dispose();
                }
                catch
                {
                    Trace.WriteLine(new LogMessage("WebBrowser still in use.", String.Format("Dispose")));
                }
            }

            base.Dispose(disposing);
        }
    }
}
