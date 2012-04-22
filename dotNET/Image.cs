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
        private string _filePath;
        PictureBox _pictureBox;
        
        public ImagePosition(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            _filePath = options.uri;
            
            if (!System.IO.File.Exists(_filePath))
            {
                // Exit
                System.Diagnostics.Trace.WriteLine(new LogMessage("Image - Dispose", "Cannot Create image object. Invalid Filepath."), LogType.Error.ToString());
                return;
            }

            try
            {
                _pictureBox = new PictureBox();
                _pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                _pictureBox.Image = new Bitmap(_filePath);
                _pictureBox.Size = new Size(_width, _height);
                _pictureBox.Location = new Point(0, 0);
                _pictureBox.BorderStyle = BorderStyle.None;
                _pictureBox.BackColor = Color.Transparent;
                
                this.Controls.Add(this._pictureBox);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(new LogMessage("ImagePosition", String.Format("Cannot create Image Object with exception: {0}", ex.Message)), LogType.Error.ToString());
            }
        }

        public override void RenderMedia()
        {
            base.RenderMedia();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    Controls.Remove(_pictureBox);

                    _pictureBox.Image.Dispose();
                    _pictureBox.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(new LogMessage("Image - Dispose", String.Format("Cannot dispose Image Object with exception: {0}", ex.Message)), LogType.Error.ToString());
                }
            }

            base.Dispose(disposing);
        }
    }
}
