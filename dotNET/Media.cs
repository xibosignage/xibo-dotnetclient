/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2012 Daniel Garner
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
using XiboClient.Properties;
using System.Management;
using System.Diagnostics;

namespace XiboClient
{
    class Media : Form
    {
        // Events
        public delegate void DurationElapsedDelegate(int filesPlayed);
        public event DurationElapsedDelegate DurationElapsedEvent;
        protected int _filesPlayed = 1;

        /// <summary>
        /// Gets or Sets the duration of this media. Will be 0 if ""
        /// </summary>
        public int Duration
        {
            get
            {
                return _duration;
            }
            set
            {
                _duration = value;
            }
        }
        private int _duration;

        // Variables for size and position
        public int _width;
        public int _height;
        public int _top;
        public int _left;

        /// <summary>
        /// Has this media exipred?
        /// </summary>
        public bool Expired
        {
            get
            {
                return _hasExpired;
            }
            set
            {
                _hasExpired = value;
            }
        }
        private bool _hasExpired = false;

        // Private Timer
        protected Timer _timer;
        private bool _timerStarted = false;

        /// <summary>
        /// Refresh Rate
        /// </summary>
        /// <returns></returns>
        public static int PrimaryScreenRefreshRate
        {
            get
            {
                if (_refreshRate == 0)
                {

                    try
                    {
                        ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from win32_videocontroller");

                        foreach (ManagementObject videocontroller in searcher.Get())
                        {
                            uint rate = (uint)videocontroller["CurrentRefreshRate"];

                            if (rate != 0)
                            {
                                Debug.WriteLine("Screen RefreshRate: " + rate);

                                _refreshRate = (int)rate;

                                if (_refreshRate < 0 || _refreshRate > 500)
                                {
                                    Debug.WriteLine("Invalid!");
                                    _refreshRate = 60;
                                }

                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _refreshRate = 60;
                        Debug.WriteLine("Unable to get screen refresh rate: " + ex);
                    }
                }
                return _refreshRate;
            }
        }
        private static int _refreshRate;

        /// <summary>
        /// Media Object
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="top"></param>
        /// <param name="left"></param>
        public Media(int width, int height, int top, int left)
        {
            Hide();

            _width = width;
            _height = height;
            _top = top;
            _left = left;

            // Form properties
            TopLevel = false;
            FormBorderStyle = FormBorderStyle.None;

            Width = width;
            Height = height;
            Location = new System.Drawing.Point(0, 0);        

            // Transparency
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = System.Drawing.Color.Transparent;
            TransparencyKey = System.Drawing.Color.White;

            // Only enable double buffering if set in options
            if (Settings.Default.DoubleBuffering)
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
                SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            }
        }

        /// <summary>
        /// Start the Timer for this Media
        /// </summary>
        protected void StartTimer()
        {
            //start the timer
            if (!_timerStarted && _duration != 0)
            {
                _timer = new Timer();
                _timer.Interval = (1000 * _duration);
                _timer.Start();

                _timer.Tick += new EventHandler(timer_Tick);

                _timerStarted = true;
            }
        }

        /// <summary>
        /// Render Media call
        /// </summary>
        public virtual void RenderMedia() 
        {
            // Start the timer for this media
            StartTimer();

            // Show the form
            Show();
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
            _hasExpired = true;

            Trace.WriteLine(new LogMessage("Media - SignalElapsedEvent", "Media Complete"), LogType.Audit.ToString());

            DurationElapsedEvent(_filesPlayed);
        }

        /// <summary>
        /// Dispose of this media
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (_timer != null)
                    _timer.Dispose();
            }
            catch (Exception ex)
            {
                // Some things dont have a timer
                Debug.WriteLine(ex.Message);
            }

            base.Dispose(disposing);
        }
    }
}
