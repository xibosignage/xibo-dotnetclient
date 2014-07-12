/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2012 Daniel Garner
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
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;

namespace XiboClient
{
    class WebContent : Media
    {
        int scheduleId;
        int layoutId;
        string mediaId;
        string type;

        string _filePath;
        WebBrowser webBrowser;
        int duration;

        public WebContent(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            duration        = options.duration;
            scheduleId      = options.scheduleId;
            layoutId        = options.layoutId;
            mediaId         = options.mediaid;
            type = options.type;

            webBrowser = new WebBrowser();

            webBrowser.Size = this.Size;
            webBrowser.ScrollBarsEnabled = false;
            webBrowser.ScriptErrorsSuppressed = true;

            // Offset?
            double offsetTop = Convert.ToDouble(options.Dictionary.Get("offsetTop", "0"));
            double offsetLeft = Convert.ToDouble(options.Dictionary.Get("offsetLeft", "0"));
            double scaling = Convert.ToDouble(options.Dictionary.Get("scaling", "100"));

            // Attach event
            webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(webBrowser_DocumentCompleted);

            if (!Properties.Settings.Default.powerpointEnabled && options.type == "powerpoint")
            {
                webBrowser.DocumentText = "<html><body><h1>Powerpoint not enabled on this display</h1></body></html>";
                
                Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", "Powerpoint is not enabled on this display", scheduleId, layoutId, mediaId));
            }
            else
            {
                try
                {
                    // Try to make a URI out of the file path
                    try
                    {
                        _filePath = Uri.UnescapeDataString(options.uri).Replace('+', ' ');
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(new LogMessage("WebContent", "Unable to get a URI with exception: " + ex.Message), LogType.Audit.ToString());
                    }

                    if (offsetLeft == 0 && offsetTop == 0 && scaling == 100)
                    {
                        webBrowser.Navigate(_filePath);
                    }
                    else
                    {
                        double w = Convert.ToDouble(options.width);
                        double h = Convert.ToDouble(options.height);
                        string zoom = "";

                        // Scale?
                        if (scaling != 100)
                        {
                            // Convert from percentage
                            scaling = scaling / 100;

                            // Alter the width and height
                            w = w * (1 / scaling);
                            h = h * (1 / scaling);
                            zoom = "zoom: " + scaling.ToString(CultureInfo.InvariantCulture) + ";";
                        }

                        // Load an IFRAME into the DocumentText
                        string iframe = "<html><body style='margin:0; border:0;'><iframe style='border:0;margin-left:-" + offsetLeft.ToString(CultureInfo.InvariantCulture) + "px; margin-top:-" + offsetTop.ToString(CultureInfo.InvariantCulture) + "px;" + zoom + "' scrolling=\"no\" width=\"" + (w + offsetLeft).ToString(CultureInfo.InvariantCulture) + "px\" height=\"" + (h + offsetTop).ToString(CultureInfo.InvariantCulture) + "px\" src=\"" + _filePath + "\"></body></html>";
                        webBrowser.DocumentText = iframe;
                    }
                }
                catch (Exception ex)
                {
                    webBrowser.DocumentText = "<html><body><h1>Unable to show this web location - invalid address.</h1></body></html>";

                    Trace.WriteLine(new LogMessage("WebContent", "Unable to show webpage. Exception: " + ex.Message, scheduleId, layoutId), LogType.Error.ToString());
                }

                base.Duration = duration;
                base.RenderMedia();
            }
        }

        public override void RenderMedia()
        {
            //do nothing
            return;
        }

        void webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            // Get ready to show the control
            Show();
            Controls.Add(webBrowser);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                Controls.Remove(webBrowser);
                webBrowser.Dispose();
                GC.Collect();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(String.Format("Unable to dispose {0} because {1}", _filePath, ex.Message));
            }

            base.Dispose(disposing);
        }
    }
}