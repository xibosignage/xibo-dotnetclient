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
using System.Collections.ObjectModel;
using System.Xml;

namespace XiboClient
{
    class RssReader : IDisposable
    {
        private string feedURL;
        private string feedTitle;
        private string feedDescription;
        private Collection<RssItem.Item> feedItems = new Collection<RssItem.Item>();

        private bool _IsDisposed;


        #region Properties

        /// <summary>
        /// Gets or sets the URL of the RSS feed to parse.
        /// </summary>
        public string Url
        {
            get { return feedURL; }
            set { feedURL = value; }
        }


        /// <summary>
        /// Gets all the items in the RSS feed.
        /// </summary>
        public Collection<RssItem.Item> RssItems
        {
            get { return feedItems; }
        }

        /// <summary>
        /// Gets the title of the RSS feed.
        /// </summary>
        public string FeedTitle
        {
            get { return feedTitle; }
        }

        /// <summary>
        /// Gets the description of the RSS feed.
        /// </summary>
        public string FeedDescription
        {
            get { return feedDescription; }
        }

        #endregion

        /// <summary>
        /// Retrieves the remote RSS feed and parses it.
        /// </summary>
        public Collection<RssItem.Item> GetFeed()
        {
            //check to see if the FeedURL is empty
            if (String.IsNullOrEmpty(Url))
            {
                //throw an exception if not provided
                throw new ArgumentException("You must provide a feed URL");
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(Url);

            //parse the items of the feed
            ParseDocElements(xmlDoc.SelectSingleNode("//channel"), "title", ref feedTitle);
            ParseDocElements(xmlDoc.SelectSingleNode("//channel"), "description", ref feedDescription);

            ParseRssItems(xmlDoc);

            //return the feed items
            return feedItems;
        }


        /// <summary>
        /// Parses the xml document in order to retrieve the RSS items.
        /// </summary>
        private void ParseRssItems(XmlDocument xmlDoc)
        {
            feedItems.Clear();
            XmlNodeList nodes = xmlDoc.SelectNodes("rss/channel/item");

            foreach (XmlNode node in nodes)
            {
                RssItem.Item item = new RssItem.Item();
                ParseDocElements(node, "title", ref item.Title);
                ParseDocElements(node, "description", ref item.Description);
                ParseDocElements(node, "link", ref item.Link);

                string date = null;
                ParseDocElements(node, "pubDate", ref date);
                DateTime.TryParse(date, out item.Date);

                feedItems.Add(item);
            }
        }


        /// <summary>
        /// Parses the XmlNode with the specified XPath query
        /// and assigns the value to the property parameter.
        /// </summary>
        private void ParseDocElements(XmlNode parent, string xPath, ref string property)
        {
            XmlNode node = parent.SelectSingleNode(xPath);
            if (node != null)
                property = node.InnerText;
            else
                property = "Unresolvable";
        }

        #region IDisposable Members

        /// <summary>
        /// Performs the disposal.
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (disposing && !_IsDisposed)
            {
                feedItems.Clear();
                feedURL = null;
                feedTitle = null;
                feedDescription = null;
            }

            _IsDisposed = true;
        }

        /// <summary>
        /// Releases the object to the garbage collector
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    class RssItem
    {
        /// <summary>
        /// A structure to hold the RSS Feed items
        /// </summary>
        [Serializable]
        public struct Item
        {
            /// <summary>
            /// The publishing date.
            /// </summary>
            public DateTime Date;

            /// <summary>
            /// The title of the feed
            /// </summary>
            public string Title;

            /// <summary>
            /// A description of the content (or the feed itself)
            /// </summary>
            public string Description;

            /// <summary>
            /// The link to the feed
            /// </summary>
            public string Link;
        }
    }
}
