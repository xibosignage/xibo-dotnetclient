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
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System;
using System.Diagnostics;

namespace XiboClient.Control
{
    class DurationController : WebApiController
    {
        private EmbeddedServer _parent;

        public DurationController(EmbeddedServer parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Expire the current Widget
        /// </summary>
        [Route(HttpVerbs.Post, "/expire")]
        public async void Expire()
        {
            try
            {
                var data = await HttpContext.GetRequestDataAsync<DurationRequest>();
                _parent.Duration("expire", data.id, 0);
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("HookController", "Expire: unable to parse request: " + e.Message), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// Extend the current Widget
        /// </summary>
        [Route(HttpVerbs.Post, "/extend")]
        public async void Extend()
        {
            try
            {
                var data = await HttpContext.GetRequestDataAsync<DurationRequest>();
                _parent.Duration("extend", data.id, data.duration);
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("HookController", "Extend: unable to parse request: " + e.Message), LogType.Error.ToString());
            }
        }

        /// <summary>
        /// Set the current Widget's duration
        /// </summary>
        [Route(HttpVerbs.Post, "/set")]
        public async void Set()
        {
            try
            {
                var data = await HttpContext.GetRequestDataAsync<DurationRequest>();
                _parent.Duration("set", data.id, data.duration);
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("HookController", "Set: unable to parse request: " + e.Message), LogType.Error.ToString());
            }
        }
    }
}
