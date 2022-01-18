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
using CefSharp;
using CefSharp.Handler;
using System.Diagnostics;

namespace XiboClient.Helpers
{
    class XiboRequestHandler : RequestHandler
    {
        private bool _isConfigureProxy;

        public XiboRequestHandler(bool isConfigureProxy)
        {
            _isConfigureProxy = isConfigureProxy;
        }

        protected override void OnRenderProcessTerminated(IWebBrowser chromiumWebBrowser, IBrowser browser, CefTerminationStatus status)
        {
            // If the render process crashed, we should just log.
            Trace.WriteLine(new LogMessage("XiboRequestHandler", "OnRenderProcessTerminate: a cef sub process has terminated. " + status.ToString()), LogType.Error.ToString());
        }

        protected override bool GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
        {
            if (_isConfigureProxy && isProxy)
            {
                callback.Continue(ApplicationSettings.Default.ProxyUser, ApplicationSettings.Default.ProxyPassword);

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
