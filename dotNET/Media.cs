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
using XiboClient.Properties;

namespace XiboClient
{
    class Media : Form
    {
        public Media(int width, int height, int top, int left)
        {
            Hide();

            this.width = width;
            this.height = height;
            this.top = top;
            this.left = left;

            // Form properties
            this.TopLevel = false;
            this.FormBorderStyle = FormBorderStyle.None;
            //this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.Width = width;
            this.Height = height;
            this.Location = new System.Drawing.Point(0, 0);        

            // Transparency
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = System.Drawing.Color.Transparent;
            this.TransparencyKey = System.Drawing.Color.White;

            if (Settings.Default.DoubleBuffering)
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
                SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            }
        }

        protected void StartTimer()
        {
            //start the timer
            if (!timerStarted && duration != 0)
            {
                timer = new Timer();
                timer.Interval = (1000 * duration);
                timer.Start();

                timer.Tick += new EventHandler(timer_Tick);

                timerStarted = true;
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

        protected virtual void timer_Tick(object sender, EventArgs e)
        {
            // Once it has expired we might as well stop the timer?
            timer.Stop();

            SignalElapsedEvent();
        }

        /// <summary>
        /// Signals that an event is elapsed
        /// Will raise a DurationElapsedEvent
        /// </summary>
        public void SignalElapsedEvent()
        {
            this.hasExpired = true;

            System.Diagnostics.Debug.WriteLine("Media Complete", "Media - SignalElapsedEvent");

            DurationElapsedEvent();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                timer.Dispose();
            }
            catch (NullReferenceException ex)
            {
                // Some things dont have a timer
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            base.Dispose(disposing);
        }


        public delegate void DurationElapsedDelegate();
        public event DurationElapsedDelegate DurationElapsedEvent;

        #region Properties

        /// <summary>
        /// Gets or Sets the duration of this media. Will be 0 if ""
        /// </summary>
        public int Duration
        {
            get
            {
                return this.duration;
            }
            set
            {
                duration = value;
            }
        }

        #endregion

        //Variables for size and position
        public int width;
        public int height;
        public int top;
        public int left;

        public bool hasExpired = false;
        
        //timer vars
        Timer timer;
        private int duration;
        bool timerStarted = false;
    }
}
