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
using EmbedIO.Routing;
using EmbedIO;
using EmbedIO.WebApi;
using System;
using System.Threading.Tasks;

namespace XiboClient.Control
{
    internal class FaultController : WebApiController
    {
        [Route(HttpVerbs.Post, "/")]
        public async Task<string> Fault()
        {  
            if (!HttpContext.Request.IsLocal)
            {
                throw HttpException.Forbidden();
            }

            try
            {
                var data = await HttpContext.GetRequestDataAsync<FaultRequest>();

                // Do some work to get from the key to the layoutId/widgetId
                if (string.IsNullOrEmpty(data.key) || !data.key.Contains("_"))
                {
                    throw new Exception("Invalid key, must be in the format xiboIC_<widgetId>");
                }

                string[] splitKey = data.key.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

                // widgetId must be parseable as an integer
                // only old widgets have string IDs and they would not support calling this method.
                int widgetId = int.Parse(splitKey[1]);

                CacheManager.Instance.AddUnsafeWidget(
                    (UnsafeFaultCodes)data.code,
                    widgetId + "",
                    data.reason,
                    data.ttl
                );
            }
            catch (Exception e)
            {
                LogMessage.Error("FaultController", "Fault", "Trigger: unable to parse request: " + e.Message);
                LogMessage.Trace("FaultController", "Fault", e.StackTrace.ToString());
                throw HttpException.NotAcceptable();
            }

            return string.Empty;
        }
    }
}
