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
    public partial class FlashNew : Form
    {
        public FlashNew()
        {
            InitializeComponent();
            this.TopLevel = false;
        }

        public void StartPlayer(string filePath)
        {
            axShockwaveFlash1.Width = this.Width;
            axShockwaveFlash1.Height = this.Height;
            axShockwaveFlash1.Location = new System.Drawing.Point(0, 0);
            axShockwaveFlash1.WMode = "transparent";

            axShockwaveFlash1.Size = this.Size;
            axShockwaveFlash1.Visible = true;

            axShockwaveFlash1.LoadMovie(0, filePath);
            axShockwaveFlash1.Play();
        }

        public void StopPlayer()
        {
            axShockwaveFlash1.Dispose();
        }

        public bool IsPlaying()
        {
            return axShockwaveFlash1.IsPlaying();
        }
    }
}
