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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace XiboClient.Rendering
{
    class Transitions
    {
        /// <summary>
        /// Select Animation type , pass animation details 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="dp"></param>
        /// <param name="type"></param>
        /// <param name="direction"></param>
        /// <param name="duration"></param>
        /// <param name="inOut"></param>
        /// <param name="top"></param>
        /// <param name="left"></param>
        public static void MoveAnimation(object item, DependencyProperty dp, string type, string direction, double duration, string inOut, int top, int left)
        {
            switch (type)
            {
                case "fly":
                    FlyAnimation(item, direction, duration, inOut, top, left);
                    break;
                case "fadeIn":
                    FadeIn(item, dp, duration);
                    break;
                case "fadeOut":
                    FadeOut(item, dp, duration);
                    break;
            }
        }

        public static DoubleAnimation Get(string type, double duration)
        {
            return new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(duration)
            };
        }

        /// <summary>
        /// Fade in animation
        /// </summary>
        /// <param name="item"></param>
        /// <param name="dp"></param>
        /// <param name="duration"></param>
        private static void FadeIn(object item, DependencyProperty dp, double duration)
        {
            DoubleAnimation doubleAnimationFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(duration)
            };

            if (item is System.Windows.Controls.Image)
            {
                (item as System.Windows.Controls.Image).BeginAnimation(dp, doubleAnimationFade);
            }
            else if (item is MediaElement)
            {
                (item as MediaElement).BeginAnimation(dp, doubleAnimationFade);
            }
        }

        /// <summary>
        /// FadeOut animation
        /// </summary>
        /// <param name="item"></param>
        /// <param name="direction"></param>
        /// <param name="duration"></param>
        private static void FadeOut(object item, DependencyProperty dp, double duration)
        {
            DoubleAnimation doubleAnimationFade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(duration)
            };

            if (item is System.Windows.Controls.Image)
            {
                (item as System.Windows.Controls.Image).BeginAnimation(dp, doubleAnimationFade);
            }
            else if (item is MediaElement)
            {
                (item as MediaElement).BeginAnimation(dp, doubleAnimationFade);
            }
        }

        /// <summary>
        /// item moving animation with all directions
        /// </summary>
        /// <param name="item"></param>
        /// <param name="direction"></param>
        /// <param name="duration"></param>
        /// <param name="inOut"></param>
        private static void FlyAnimation(object item, string direction, double duration, string inOut, int top, int left)
        {
            int inValueY = 0;
            int inValueX = 0;

            int endValueX = 0;
            int endValueY = 0;

            int screenHeight = (int)SystemParameters.PrimaryScreenHeight;
            int screenWight = Convert.ToInt32(SystemParameters.PrimaryScreenWidth);

            if (inOut == "in")
            {
                inValueY = screenHeight;
                inValueX = screenWight;
                endValueX = 0;
                endValueY = 0;
            }
            else if (inOut == "out")
            {
                inValueY = 0;
                inValueX = 0;
                endValueX = screenWight;
                endValueY = screenHeight;
            }

            DoubleAnimation doubleAnimationX = new DoubleAnimation();
            DoubleAnimation doubleAnimationY = new DoubleAnimation();
            doubleAnimationX.To = endValueX;
            doubleAnimationY.To = endValueY;
            doubleAnimationX.Duration = TimeSpan.FromMilliseconds(duration);
            doubleAnimationY.Duration = TimeSpan.FromMilliseconds(duration);
            var trans = new TranslateTransform();
            switch (direction)
            {
                case "N":
                    if (inOut == "in")
                    {
                        doubleAnimationY.From = (screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationY.From = top;
                    }

                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    break;
                case "NE":
                    if (inOut == "in")
                    {
                        doubleAnimationX.From = (screenWight - left);
                        doubleAnimationY.From = (screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = top;
                    }

                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    break;
                case "E":
                    if (inOut == "in")
                    {
                        doubleAnimationX.From = -(screenWight - left);
                    }
                    else
                    {
                        if (left == 0)
                        {
                            doubleAnimationX.From = -left;
                        }
                        else
                        {
                            doubleAnimationX.From = -(screenWight - left);
                        }

                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    break;
                case "SE":
                    if (inOut == "in")
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = -(screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = (screenWight - left);
                        doubleAnimationY.From = -(screenHeight - top);
                    }
                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    break;
                case "S":
                    if (inOut == "in")
                    {
                        doubleAnimationX.From = -(screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = -top;
                    }

                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationX);
                    break;
                case "SW":
                    if (inOut == "in")
                    {
                        doubleAnimationX.From = (screenWight - left);
                        doubleAnimationY.From = -top;
                    }
                    else
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = -(screenHeight - left);
                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);
                    break;
                case "W":
                    if (inOut == "in")
                    {
                        doubleAnimationX.From = (screenWight - left);
                    }
                    else
                    {
                        doubleAnimationX.From = -left;
                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);

                    break;
                case "NW":
                    if (inOut == "in")
                    {
                        doubleAnimationX.From = (screenWight - left);
                        doubleAnimationY.From = (screenHeight - top);
                    }
                    else
                    {
                        doubleAnimationX.From = left;
                        doubleAnimationY.From = top;
                    }

                    trans.BeginAnimation(TranslateTransform.XProperty, doubleAnimationX);
                    trans.BeginAnimation(TranslateTransform.YProperty, doubleAnimationY);

                    break;
            }

            if (item is System.Windows.Controls.Image)
            {
                (item as System.Windows.Controls.Image).RenderTransform = trans;
            }
            else if (item is MediaElement)
            {
                (item as MediaElement).RenderTransform = trans;
            }
        }
    }
}