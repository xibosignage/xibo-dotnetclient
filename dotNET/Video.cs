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
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;

namespace XiboClient
{
    class Video : Media
    {
        public Video(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            this.filePath = options.uri;
            this.duration = options.duration;

            videoPlayer = new VideoPlayer();
            videoPlayer.Width = options.width;
            videoPlayer.Height = options.height;
            videoPlayer.Location = new System.Drawing.Point(0, 0);

            videoPlayer.VideoPlayerElapsedEvent += new VideoPlayer.VideoPlayerElapsed(videoPlayer_VideoPlayerElapsedEvent);

            Controls.Add(videoPlayer);
        }

        void videoPlayer_VideoPlayerElapsedEvent()
        {
            Hide();
            videoPlayer.Hide();

            // Time is up
            SignalElapsedEvent();
        }

        public override void RenderMedia()
        {
            try 
            {
                videoPlayer.StartPlayer(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                SignalElapsedEvent();
                return;
            }

            // Do we start a timer?
            if (duration != 0)
            {
                base.RenderMedia();
            }

            // Add and show the control
            Show();
            videoPlayer.Show();
            Application.DoEvents();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose of managed resources
            }
            
            // Unmanaged resources
            Controls.Remove(videoPlayer);

            try
            {
                videoPlayer.Hide();
                videoPlayer.Dispose();
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("Unable to dispose of video player", "Dispose");
            }

            base.Dispose(disposing);
        }

        string filePath;
        VideoPlayer videoPlayer;
        private int duration;
    }
}
