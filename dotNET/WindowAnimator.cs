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
using System.Windows.Forms;

namespace XiboClient
{    
    public class WindowAnimator    
    {        
        Form window;        
        float Step;        
        Timer time;
        Direction dir;

        public enum Direction
        {
            FadeIn, FadeOut
        }

        bool fadeOut = false;
        
        public WindowAnimator(Form FormToAnimate)        
        {            
            window = FormToAnimate;        
        }        
        
        public void WindowFadeIn(int interval, float steps, Direction direction)        
        {            
            //Save steps            
            Step = steps;
            
            //Create Timer            
            time = new Timer();            
            time.Interval = interval;
            if (direction == Direction.FadeIn)
            {
                time.Tick += new EventHandler(Timer_TickIn); 
            }
            else
            {
                time.Tick += new EventHandler(Timer_TickOut); 
            }
                       
            time.Start();        
        }        
        
        private void Timer_TickIn(object sender, EventArgs e)        
        {            
            //Check the Opacity of the form            
            if (window.Opacity != 1.0)            
            {                
                //Lower then 1, increment opacity                
                window.Opacity += Step;            
            }            
            else            
            {                
                //We´re finished, stop the timer                
                time.Stop();

                try
                {
                    FadeComplete(window);
                }
                catch { 
                    // There might not be an event handler
                }
            }        
        }

        private void Timer_TickOut(object sender, EventArgs e)
        {
            //Check the Opacity of the form            
            if (window.Opacity != 0.0)
            {
                //Lower then 1, increment opacity                
                window.Opacity -= Step;
            }
            else
            {
                //We´re finished, stop the timer                
                time.Stop();

                FadeComplete(window);
            }
        }

        public delegate void FadeCompleteDelegate(Form f);
        public event FadeCompleteDelegate FadeComplete;
    }
}