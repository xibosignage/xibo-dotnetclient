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

            this.Controls.Add(videoPlayer);
        }

        public override void RenderMedia()
        {
            if (duration == 0)
            {
                // Determine the end time ourselves
                base.Duration = 1; //check every second
            }

            base.RenderMedia();

            videoPlayer.Show();

            try 
            {
                videoPlayer.StartPlayer(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                
                // Unable to start video - expire this media immediately
                base.timer_Tick(null, null);
            }
        }

        protected override void timer_Tick(object sender, EventArgs e)
        {
            if (duration == 0)
            {
                   // Has the video finished playing
                if (videoPlayer.FinishedPlaying)
                {
                    // Raise the expired tick which will clear this media
                    base.timer_Tick(sender, e);
                }
            }
            else
            {
                // Our user defined timer duration has expired - so raise the base timer tick which will clear this media
                base.timer_Tick(sender, e);
            }

            return;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
               
            }

            try
            {
                videoPlayer.Hide();
                Controls.Remove(videoPlayer);
                videoPlayer.Dispose();
            }
            catch
            {

            }

            base.Dispose(disposing);
        }

        string filePath;
        VideoPlayer videoPlayer;
        private int duration;
    }
}
