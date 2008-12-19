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
using System.Text;
using System.Windows.Forms;

namespace XiboClient
{
    class Flash : Media
    {
        public Flash (RegionOptions options)
            : base(options.width, options.height, options.top, options.left) 
        {
            this.filePath = options.uri;
            duration = options.duration;

            // Create the flash player form
            flashPlayer = new FlashNew();
            flashPlayer.Width = options.width;
            flashPlayer.Height = options.height;
            flashPlayer.Location = new System.Drawing.Point(0, 0);

            this.Controls.Add(flashPlayer);

            return;
        }

        public override void RenderMedia()
        {
            // Have we been provided with a duration? Or is the duration 0 (auto determine)
            if (duration == 0)
            {
                // We do our own timing
                determineTime = true;
                base.Duration = 5; //Check every 5 seconds
            }

            base.RenderMedia();

            // Show this flash form
            flashPlayer.Show();

            try
            {
                flashPlayer.StartPlayer(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return;
        }

        protected override void timer_Tick(object sender, EventArgs e)
        {
            // If we are dealing with the time && we havent reached the end yet, keep going.
            if (determineTime && flashPlayer.IsPlaying())
            {
                return;
            }
            else
            {
                base.timer_Tick(sender, e);
            }
            return;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //Make sure we correctly clear these values
                try
                {
                    flashPlayer.Close();
                    this.Controls.Remove(flashPlayer);
                    flashPlayer.Dispose();
                }
                catch { }
            }

            base.Dispose(disposing);
        }

        private string filePath;
        private int duration;
        private bool determineTime = false;

        FlashNew flashPlayer;
    }
}
