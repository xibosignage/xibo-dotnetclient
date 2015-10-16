/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2014 Spring Signage Ltd
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
using System.IO;

namespace XiboClient
{
    class PowerPoint : Media
    {
        int scheduleId;
        int layoutId;
        string mediaId;
        string type;

        string _filePath;
        WebBrowser webBrowser;
        int duration;

        public PowerPoint(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            duration = options.duration;
            scheduleId = options.scheduleId;
            layoutId = options.layoutId;
            mediaId = options.mediaid;
            type = options.type;
            _filePath = Uri.UnescapeDataString(options.uri).Replace('+', ' ');

            webBrowser = new WebBrowser();
            webBrowser.Size = this.Size;
            webBrowser.ScrollBarsEnabled = false;
            webBrowser.ScriptErrorsSuppressed = true;
            webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(webBrowser_DocumentCompleted);
            webBrowser.Visible = false;

            if (!ApplicationSettings.Default.PowerpointEnabled)
            {
                webBrowser.DocumentText = "<html><body><h1>Powerpoint not enabled on this display</h1></body></html>";

                Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", "Powerpoint is not enabled on this display", scheduleId, layoutId, mediaId));
            }
            else
            {
                try
                {
                    webBrowser.Navigate(_filePath);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("WebContent", "Unable to show webpage. Exception: " + ex.Message, scheduleId, layoutId), LogType.Error.ToString());
                    throw new InvalidOperationException("Cannot navigate to PowerPoint file");
                }
            }

            Controls.Add(webBrowser);
            Show();
        }

        void webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            // Get ready to show the control
            webBrowser.Visible = true;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                Controls.Remove(webBrowser);
                webBrowser.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(String.Format("Unable to dispose {0} because {1}", _filePath, ex.Message));
            }

            base.Dispose(disposing);
        }
    }
}