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
using System.IO;

/// 05/09/12 Dan Created as a MOCK UP for the DirectShow Player

namespace XiboClient
{
    class VideoDS : Media
    {
        private VideoPlayer _videoPlayer;
        //private DSVideoPlayer _videoPlayer;
        private bool _expired = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        public VideoDS(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            _filesPlayed = 0;
            _videoPlayer = new VideoPlayer();
            _videoPlayer.Width = options.width;
            _videoPlayer.Height = options.height;
            _videoPlayer.Location = new System.Drawing.Point(0, 0);
            //_videoPlayer.SetPlaylist(options.mediaNodes, options.CurrentIndex);
            
            Controls.Add(_videoPlayer);
        }

        public override void RenderMedia()
        {
            // Configure an event for the end of a file
            // _videoPlayer.VideoEnd += new VideoPlayer.VideoEnd(_videoPlayer_FileEnd);
            
            // Use an event for the end of the playlist
            //_videoPlayer.PlaylistEnd += new VideoPlayer.PlaylistEnd(_videoPlayer_VideoEnd);

            // Render media as normal (starts the timer, shows the form, etc)
            base.RenderMedia();

            try 
            {
                // Start Player
                //_videoPlayer.StartPlayer();

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

        //private void _videoPlayer_FileEnd()
        //{
        //  _filesPlayed++;
        //}

        /// <summary>
        /// Video End event
        /// </summary>
        private void _videoPlayer_VideoEnd()
        {
            // Has the video finished playing
            if (_videoPlayer.FinishedPlaying)
            {
                Trace.WriteLine(new LogMessage("Video - _videoPlayer_VideoEnd", "End of video detected"), LogType.Audit.ToString());

                // Immediately hide the player
                _videoPlayer.Hide();

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
            if (_expired)
                base.timer_Tick(sender, e);
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
