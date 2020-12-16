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
using System.Windows.Media;
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
        /// Media has stopped
        /// </summary>
        public delegate void MediaStoppedDelegate(Media media);
        public event MediaStoppedDelegate MediaStoppedEvent;

        /// <summary>
        /// Have we stopped?
        /// </summary>
        private bool _stopped = false;

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

        /// <summary>
        /// The time we started
        /// </summary>
        protected DateTime _startTick;

        /// <summary>
        /// The ScheduleId
        /// </summary>
        public int ScheduleId { get; private set; }

        /// <summary>
        /// The LayoutId
        /// </summary>
        public int LayoutId { get; private set; }

        /// <summary>
        /// Are stats enabled.
        /// </summary>
        public bool StatsEnabled { get; private set; }

        /// <summary>
        /// Media Object
        /// </summary>
        /// <param name="options"></param>
        public Media(RegionOptions options)
        {
            InitializeComponent();

            // Store the options.
            this.options = options;
            this.Id = options.mediaid;
            ScheduleId = options.scheduleId;
            LayoutId = options.layoutId;
            StatsEnabled = options.isStatEnabled;
        }

        /// <summary>
        /// Start the Timer for this Media
        /// </summary>
        protected void StartTimer(double position)
        {
            //start the timer
            if (!_timerStarted && Duration > 0)
            {
                double remainingSeconds = (Duration - position);

                Debug.WriteLine("StartTimer: duration = " + Duration + ", position = " + position + ", Delta = " + remainingSeconds, "Media");

                // a timer must run for some time at least
                // the fact we're here at all means that some other things on this Layout have time to run
                // so expire after the minimum sensible time.
                if (remainingSeconds <= 0)
                {
                    remainingSeconds = 1;
                }

                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(remainingSeconds);
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
                StartTimer(0);
            }
        }

        /// <summary>
        /// Render Media call
        /// </summary>
        public virtual void RenderMedia(double position)
        {
            // Record the start time.
            if (position <= 0 || this._startTick == null)
            {
                this._startTick = DateTime.Now;
            }

            // We haven't stopped
            this._stopped = false;

            // Start the timer for this media
            StartTimer(position);

            // Transition In
            TransitionIn();
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
        /// <param name="regionStopped"/>
        /// </summary>
        public void Stop(bool regionStopped)
        {
            if (regionStopped)
            {
                this._stopped = true;
                this.MediaStoppedEvent?.Invoke(this);
            }
            else
            {
                TransitionOut();
            }

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
        /// Final clean up
        /// </summary>
        public virtual void Stopped()
        {

        }

        /// <summary>
        /// Get the Current Tick
        /// </summary>
        /// <returns></returns>
        public double CurrentPlaytime()
        {
            return (DateTime.Now - this._startTick).TotalSeconds;
        }

        /// <summary>
        /// Is a region size change required
        /// </summary>
        /// <returns></returns>
        public virtual bool RegionSizeChangeRequired()
        {
            return false;
        }

        /// <summary>
        /// TransitionIn if necessary
        /// </summary>
        public void TransitionIn()
        {
            // Does this Media item have an inbound transition?
            string transIn = options.Dictionary.Get("transIn");
            if (!string.IsNullOrEmpty(transIn))
            {
                // Yes we do have one.
                int duration = options.Dictionary.Get("transInDuration", 1000);

                switch (transIn)
                {
                    case "fly":
                        FlyAnimation(options.Dictionary.Get("transInDirection", "W"), duration, true);
                        break;
                    case "fadeIn":
                        DoubleAnimation animation = new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(duration)
                        };
                        BeginAnimation(OpacityProperty, animation);
                        break;
                }
            }
        }

        /// <summary>
        /// Transition Out
        /// </summary>
        public void TransitionOut()
        {
            // Does this Media item have an outbound transition?
            string transOut = options.Dictionary.Get("transOut");
            if (!string.IsNullOrEmpty(transOut))
            {
                // Yes we do have one.
                int duration = options.Dictionary.Get("transOutDuration", 1000);

                switch (transOut)
                {
                    case "fly":
                        FlyAnimation(options.Dictionary.Get("transOutDirection", "E"), duration, false);
                        break;
                    case "fadeOut":
                        DoubleAnimation animation = new DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = TimeSpan.FromMilliseconds(duration)
                        };
                        animation.Completed += Animation_Completed;
                        BeginAnimation(OpacityProperty, animation);
                        break;
                }
            }
            else if (!this._stopped)
            {
                this._stopped = true;
                this.MediaStoppedEvent?.Invoke(this);
            }
        }

        /// <summary>
        /// Animation completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Animation_Completed(object sender, EventArgs e)
        {
            // Indicate we have stopped (only once)
            if (!this._stopped)
            {
                this._stopped = true;
                this.MediaStoppedEvent?.Invoke(this);
            }
        }

        /// <summary>
        /// Fly Animation
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="duration"></param>
        /// <param name="isInbound"></param>
        private void FlyAnimation(string direction, double duration, bool isInbound)
        {
            // We might not need both of these, but we add them just in case we have a mid-way compass point
            var trans = new TranslateTransform();

            DoubleAnimation doubleAnimationX = new DoubleAnimation();
            doubleAnimationX.Duration = TimeSpan.FromMilliseconds(duration);
            doubleAnimationX.Completed += Animation_Completed;

            DoubleAnimation doubleAnimationY = new DoubleAnimation();
            doubleAnimationY.Duration = TimeSpan.FromMilliseconds(duration);
            doubleAnimationY.Completed += Animation_Completed;

            // Get the viewable window width and height
            int screenWidth = options.PlayerWidth;
            int screenHeight = options.PlayerHeight;

            int top = options.top;
            int left = options.left;

            // Where should we end up once we are done?
            if (isInbound)
            {
                // End up at the top/left
                doubleAnimationX.To = left;
                doubleAnimationY.To = top;
            }
            else
            {
                // End up off the screen
                doubleAnimationX.To = screenWidth;
                doubleAnimationY.To = screenHeight;
            }

            // Compass points
            switch (direction)
            {
                case "N":
                    if (isInbound)
                    {
                        // We come in from the bottom of the screen
                        doubleAnimationY.From = (screenHeight - top);
                    }
                    else
                    {
                        // We go out across the top
                        doubleAnimationY.From = top;
                    }

                    BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    break;

                case "NE":
                    if (isInbound)
                    {
                        doubleAnimationX.From = (screenWidth - left);
                        doubleAnimationY.From = (screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = top;
                    }


                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    RenderTransform = trans;
                    break;

                case "E":
                    if (isInbound)
                    {
                        doubleAnimationX.From = -(screenWidth - left);
                    }
                    else
                    {
                        if (left == 0)
                        {
                            doubleAnimationX.From = -left;
                        }
                        else
                        {
                            doubleAnimationX.From = -(screenWidth - left);
                        }

                    }

                    BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    break;

                case "SE":
                    if (isInbound)
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = -(screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = (screenWidth - left);
                        doubleAnimationY.From = -(screenHeight - top);
                    }

                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    RenderTransform = trans;
                    break;

                case "S":
                    if (isInbound)
                    {
                        doubleAnimationX.From = -(screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = -top;
                    }

                    BeginAnimation(TranslateTransform.YProperty, doubleAnimationX);
                    break;

                case "SW":
                    if (isInbound)
                    {
                        doubleAnimationX.From = (screenWidth - left);
                        doubleAnimationY.From = -top;
                    }
                    else
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = -(screenHeight - left);
                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    RenderTransform = trans;
                    break;

                case "W":
                    if (isInbound)
                    {
                        doubleAnimationX.From = (screenWidth - left);
                    }
                    else
                    {
                        doubleAnimationX.From = -left;
                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    RenderTransform = trans;
                    break;

                case "NW":
                    if (isInbound)
                    {
                        doubleAnimationX.From = (screenWidth - left);
                        doubleAnimationY.From = (screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = top;
                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    RenderTransform = trans;
                    break;
            }
        }
    }
}
