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
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace XiboClient
{
    public partial class VideoPlayer : Form
    {
        public VideoPlayer()
        {
            InitializeComponent();
            this.TopLevel = false;

            finished = false;
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
        }

        void axWMP_PlayStateChange(object sender, AxWMPLib._WMPOCXEvents_PlayStateChangeEvent e)
        {
            if (e.newState == 8)
            {
                // indicate we are stopped
                finished = true;
            }
        }

        /// <summary>
        /// Has this player finished playing
        /// </summary>
        public bool FinishedPlaying
        {
            get
            {
                return this.finished;
            }
        }

        private bool finished;
    }
}