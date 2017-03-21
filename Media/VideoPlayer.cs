/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2015 Daniel Garner 
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
using System.Runtime.InteropServices;

/// 09/06/12 Dan Changed to raise an event when the video is finished
/// 04/08/12 Dan Changed to raise an error event if one is raised from the control

namespace XiboClient
{
    public partial class VideoPlayer : Form
    {
        private bool _finished;
        private bool _visible = true;

        public delegate void VideoFinished();
        public event VideoFinished VideoEnd;

        public delegate void VideoErrored();
        public event VideoErrored VideoError;

        private bool _looping = false;

        public VideoPlayer()
        {
            InitializeComponent();
            this.TopLevel = false;

            _finished = false;
        }

        public void StartPlayer(string filePath)
        {
            if (_visible)
            {
                axWindowsMediaPlayer1.Visible = true;
                axWindowsMediaPlayer1.Width = this.Width;
                axWindowsMediaPlayer1.Height = this.Height;
            }
            else
            {
                axWindowsMediaPlayer1.Visible = false;
            }
            axWindowsMediaPlayer1.Location = new System.Drawing.Point(0, 0);

            axWindowsMediaPlayer1.uiMode = "none";
            axWindowsMediaPlayer1.URL = filePath;
            axWindowsMediaPlayer1.stretchToFit = true;
            axWindowsMediaPlayer1.windowlessVideo = false;

            axWindowsMediaPlayer1.PlayStateChange += new AxWMPLib._WMPOCXEvents_PlayStateChangeEventHandler(axWMP_PlayStateChange);
            axWindowsMediaPlayer1.ErrorEvent += new EventHandler(axWindowsMediaPlayer1_ErrorEvent);
        }

        /// <summary>
        /// Set Loop
        /// </summary>
        /// <param name="looping"></param>
        public void SetLooping(bool looping)
        {
            axWindowsMediaPlayer1.settings.setMode("loop", looping);
            _looping = looping;
        }

        /// <summary>
        /// Set Mute
        /// </summary>
        /// <param name="mute"></param>
        public void SetMute(bool mute)
        {
            if (mute)
            {
                axWindowsMediaPlayer1.settings.volume = 0;
                axWindowsMediaPlayer1.settings.mute = true;
            }
            else
                axWindowsMediaPlayer1.settings.volume = 100;
        }

        /// <summary>
        /// Set Volume
        /// </summary>
        /// <param name="volume"></param>
        public void SetVolume(int volume)
        {
            if (volume == 0)
            {
                SetMute(true);
            }
            else
            {
                axWindowsMediaPlayer1.settings.volume = volume;
                axWindowsMediaPlayer1.settings.mute = false;
            }
        }

        /// <summary>
        /// Visible
        /// </summary>
        /// <param name="visible"></param>
        public void SetVisible(bool visible)
        {
            _visible = visible;
        }

        /// <summary>
        /// Stop and Clear everything
        /// </summary>
        public void StopAndClear()
        {
            try
            {
                if (axWindowsMediaPlayer1 != null)
                {
                    // Unbind events
                    axWindowsMediaPlayer1.PlayStateChange -= axWMP_PlayStateChange;
                    axWindowsMediaPlayer1.ErrorEvent -= axWindowsMediaPlayer1_ErrorEvent;

                    // Release resources
                    Marshal.FinalReleaseComObject(axWindowsMediaPlayer1.currentMedia);
                    axWindowsMediaPlayer1.close();
                    axWindowsMediaPlayer1.URL = null;
                    axWindowsMediaPlayer1.Dispose();

                    // Remove the WMP control
                    Controls.Remove(axWindowsMediaPlayer1);

                    // Workaround to remove the event handlers from the cachedLayoutEventArgs
                    PerformLayout();

                    // Close this form
                    Close();

                    axWindowsMediaPlayer1 = null;
                }

                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch (AccessViolationException)
            {

            }
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

            Trace.WriteLine(new LogMessage("VideoPlayer - ErrorEvent", axWindowsMediaPlayer1.URL + ". Ex = " + error), LogType.Error.ToString());

            // Raise the event
            if (VideoError == null)
            {
                Trace.WriteLine(new LogMessage("VideoPlayer - ErrorEvent", "Error event handler is null"), LogType.Audit.ToString());
            }
            else
            {
                VideoError();
            }
        }

        void axWMP_PlayStateChange(object sender, AxWMPLib._WMPOCXEvents_PlayStateChangeEvent e)
        {
            if (e.newState == 8 && !_looping)
            {
                // indicate we are stopped
                _finished = true;

                // Raise the event
                if (VideoEnd == null)
                {
                    Trace.WriteLine(new LogMessage("VideoPlayer - Playstate Complete", "Video end handler is null"), LogType.Audit.ToString());
                }
                else
                {
                    VideoEnd();
                }
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