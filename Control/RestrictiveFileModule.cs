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
using EmbedIO.Files;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XiboClient.Control
{
    class RestrictiveFileModule : FileModule
    {
        private List<string> _restrictedPaths;

        public RestrictiveFileModule(string baseRoute, IFileProvider provider, List<string> restrictedPaths)
            : base(baseRoute, provider)
        {
            _restrictedPaths = restrictedPaths;
        }

        protected override async Task OnRequestAsync(IHttpContext context)
        {
            if (_restrictedPaths.Any(o => context.RequestedPath.StartsWith(o)))
            {
                throw HttpException.Forbidden();
            }
            else
            {
                await base.OnRequestAsync(context);
            }
        }
    }
}
