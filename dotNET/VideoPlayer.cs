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
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

/// 09/06/12 Dan Changed to raise an event when the video is finished
/// 04/08/12 Dan Changed to raise an error event if one is raised from the control

namespace XiboClient
{
    public partial class VideoPlayer : Form
    {
        private bool _finished;

        public delegate void VideoFinished();
        public event VideoFinished VideoEnd;

        public delegate void VideoErrored();
        public event VideoErrored VideoError;

        public VideoPlayer()
        {
            InitializeComponent();
            this.TopLevel = false;

            _finished = false;
        }

        public void StartPlayer(string filePath)
        {
            axWindowsMediaPlayer1.Visible = true;
            axWindowsMediaPlayer1.Width = this.Width;
            axWindowsMediaPlayer1.Height = this.Height;
            axWindowsMediaPlayer1.Location = new System.Drawing.Point(0, 0);

            axWindowsMediaPlayer1.uiMode = "none";
            axWindowsMediaPlayer1.URL = filePath;
            axWindowsMediaPlayer1.stretchToFit = true;
            axWindowsMediaPlayer1.windowlessVideo = true;

            axWindowsMediaPlayer1.PlayStateChange += new AxWMPLib._WMPOCXEvents_PlayStateChangeEventHandler(axWMP_PlayStateChange);
            axWindowsMediaPlayer1.ErrorEvent += new EventHandler(axWindowsMediaPlayer1_ErrorEvent);
        }

        void axWindowsMediaPlayer1_ErrorEvent(object sender, EventArgs e)
        {
            // Get the error for logging
            string error;
            try 
            {
                error = axWindowsMediaPlayer1.Error.get_Item(0).errorDescription;
            }
            catch 
            {
                error = "Unknown Error";
            }

            Trace.WriteLine(new LogMessage("VideoPlayer - ErrorEvent", error), LogType.Error.ToString());

            // Raise the event
            VideoError();
        }

        void axWMP_PlayStateChange(object sender, AxWMPLib._WMPOCXEvents_PlayStateChangeEvent e)
        {
            if (e.newState == 8)
            {
                // indicate we are stopped
                _finished = true;

                VideoEnd();
            }
        }

        /// <summary>
        /// Has this player finished playing
        /// </summary>
        public bool FinishedPlaying
        {
            get
            {
                return _finished;
            }
        }
    }
}