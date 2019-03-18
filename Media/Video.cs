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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

/// 09/06/12 Dan Changed to raise an event when the video is finished
/// 03/11/12 Dan Fix for non zero duration timers.

namespace XiboClient
{
    class Video : Media
    {
        private string _filePath;
        private VideoPlayer _videoPlayer;
        private int _duration;
        private bool _expired = false;
        private bool _detectEnd = false;
        private RegionOptions _options;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        public Video(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            _options = options;
            _filePath = Uri.UnescapeDataString(options.uri).Replace('+',' ');
            _duration = options.duration;

            _videoPlayer = new VideoPlayer();

            // Should this video be full screen?
            if (options.Dictionary.Get("showFullScreen", "0") == "1")
            {
                Width = options.LayoutSize.Width;
                Height = options.LayoutSize.Height;
                _videoPlayer.Width = options.LayoutSize.Width;
                _videoPlayer.Height = options.LayoutSize.Height;
            }
            else
            {
                _videoPlayer.Width = options.width;
                _videoPlayer.Height = options.height;
            }

            // Assert the location after setting the control size
            _videoPlayer.Location = new System.Drawing.Point(0, 0);

            // Should we loop?
            _videoPlayer.SetLooping((options.Dictionary.Get("loop", "0") == "1" && _duration != 0));

            // Should we mute?
            _videoPlayer.SetMute((options.Dictionary.Get("mute", "0") == "1"));

            // Capture any video errors
            _videoPlayer.VideoError += new VideoPlayer.VideoErrored(_videoPlayer_VideoError);
            _videoPlayer.VideoEnd += new VideoPlayer.VideoFinished(_videoPlayer_VideoEnd);

            Controls.Add(_videoPlayer);
        }

        public override void RenderMedia()
        {
            // Check to see if the video exists or not (if it doesnt say we are already expired)
            if (!File.Exists(_filePath))
            {
                Trace.WriteLine(new LogMessage("Video - RenderMedia", "Local Video file " + _filePath + " not found."));
                throw new FileNotFoundException();
            }

            // Do we need to determine the end time ourselves?
            if (_duration == 0)
            {
                // Set the duration to 1 second
                Duration = 1;
                _detectEnd = true;
            }
                
            // Render media as normal (starts the timer, shows the form, etc)
            base.RenderMedia();

            try 
            {
                // Start Player
                _videoPlayer.StartPlayer(_filePath);

                // Show the player
                _videoPlayer.Show();

                Trace.WriteLine(new LogMessage("Video - RenderMedia", "Video Started"), LogType.Audit.ToString());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("Video - RenderMedia", ex.Message), LogType.Error.ToString());
                
                // Unable to start video - expire this media immediately
                throw;
            }
        }

        void _videoPlayer_VideoError()
        {
            // Immediately hide the player
            _videoPlayer.Hide();

            _expired = true;
        }

        /// <summary>
        /// Video End event
        /// </summary>
        void _videoPlayer_VideoEnd()
        {
            // Has the video finished playing
            if (_videoPlayer.FinishedPlaying)
            {
                Trace.WriteLine(new LogMessage("Video - _videoPlayer_VideoEnd", "End of video detected"), LogType.Audit.ToString());

                // Immediately hide the player
                _videoPlayer.Hide();

                // Set to expired
                _expired = true;
            }
        }

        /// <summary>
        /// Override the timer tick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void timer_Tick(object sender, EventArgs e)
        {
            if (!_detectEnd || _expired)
                base.timer_Tick(sender, e);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                // Remove the event handlers
                _videoPlayer.VideoError -= _videoPlayer_VideoError;
                _videoPlayer.VideoEnd -= _videoPlayer_VideoEnd;

                // Stop and Clear
                _videoPlayer.StopAndClear();

                // Remove the control
                Controls.Remove(_videoPlayer);

                // Dispose of the Control
                _videoPlayer.Dispose();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Video - Dispose", "Problem disposing of the Video Player. Ex = " + e.Message), LogType.Audit.ToString());
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Is a region size change required
        /// </summary>
        /// <returns></returns>
        public override bool RegionSizeChangeRequired()
        {
            return (_options.Dictionary.Get("showFullScreen", "0") == "1");
        }

        /// <summary>
        /// Get Region Size
        /// </summary>
        /// <returns></returns>
        public override System.Drawing.Size GetRegionSize()
        {
            if (RegionSizeChangeRequired())
            {
                return new System.Drawing.Size(_videoPlayer.Width, _videoPlayer.Height);
            }
            else
            {
                return base.GetRegionSize();
            }
        }

        /// <summary>
        /// Get Region Location
        /// </summary>
        /// <returns></returns>
        public override System.Drawing.Point GetRegionLocation()
        {
            if (RegionSizeChangeRequired())
            {
                return new System.Drawing.Point(0, 0);
            }
            else
            {
                return base.GetRegionLocation();
            }
        }
    }
}
