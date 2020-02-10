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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace XiboClient.Rendering
{
    /// <summary>
    /// Interaction logic for Media.xaml
    /// </summary>
    public partial class Media : UserControl
    {

        /// <summary>
        /// Event for Duration Elapsed
        /// </summary>
        /// <param name="filesPlayed"></param>
        public delegate void DurationElapsedDelegate(int filesPlayed);
        public event DurationElapsedDelegate DurationElapsedEvent;
        protected int _filesPlayed = 1;

        /// <summary>
        /// The Id of this Media
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or Sets the duration of this media. Will be 0 if ""
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Has this media exipred?
        /// </summary>
        public bool Expired { get; set; } = false;

        // Private Timer
        protected DispatcherTimer _timer;
        private bool _timerStarted = false;

        /// <summary>
        /// The Intended Width of this Media
        /// </summary>
        public int WidthIntended { get { return options.width; } }

        /// <summary>
        /// The Intended Height of this Media
        /// </summary>
        public int HeightIntended { get { return options.width; } }

        /// <summary>
        /// The Region Options
        /// </summary>
        private RegionOptions options;

        public Media(RegionOptions options)
        {
            InitializeComponent();

            // Store the options.
            this.options = options;
            this.Id = options.mediaid;
        }

        /// <summary>
        /// Start the Timer for this Media
        /// </summary>
        protected void StartTimer()
        {
            //start the timer
            if (!_timerStarted && Duration != 0)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(Duration);
                _timer.Start();

                _timer.Tick += new EventHandler(timer_Tick);

                _timerStarted = true;
            }
        }

        /// <summary>
        /// Reset the timer and start again
        /// </summary>
        protected void RestartTimer()
        {
            if (_timerStarted)
            {
                _timer.Stop();
                _timer.Start();
            }
            else
            {
                StartTimer();
            }
        }

        /// <summary>
        /// Render Media call
        /// </summary>
        public virtual void RenderMedia()
        {
            // Start the timer for this media
            StartTimer();
        }

        /// <summary>
        /// Timer Tick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void timer_Tick(object sender, EventArgs e)
        {
            // Once it has expired we might as well stop the timer?
            _timer.Stop();

            // Signal that this Media Item's duration has elapsed
            SignalElapsedEvent();
        }

        /// <summary>
        /// Signals that an event is elapsed
        /// Will raise a DurationElapsedEvent
        /// </summary>
        public void SignalElapsedEvent()
        {
            Expired = true;

            Trace.WriteLine(new LogMessage("Media - SignalElapsedEvent", "Media Complete"), LogType.Audit.ToString());

            // We're complete
            DurationElapsedEvent?.Invoke(_filesPlayed);
        }

        /// <summary>
        /// Stop this Media
        /// </summary>
        public virtual void Stop()
        {
            // Initiate any tidy up that is needed in here.
            // Dispose of the Timer
            if (_timer != null)
            {
                if (_timer.IsEnabled)
                {
                    _timer.Stop();
                }
                _timer = null;
            }
        }

        /// <summary>
        /// Is a region size change required
        /// </summary>
        /// <returns></returns>
        public bool RegionSizeChangeRequired()
        {
            return false;
        }

        /// <summary>
        /// Get Region Size
        /// </summary>
        /// <returns></returns>
        public Size GetRegionSize()
        {
            return new Size(Width, Height);
        }

        /// <summary>
        /// Get Region Location
        /// </summary>
        /// <returns></returns>
        public Point GetRegionLocation()
        {
            return new Point(this.options.top, this.options.left);
        }

        public void TransitionIn()
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(1500)
            };
            BeginAnimation(OpacityProperty, animation);

            /*string transIn = this.options.Dictionary.Get("transIn");
            if (!string.IsNullOrEmpty(transIn))
            {
                switch (transIn)
                {
                    case "fadeIn":

                }
                Transitions.MoveAnimation(medaiElemnt, OpacityProperty, transIn, transInDirection, transInDuration, "in", _top, _left);
            } else
            {
                return null;
            }*/
        }

        public void TransitionOut()
        {
            // Transition out?
            DoubleAnimation animation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(1500)
            };
            //animation.Completed += Animation_Completed;
            BeginAnimation(OpacityProperty, animation);
        }
    }
}
