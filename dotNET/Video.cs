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
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

/// 09/06/12 Dan Changed to raise an event when the video is finished

namespace XiboClient
{
    class Video : Media
    {
        string _filePath;
        VideoPlayer _videoPlayer;
        private int _duration;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        public Video(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            _filePath = Uri.UnescapeDataString(options.uri);
            _duration = options.duration;

            _videoPlayer = new VideoPlayer();
            _videoPlayer.Width = options.width;
            _videoPlayer.Height = options.height;
            _videoPlayer.Location = new System.Drawing.Point(0, 0);

            this.Controls.Add(_videoPlayer);
        }

        public override void RenderMedia()
        {
            // Do we need to determine the end time ourselves?
            if (_duration == 0)
            {
                // Use an event for this.
                _videoPlayer.VideoEnd += new VideoPlayer.VideoFinished(_videoPlayer_VideoEnd);

                // Show the form
                Show();
            }
            else
                // Render media as normal (starts the timer, shows the form, etc)
                base.RenderMedia();

            try 
            {
                _videoPlayer.StartPlayer(_filePath);

                Trace.WriteLine(new LogMessage("Video - RenderMedia", "Video Started"), LogType.Audit.ToString());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("Video - RenderMedia", ex.Message), LogType.Error.ToString());
                
                // Unable to start video - expire this media immediately
                SignalElapsedEvent();
            }

            // Show the player
            _videoPlayer.Show();
        }

        /// <summary>
        /// Video End event
        /// </summary>
        void _videoPlayer_VideoEnd()
        {
            // Has the video finished playing
            if (_videoPlayer.FinishedPlaying)
            {
                Trace.WriteLine(new LogMessage("Video - timer_Tick", "End of video detected"), LogType.Audit.ToString());

                // Immediately hide the player
                _videoPlayer.Hide();

                // We are expired
                SignalElapsedEvent();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
               
            }

            try
            {
                _videoPlayer.Hide();
                Controls.Remove(_videoPlayer);
                _videoPlayer.Dispose();
            }
            catch
            {

            }

            base.Dispose(disposing);
        }
    }
}
