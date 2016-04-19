using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace XiboClient
{
    class Audio : Media
    {
        private string _filePath;
        private VideoPlayer _videoPlayer;
        private int _duration;
        private bool _expired = false;
        private bool _detectEnd = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        public Audio(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            _filePath = Uri.UnescapeDataString(options.uri).Replace('+',' ');
            _duration = options.duration;

            _videoPlayer = new VideoPlayer();
            _videoPlayer.Width = 0;
            _videoPlayer.Height = 0;
            _videoPlayer.Location = new System.Drawing.Point(0, 0);
            _videoPlayer.SetVisible(false);

            // Should we loop?
            _videoPlayer.SetLooping((options.Dictionary.Get("loop", "0") == "1" && _duration != 0));

            // Should we mute?
            _videoPlayer.SetVolume(options.Dictionary.Get("volume", 100));

            // Capture any video errors
            _videoPlayer.VideoError += new VideoPlayer.VideoErrored(_videoPlayer_VideoError);
            _videoPlayer.VideoEnd += new VideoPlayer.VideoFinished(_videoPlayer_VideoEnd);

            Controls.Add(_videoPlayer);
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
                Duration = 1;
                _detectEnd = true;
            }
                
            // Render media as normal (starts the timer, shows the form, etc)
            base.RenderMedia();

            try 
            {
                // Start Player
                _videoPlayer.StartPlayer(_filePath);

                // Show the player
                _videoPlayer.Show();

                Trace.WriteLine(new LogMessage("Audio - RenderMedia", "Video Started"), LogType.Audit.ToString());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("Audio - RenderMedia", ex.Message), LogType.Error.ToString());
                
                // Unable to start video - expire this media immediately
                throw;
            }
        }

        void _videoPlayer_VideoError()
        {
            // Immediately hide the player
            _videoPlayer.Hide();

            _expired = true;
        }

        /// <summary>
        /// Video End event
        /// </summary>
        void _videoPlayer_VideoEnd()
        {
            // Has the video finished playing
            if (_videoPlayer.FinishedPlaying)
            {
                Trace.WriteLine(new LogMessage("Audio - _videoPlayer_VideoEnd", "End of video detected"), LogType.Audit.ToString());

                // Immediately hide the player
                _videoPlayer.Hide();

                // Set to expired
                _expired = true;
            }
        }

        /// <summary>
        /// Override the timer tick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void timer_Tick(object sender, EventArgs e)
        {
            if (!_detectEnd || _expired)
                base.timer_Tick(sender, e);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                // Remove the event handlers
                _videoPlayer.VideoError -= _videoPlayer_VideoError;
                _videoPlayer.VideoEnd -= _videoPlayer_VideoEnd;

                // Stop and Clear
                _videoPlayer.StopAndClear();

                // Remove the control
                Controls.Remove(_videoPlayer);

                // Dispose of the Control
                _videoPlayer.Dispose();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("Audio - Dispose", "Problem disposing of the Video Player. Ex = " + e.Message), LogType.Audit.ToString());
            }

            base.Dispose(disposing);
        }
    }
}
