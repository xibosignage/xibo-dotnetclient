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
using System.Collections.ObjectModel;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace XiboClient
{
    class Rss : Media
    {
        int scheduleId;
        int layoutId;

        /// <summary>
        /// Creates an RSS position with the RegionOptions parameter
        /// </summary>
        /// <param name="options"></param>
        public Rss(RegionOptions options)
            : base(options.width, options.height, options.top, options.left)
        {
            if (String.IsNullOrEmpty(options.uri))
            {
                throw new ArgumentNullException("Uri", "The Uri for the RSS feed can not be empty");
            }

            // we are going to display this in a web browser control.
            this.filePath = options.uri;
            this.direction = options.direction;
            this.backgroundImage = options.backgroundImage;
            this.backgroundColor = options.backgroundColor;
            copyrightNotice = options.copyrightNotice;
            mediaid = options.mediaid;
            scheduleId = options.scheduleId;
            layoutId = options.layoutId;

            // Set up the backgrounds
            backgroundTop = options.backgroundTop + "px";
            backgroundLeft = options.backgroundLeft + "px";

            webBrowser = new WebBrowser();
            webBrowser.Size = this.Size;
            webBrowser.ScrollBarsEnabled = false;

            this.documentText = options.text;
            this.documentTemplate = options.documentTemplate;

            try
            {
                webBrowser.DocumentText = String.Format("<html><head><script type='text/javascript'>{0}</script><style type='text/css'>p, h1, h2, h3, h4, h5 {{ margin:2px; font-size:{1}em; }}</style></head><body></body></html>", Properties.Resources.textRender, options.scaleFactor.ToString());            
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", e.Message, options.scheduleId, options.layoutId, options.mediaid));
            }

            webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(webBrowser_DocumentCompleted);
        }

        /// <summary>
        /// Refreshes the Local Rss copy from the Remote source
        /// Uses a Async call
        /// </summary>
        private void refreshLocalRss()
        {
            try
            {
                wc = new System.Net.WebClient();

                wc.OpenReadCompleted += new System.Net.OpenReadCompletedEventHandler(wc_OpenReadCompleted);
                
                wc.OpenReadAsync(new Uri(filePath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", ex.Message, scheduleId, layoutId, mediaid));
            }
        }

        void wc_OpenReadCompleted(object sender, System.Net.OpenReadCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                System.Diagnostics.Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", e.Error, scheduleId, layoutId, mediaid));

                return;
            }

            System.IO.Stream data = e.Result;

            wc.Dispose();

            try
            {
                System.IO.StreamReader sr = new System.IO.StreamReader(data);

                StreamWriter sw = new StreamWriter(File.Open(rssFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));

                sw.Write(sr.ReadToEnd());

                sr.Close();
                sw.Close();

                rssReady = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", ex.Message, scheduleId, layoutId, mediaid));
            }

            try
            {
                if (rssReady)
                {
                    loadRss();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                // The control might have expired by the time this returns
            }
        }

        /// <summary>
        /// Handles the document completed event.
        /// Resets the Background color to be options.backgroundImage
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            rssReady = false;

            // Pull the RSS feed, and put it in a temporary file cache
            // We want to check the file exists first
            rssFilePath = Properties.Settings.Default.LibraryPath + @"\" + mediaid + ".xml";

            if (!System.IO.File.Exists(rssFilePath))
            {
                refreshLocalRss();
            }
            else
            {
                // It exists - therefore we want to get the last time it was updated
                DateTime lastWriteDate = System.IO.File.GetLastWriteTime(rssFilePath);

                if (DateTime.Now.CompareTo(lastWriteDate.AddHours(6.0)) > 0)
                {
                    refreshLocalRss();
                }
                else
                {
                    rssReady = true;
                }
            }

            if (rssReady)
            {
                loadRss();
            }
            else
            {
                // Load a loading sign - assume the RSS will be ready after being handled by the thread
                loadLoading();
            }

            //Add the control
            this.Controls.Add(webBrowser);
        }

        private void loadLoading()
        {
            HtmlDocument htmlDoc = webBrowser.Document;

            if (backgroundImage == null || backgroundImage == "")
            {
                htmlDoc.Body.Style = "background-color:" + backgroundColor + " ;";
            }
            else
            {
                htmlDoc.Body.Style = "background-image: url('" + backgroundImage + "'); background-attachment:fixed; background-color:" + backgroundColor + " background-repeat: no-repeat; background-position: " + backgroundLeft + " " + backgroundTop + ";";
            }

            htmlDoc.Body.InnerHtml = "<h1>Loading...</h1>";
        }

        private void loadRss()
        {
            // Create the HtmlDoc and set the backgrounds and stuff
            HtmlDocument htmlDoc = webBrowser.Document;
            
            htmlDoc.Body.InnerHtml = "";

            if (backgroundImage == null || backgroundImage == "")
            {
                htmlDoc.Body.Style = "background-color:" + backgroundColor + " ;";
            }
            else
            {
                htmlDoc.Body.Style = "background-image: url('" + backgroundImage + "'); background-attachment:fixed; background-color:" + backgroundColor + " background-repeat: no-repeat; background-position: " + backgroundLeft + " " + backgroundTop + ";";
            }

            //Get the RSS
            rssReader = new RssReader();
            rssReader.Url = rssFilePath;

            try
            {
                rssReader.GetFeed();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(String.Format("[*]ScheduleID:{1},LayoutID:{2},MediaID:{3},Message:{0}", ex.Message, scheduleId, layoutId, mediaid));
                
                rssReader.Dispose();
                htmlDoc.Body.InnerHtml = "<h1>Can not display this feed.</h1>";

                return;
            }

            item = rssReader.RssItems;

            if (direction == "single")
            {
                // Hand the control over to a timer
                itemTick = new Timer();
                itemTick.Interval = (Duration / item.Count) * 1000;

                itemTick.Tick += new EventHandler(itemTick_Tick);

                // Charge the first one
                SingleItemRender();

                // Start the timer to get the others
                itemTick.Start();

                return;
            }

            //for each item that has been returned by the feed, do some trickery
            for (int i = 0; i < item.Count; i++)
            {
                String temp = documentTemplate;

                temp = temp.Replace("[Title]", item[i].Title);
                temp = temp.Replace("[Description]", item[i].Description);
                temp = temp.Replace("[Date]", item[i].Date.ToString());
                temp = temp.Replace("[Link]", item[i].Link);

                if (direction == "left" || direction == "right")
                {
                    // Remove all <p></p> from the temp
                    temp = temp.Replace("<p>", "");
                    temp = temp.Replace("</p>", "");

                    // Sub in the temp to the format string
                    documentText += string.Format("<span class='article' style='padding-left:4px;'>{0}</span>", temp);
                    documentText += string.Format("<span style='padding-left:4px;'>{0}</span>", " - ");
                }
                else
                {
                    documentText += string.Format("<div style='display:block;padding:4px;'>{0}</div>", temp);
                }
            }

            // Add the Copyright Notice
            documentText += CopyrightNotice;

            //decide whether we need a marquee or not
            if (direction == "none")
            {
                // we dont
                // set the body of the webBrowser to the document text (altered by the RSS feed)
                htmlDoc.Body.InnerHtml = documentText;
            }
            else
            {
                // Read the contents of the TextRender.js file

                String textRender = "";
                String textWrap = "";
                if (direction == "left" || direction == "right")
                {
                    // Make sure the text does not wrap when going from left to right.
                    textWrap = "white-space: nowrap";
                    documentText = String.Format("<nobr>{0}</nobr>", documentText);
                }
                else
                {
                    // Up and Down
                    textWrap = String.Format("width: {0}px;", this.width - 50);
                }

                textRender += string.Format("<div id='text' style='position:relative;overflow:hidden;width:{0}px; height:{1}px;'>", this.width, this.height);
                textRender += string.Format("<div id='innerText' style='position:absolute; left: 0px; top: 0px; {0}'>{1}</div></div>", textWrap, documentText);
                
                htmlDoc.Body.InnerHtml = textRender;

                // Call the JavaScript on the page
                Object[] objArray = new Object[2];
                objArray[0] = direction;
                objArray[1] = 30;

                htmlDoc.InvokeScript("init", objArray);
            }

            //clean up
            item.Clear();

            // We dont need the reader anymore
            rssReader.Dispose();
        }

        void itemTick_Tick(object sender, EventArgs e)
        {
            // Call the SingleItemRender
            SingleItemRender();

            Application.DoEvents();
        }

        /// <summary>
        /// Renders a single RSS item from the stack
        /// </summary>
        void SingleItemRender()
        {
            currentItem++;

            //Reset it
            if (currentItem >= item.Count)
            {
                currentItem = 0;
            }

            String temp = documentTemplate;

            temp = temp.Replace("[Title]", item[currentItem].Title);
            temp = temp.Replace("[Description]", item[currentItem].Description);
            temp = temp.Replace("[Date]", item[currentItem].Date.ToString());
            temp = temp.Replace("[Link]", item[currentItem].Link);

            documentText = string.Format("<div style='display:block;'>{0}</div>", temp);

            HtmlDocument htmlDoc = webBrowser.Document;

            htmlDoc.Body.InnerHtml = documentText;

            return;
        }

        /// <summary>
        /// The Formatted Copyright notice
        /// </summary>
        private String CopyrightNotice
        {
            get
            {
                if (direction == "left" || direction == "right")
                {
                    return String.Format("<span style='font-family: Arial; font-size: 8px;'>{0}</span>", copyrightNotice);
                }
                else
                {
                    return String.Format("<div style='display:block;font-family: Arial; font-size: 8px;'>{0}</div>", copyrightNotice); ;
                }
            }
        }

        /// <summary>
        /// Renders the media timers
        /// </summary>
        public override void RenderMedia()
        {
            base.RenderMedia();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (item != null) item.Clear();

                if (itemTick != null)
                {
                    itemTick.Stop();
                    itemTick.Dispose();

                    System.Diagnostics.Debug.WriteLine("Disposed of the itemTick Timer","Rss - Dispose");
                }

                if (rssReader != null)
                {
                     // We dont need the reader anymore
                     rssReader.Dispose();

                     System.Diagnostics.Debug.WriteLine("Disposed of the RSS Reader", "Rss - Dispose");
                }

                try
                {
                    webBrowser.DocumentText = "";
                    webBrowser.Dispose();

                    System.Diagnostics.Debug.WriteLine("Disposed of the Web Browser control", "Rss - Dispose");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Web browser control already disposed", "Rss - Dispose");
                }
            }

            base.Dispose(disposing);
        }

        private Collection<RssItem.Item> item;
        private string filePath;
        private string direction;
        private string backgroundImage;
        private string backgroundColor;
        private WebBrowser webBrowser;
        private string copyrightNotice;
        private string mediaid;

        private string rssFilePath;

        //Build up the RSS feed
        private string documentText;
        private string documentTemplate;

        private string backgroundTop;
        private string backgroundLeft;
        System.Net.WebClient wc;

        private bool rssReady;
        private int currentItem = -1;

        Timer itemTick;
        RssReader rssReader;
    }
}
