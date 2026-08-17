/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2026 Xibo Signage Ltd
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

namespace XiboClient.Error
{
    /// <summary>
    /// Thrown when the XMR configuration supplied by the CMS cannot be used.
    /// These are not transient and will not be resolved by retrying, so we log them at
    /// error level and surface them on the info screen rather than failing quietly.
    /// </summary>
    class XmrConfigurationException : Exception
    {
        public XmrConfigurationException(string message) : base(message)
        {
        }
    }
}
