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
using Flurl;
using Flurl.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiboClient.Action
{
    class HttpCommand
    {
        /// <summary>
        /// The command
        /// </summary>
        private Command _command;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="command">The command to run</param>
        public HttpCommand(Command command)
        {
            _command = command;
        }

        /// <summary>
        /// Run the command
        /// </summary>
        /// <returns></returns>
        public async Task<int> RunAsync()
        {
            if (!_command.CommandString.StartsWith("http"))
            {
                throw new ArgumentException("Not a HTTP command");
            }

            // Split the command string by "pipe"
            string[] command = _command.CommandString.Split('|');

            // The format is
            // http|<url>|<content-type|application/x-www-form-urlencoded|application/json|text/plain>|<body-data>
            var url = new Url(command[1]);
            var contentType = command[2];
            var config = JsonConvert.DeserializeObject<HttpCommandConfig>(command[3]);

            if (!string.IsNullOrEmpty(config.headers))
            {
                url.WithHeaders(JObject.Parse(config.headers));
            }

            IFlurlResponse result;
            switch (config.method.ToUpperInvariant())
            {
                case "GET":
                    result = await url.GetAsync();
                    break;

                case "POST":
                    result = (contentType == "application/json") ? await url.PostJsonAsync(JObject.Parse(config.body)) : await url.PostStringAsync(config.body);
                    break;

                case "PUT":
                    result = (contentType == "application/json") ? await url.PutJsonAsync(JObject.Parse(config.body)) : await url.PutStringAsync(config.body);
                    break;

                case "DELETE":
                    result = await url.DeleteAsync();
                    break;

                default:
                    throw new Exception("Unsupported method: " + config.method);
            }

            return result.StatusCode;
        }
    }
}
