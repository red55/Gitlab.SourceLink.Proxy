using System.Net;

using Gitlab.SourceLink.Proxy.Extensions;
using Gitlab.SourceLink.Proxy.Helpers;

using Yarp.ReverseProxy.Transforms;

namespace Gitlab.SourceLink.Proxy.Transforms
{
    public class Response(ILogger<Request> logger_) : ResponseTransform
    {
        private ILogger Logger { get; } = logger_;

        public override ValueTask ApplyAsync(ResponseTransformContext context)
        {
            if (context.HttpContext.Request.Headers.NotSourceLinkUserAgent ())
            {
                Logger.LogDebug ("Not a SourceLink request. Skip processing");
                return ValueTask.CompletedTask;
            }

            if (context.HttpContext.Request.Headers.AuthHeadersMissing ())
            {
                Logger.LogDebug ("Authentication headers are missing. Matching url");
                if (GitLabUrlMatcher.matchApiRawileUrl.IsMatch (context.HttpContext.Request.Path)
                    && context.HttpContext.Response.StatusCode == (int)HttpStatusCode.NotFound)
                {
                    Logger.LogDebug ("Matched API raw file URL and StatusCode is 404");
                    context.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.HttpContext.Response.ContentLength = 0;
                    context.HttpContext.Response.Headers.Clear ();
                    context.HttpContext.Response.Headers["Content-Length"] = "0";
                    var host = context.HttpContext.Request.Host.ToUriComponent ();

                    context.HttpContext.Response.Headers.WWWAuthenticate = $"Basic realm=\"Gitlab\"";
                    Logger.LogInformation ("Responded with 401 Unauthorized for URL: {Host} {Path}",
                        host,
                        context.HttpContext.Request.Path);
                }
                else
                {
                    Logger.LogDebug ("No matching transform for URL: {Path}", context.HttpContext.Request.Path);
                }
            }

            return ValueTask.CompletedTask;
        }
    }
}
