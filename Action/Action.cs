/**
 * Copyright (C) 2021 Xibo Signage Ltd
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace XiboClient.Action
{
    public class Action
    {
        public int Id { get; set; }
        public string ActionType { get; set; }
        public string TriggerType { get; set; }
        public string TriggerCode { get; set; }
        public int WidgetId { get; set; }
        public int SourceId { get; set; }
        public string Source { get; set; }
        public int TargetId { get; set; }
        public string Target { get; set; }
        public string LayoutCode { get; set; }
        public bool Bubble { get; set; }

        public Rect Rect { get; set; }

        /// <summary>
        /// Create an Action from XmlNode
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static Action CreateFromXmlNode(XmlNode node, int top, int left, int width, int height)
        {
            XmlAttributeCollection attributes = node.Attributes;

            return new Action
            {
                Id = int.Parse(attributes["id"].Value),
                ActionType = attributes["actionType"]?.Value,
                TriggerType = attributes["triggerType"]?.Value,
                TriggerCode = attributes["triggerCode"]?.Value,
                WidgetId = string.IsNullOrEmpty(attributes["widgetId"]?.Value) ? 0 : int.Parse(attributes["widgetId"].Value),
                SourceId = string.IsNullOrEmpty(attributes["sourceId"]?.Value) ? 0 : int.Parse(attributes["sourceId"].Value),
                Source = attributes["source"]?.Value,
                TargetId = string.IsNullOrEmpty(attributes["targetId"]?.Value) ? 0 : int.Parse(attributes["targetId"].Value),
                Target = attributes["target"]?.Value,
                LayoutCode = attributes["layoutCode"]?.Value,
                Bubble = false,
                Rect = new Rect(left, top, width, height)
            };
        }

        /// <summary>
        /// Create a list of Actions from an XmlNodeList
        /// </summary>
        /// <param name="nodeList"></param>
        /// <returns></returns>
        public static List<Action> CreateFromXmlNodeList(XmlNodeList nodeList)
        {
            return CreateFromXmlNodeList(nodeList, 0, 0, 0, 0);
        }

        /// <summary>
        /// Create a list of Actions from an XmlNodeList
        /// </summary>
        /// <param name="nodeList"></param>
        /// <returns></returns>
        public static List<Action> CreateFromXmlNodeList(XmlNodeList nodeList, int top, int left, int width, int height)
        {
            List<Action> actions = new List<Action>();
            foreach (XmlNode node in nodeList)
            {
                try
                {
                    actions.Add(CreateFromXmlNode(node, top, left, width, height));
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("Action", "CreateFromXmlNodeList: create from node failed. e = " + e.Message), LogType.Info.ToString());
                }
            }
            return actions;
        }

        /// <summary>
        /// Get Priority for action source
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static int PriorityForActionSource(string source)
        {
            switch (source)
            {
                case "widget":
                    return 1;

                case "region":
                    return 2;

                case "layout":
                    return 3;

                default:
                    return 4;
            }
        }

        /// <summary>
        /// Is the point inside this action
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool IsPointInside(Point point)
        {
            return Rect.Contains(point);
        }
    }
}
