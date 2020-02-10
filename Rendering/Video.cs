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
using System.Windows;
using System.Windows.Controls;

namespace XiboClient.Rendering
{
    class Video : Media
    {
        private string _filePath;
        private int _duration;
        private int volume;
        private bool _detectEnd = false;
        private bool isLooping = false;
        protected bool ShouldBeVisible { get; set; }

        private MediaElement mediaElement;

        public Video(RegionOptions options) : base(options)
        {
            this.ShouldBeVisible = true;

            _filePath = Uri.UnescapeDataString(options.uri).Replace('+', ' ');
            _duration = options.duration;

            // Handle Volume
            this.volume = options.Dictionary.Get("volume", 100);

            // Should we loop?
            this.isLooping = (options.Dictionary.Get("loop", "0") == "1" && _duration != 0);
        }

        private void MediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // Log and expire
            Trace.WriteLine(new LogMessage("Audio", "MediaElement_MediaFailed: Media Failed. E = " + e.ErrorException.Message), LogType.Error.ToString());

            Expired = true;
        }

        private void MediaElement_MediaEnded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Should we loop?
            if (isLooping)
            {
                this.mediaElement.Position = TimeSpan.Zero;
                this.mediaElement.Play();
            }
            else
            {
                Expired = true;
            }
        }

        public override void RenderMedia()
        {
            // Check to see if the video exists or not (if it doesnt say we are already expired)
            if (!File.Exists(_filePath))
            {
                Trace.WriteLine(new LogMessage("Audio - RenderMedia", "Local Video file " + _filePath + " not found."));
                throw new FileNotFoundException();
            }

            // Create a Media Element
            this.mediaElement = new MediaElement();
            this.mediaElement.Volume = this.volume;

            if (!this.ShouldBeVisible)
            {
                this.mediaElement.Width = 0;
                this.mediaElement.Height = 0;
                this.mediaElement.Visibility = Visibility.Hidden;
            }

            // Events
            this.mediaElement.MediaEnded += MediaElement_MediaEnded;
            this.mediaElement.MediaFailed += MediaElement_MediaFailed;

            // Do we need to determine the end time ourselves?
            if (_duration == 0)
            {
                // Set the duration to 1 second
                // this essentially means RenderMedia will set up a timer which ticks every second
                // when we're actually expired and we detect the end, we set expired
                Duration = 1;
                _detectEnd = true;
            }

            // Render media as normal (starts the timer, shows the form, etc)
            base.RenderMedia();

            try
            {
                // Start Player
                this.mediaElement.Source = new Uri(_filePath);

                this.MediaScene.Children.Add(this.mediaElement);

                Trace.WriteLine(new LogMessage("Audio - RenderMedia", "Video Started"), LogType.Audit.ToString());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("Audio - RenderMedia", ex.Message), LogType.Error.ToString());

                // Unable to start video - expire this media immediately
                throw;
            }
        }

        public override void Stop()
        {
            // Remove the event handlers
            this.mediaElement.MediaEnded -= MediaElement_MediaEnded;
            this.mediaElement.MediaFailed -= MediaElement_MediaFailed;

            base.Stop();
        }

        /// <summary>
        /// Override the timer tick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void timer_Tick(object sender, EventArgs e)
        {
            if (!_detectEnd || Expired)
            {
                // We're not end detect, so we pass the timer through
                base.timer_Tick(sender, e);
            }
        }
    }
}
