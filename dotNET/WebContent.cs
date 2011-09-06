/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006,2007,2008 Daniel Garner and James Packer
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

namespace XiboClient
{
    class WebContent : Media
    {
        int scheduleId;
        int layoutId;
        string mediaId;
        string type;

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

            // Attach event
            webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(webBrowser_DocumentCompleted);

            if (!Properties.Settings.Default.powerpointEnabled && options.type == "powerpoint")
            {
                webBrowser.DocumentText = "<html><body><h1>Powerpoint not enabled on this display</h1></body></html>";
                System.Diagnostics.Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", "Powerpoint is not enabled on this display", scheduleId, layoutId, mediaId));
            }
            else
            {
                try
                {
                    // Try to make a URI out of the file path
                    try
                    {
                        this.filePath = Uri.UnescapeDataString(options.uri);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message, "WebContent");
                    }

                    // Navigate
                    webBrowser.Navigate(this.filePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", ex.Message, scheduleId, layoutId, mediaId));

                    webBrowser.DocumentText = "<html><body><h1>Unable to show this web location - invalid address.</h1></body></html>";

                    System.Diagnostics.Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", "Unable to show the powerpoint, cannot be located", scheduleId, layoutId, mediaId));
                }
            }
        }

        public override void RenderMedia()
        {
            //do nothing
            return;
        }

        void webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            base.Duration = duration;
            base.RenderMedia();

            // Get ready to show the control
            Show();
            Application.DoEvents();
            Controls.Add(webBrowser);
        }

        protected override void Dispose(bool disposing)
        {
            System.Diagnostics.Debug.WriteLine(String.Format("Disposing {0}", filePath));
            
            try
            {
                Controls.Remove(webBrowser);
                webBrowser.Dispose();
                GC.Collect();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(String.Format("Unable to dispose {0} because {1}", filePath, ex.Message));
            }

            base.Dispose(disposing);

            System.Diagnostics.Debug.WriteLine(String.Format("Disposed {0}", filePath));
        }

        string filePath;
        WebBrowser webBrowser;
        int duration;
    }
}