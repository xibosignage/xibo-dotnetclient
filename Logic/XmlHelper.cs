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
using System.Xml;

namespace XiboClient.Logic
{
    public sealed class XmlHelper
    {
        public static string SelectNodeInnerTextOrDefault(XmlNode node, string nodeName, string defaultValue)
        {
            XmlNode select = node.SelectSingleNode(nodeName);

            return select == null ? defaultValue : select.InnerText;
        }

        public static string SelectFirstElementInnerTextOrDefault(XmlDocument doc, string tagName, string defaultValue)
        {
            XmlNodeList list = doc.GetElementsByTagName(tagName);

            return (list.Count <= 0) ? defaultValue : list.Item(0).InnerText;
        }

        public static string GetAttrib(XmlNode node, string attrib, string defVal)
        {
            XmlAttribute xmlAttrib = node.Attributes[attrib];
            if (xmlAttrib == null)
                return defVal;

            string val = xmlAttrib.Value;
            return val ?? defVal;
        }
    }
}
