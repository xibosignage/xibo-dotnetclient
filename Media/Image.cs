/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2014 Daniel Garner and James Packer
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
        RegionOptions _options;
        
        public ImagePosition(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            _options = options;
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
                _pictureBox.Size = new Size(_width, _height);
                _pictureBox.Location = new Point(0, 0);
                _pictureBox.BorderStyle = BorderStyle.None;
                _pictureBox.BackColor = Color.Transparent;

                // Do we need to align the image in any way?
                if (options.Dictionary.Get("scaleType", "stretch") == "center" && (options.Dictionary.Get("align", "center") != "center" || options.Dictionary.Get("valign", "middle") != "middle"))
                {
                    // Yes we do, so we must override the paint method
                    _pictureBox.Paint += _pictureBox_Paint;
                }
                else
                {
                    // No we don't so use a normal picture box.
                    _pictureBox.SizeMode = (options.Dictionary.Get("scaleType", "center") == "stretch") ? PictureBoxSizeMode.StretchImage : PictureBoxSizeMode.Zoom;
                    _pictureBox.Image = new Bitmap(_filePath);
                }

                Controls.Add(this._pictureBox);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(new LogMessage("ImagePosition", String.Format("Cannot create Image Object with exception: {0}", ex.Message)), LogType.Error.ToString());
            }
        }

        void _pictureBox_Paint(object sender, PaintEventArgs e)
        {
            string align = _options.Dictionary.Get("align", "center");
            string valign = _options.Dictionary.Get("valign", "middle");

            Image image = Image.FromFile(_filePath);

            // Get our image
            Graphics graphics = e.Graphics;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // Calculate the width and height required
            double imageProportion = (double)image.Width / (double)image.Height;
            double regionProportion = _pictureBox.Width / _pictureBox.Height;

            int x = 0;
            int y = 0;
            int width = _pictureBox.Width;
            int height = _pictureBox.Height;

            if (imageProportion > regionProportion)
            {
                // Use the full width possible and adjust the height accordingly
                height = (int)(_pictureBox.Width / imageProportion);

                if (valign == "middle")
                {
                    // top margin needs to drop down half
                    x = x + ((_pictureBox.Height - height) / 2);
                }
                else if (valign == "bottom") {
                    x = x + (_pictureBox.Height - height);
                }
            }
            else
            {
                // Use the full height possible and adjust the width accordingly
                width = (int)(imageProportion * _pictureBox.Height);

                if (align == "center")
                {
                    y = y + ((_pictureBox.Width - width) / 2);
                }
                else if (align == "right")
                {
                    y = y + (_pictureBox.Width - width);
                }
            }

            graphics.DrawImage(image,
                y,
                x,
                width,
                height);
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

                    if (_pictureBox.Image != null)
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
