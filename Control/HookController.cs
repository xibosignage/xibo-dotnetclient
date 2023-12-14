/**
 * Copyright (C) 2023 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - https://xibosignage.com
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
    class HookController : WebApiController
    {
        EmbeddedServer parent;

        public HookController(EmbeddedServer parent)
        {
            this.parent = parent;
        }

        /// <summary>
        /// Trigger some action.
        /// </summary>
        [Route(HttpVerbs.Post, "/")]
        public async void Trigger()
        {
            try
            {
                var data = await HttpContext.GetRequestDataAsync<TriggerRequest>();
                parent.Trigger(data.trigger, data.id);
            } 
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("HookController", "Trigger: unable to parse request: " + e.Message), LogType.Error.ToString());
            }
        }
    }
}
