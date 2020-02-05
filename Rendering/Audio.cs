using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace XiboClient.Rendering
{
    class Audio : Media
    {
        private string _filePath;
        private int _duration;
        private bool _detectEnd = false;
        private bool isLooping = false;

        private MediaElement mediaElement;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        public Audio(RegionOptions options)
            : base(options)
        {
            _filePath = Uri.UnescapeDataString(options.uri).Replace('+', ' ');
            _duration = options.duration;

            this.mediaElement = new MediaElement();
            this.mediaElement.Width = 0;
            this.mediaElement.Height = 0;
            this.mediaElement.Visibility = Visibility.Hidden;
            this.mediaElement.Volume = options.Dictionary.Get("volume", 100);

            // Events
            this.mediaElement.MediaEnded += MediaElement_MediaEnded;
            this.mediaElement.MediaFailed += MediaElement_MediaFailed;

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
