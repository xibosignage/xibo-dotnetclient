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
