/**
* Copyright (C) 2020 Xibo Signage Ltd
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
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace XiboClient.Rendering
{
    class Image : Media
    {
        private System.Windows.Controls.Image image;
        private string filePath;
        private string scaleType;
        private System.Windows.HorizontalAlignment hAlign;
        private System.Windows.VerticalAlignment vAlign;

        public Image(RegionOptions options) : base(options)
        {
            this.filePath = options.uri;
            this.scaleType = options.Dictionary.Get("scaleType", "stretch");

            // Horizontal Alignment
            switch (options.Dictionary.Get("align", "center"))
            {
                case "left":
                    hAlign = System.Windows.HorizontalAlignment.Left;
                    break;

                case "right":
                    hAlign = System.Windows.HorizontalAlignment.Right;
                    break;

                default:
                    hAlign = System.Windows.HorizontalAlignment.Center;
                    break;
            }

            // Vertical Alignment
            switch (options.Dictionary.Get("valign", "middle"))
            {
                case "top":
                    this.vAlign = System.Windows.VerticalAlignment.Top;
                    break;

                case "bottom":
                    this.vAlign = System.Windows.VerticalAlignment.Bottom;
                    break;

                default:
                    this.vAlign = System.Windows.VerticalAlignment.Center;
                    break;
            }
        }

        public override void RenderMedia(double position)
        {
            // Check that the file path exists
            if (!File.Exists(this.filePath))
            {
                Trace.WriteLine(new LogMessage("Image", "RenderMedia: Cannot Create image object. Invalid Filepath."), LogType.Error.ToString());
                throw new FileNotFoundException();
            }

            // Create a bitmap from our image.
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(this.filePath);
            bitmap.DecodePixelWidth = (int)Width;
            bitmap.EndInit();

            // Set the bitmap as the source of our image
            this.image = new System.Windows.Controls.Image()
            {
                Name = "Img" + this.Id,
                Source = bitmap
            };

            // Handle the different scale types supported
            if (this.scaleType == "stretch")
            {
                this.image.Stretch = System.Windows.Media.Stretch.Fill;
            }
            else
            {
                this.image.Stretch = System.Windows.Media.Stretch.Uniform;

                // Further worry about alignment
                this.image.HorizontalAlignment = this.hAlign;
                this.image.VerticalAlignment = this.vAlign;
            }

            this.MediaScene.Children.Add(this.image);

            // Call base render to set off timers, etc.
            base.RenderMedia(position);
        }
    }
}
