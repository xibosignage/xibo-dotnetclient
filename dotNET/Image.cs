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
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace XiboClient
{
    class ImagePosition : Media
    {
        public ImagePosition(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            this.filePath = options.uri;
            
            if (!System.IO.File.Exists(this.filePath))
            {
                // Exit
                this.loaded = false;
                return;
            }

            Bitmap img = new Bitmap(this.filePath);
 
            this.pictureBox = new PictureBox();
            this.pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            this.pictureBox.Image = img;
            this.pictureBox.Size = new Size(width, height);
            this.pictureBox.Location = new Point(0, 0);
            this.pictureBox.BorderStyle = BorderStyle.None;
            this.pictureBox.BackColor = Color.Transparent;
            this.loaded = true;

            this.Controls.Add(this.pictureBox);
        }

        public override void RenderMedia()
        {
            base.RenderMedia();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && loaded)
            {
                this.pictureBox.Dispose();
            }

            base.Dispose(disposing);
        }

        private string filePath;
        private bool loaded;
        PictureBox pictureBox;
    }
}
