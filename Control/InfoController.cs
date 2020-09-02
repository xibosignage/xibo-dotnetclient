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
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using XiboClient.Log;

namespace XiboClient.Control
{
    class InfoController : WebApiController
    {
        [Route(HttpVerbs.Get, "/")]
        public void GetInfo()
        {
            Response.ContentType = MimeType.Json;
            using (var writer = HttpContext.OpenResponseText(Encoding.UTF8, true))
            {
                JObject jObject = JObject.FromObject( new
                {
                    hardwareKey = ApplicationSettings.Default.HardwareKey.ToString(),
                    displayName = ApplicationSettings.Default.DisplayName,
                    timeZone = ApplicationSettings.Default.DisplayTimeZone
                });

                if (ClientInfo.Instance.CurrentGeoLocation != null && !ClientInfo.Instance.CurrentGeoLocation.IsUnknown)
                {
                    jObject.Add("latitude", ClientInfo.Instance.CurrentGeoLocation.Latitude);
                    jObject.Add("longitude", ClientInfo.Instance.CurrentGeoLocation.Longitude);
                }
                else
                {
                    jObject.Add("latitude", null);
                    jObject.Add("longitude", null);
                }

                writer.Write(jObject.ToString());
            }
        }
    }
}
