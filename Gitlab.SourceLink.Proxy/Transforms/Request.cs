using System.Runtime.CompilerServices;
using System.Text;

using Gitlab.SourceLink.Proxy.Extensions;
using Gitlab.SourceLink.Proxy.Helpers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

using Yarp.ReverseProxy.Transforms;

namespace Gitlab.SourceLink.Proxy.Transforms
{
    public class Request(ILogger<Request> logger_) : RequestTransform
    {
        private ILogger Logger { get; } = logger_;

        private static string BuildApiRawFileUrl(string project, string path) =>
            $"/api/v4/projects/{Uri.EscapeDataString (project)}/repository/files/{Uri.EscapeDataString (path)}/raw";


        public static Uri MakeDestinationAddress(string destinationPrefix, PathString path, QueryString query)
        {
            ReadOnlySpan<char> prefixSpan = destinationPrefix;

            if (path.HasValue && destinationPrefix.EndsWith ('/'))
            {
                // When PathString has a value it always starts with a '/'. Avoid double slashes when concatenating.
                prefixSpan = prefixSpan[0..^1];
            }

            var targetAddress = string.Concat (prefixSpan, path.ToUriComponent (), query.ToUriComponent ());

            return new Uri (targetAddress, UriKind.Absolute);
        }

        public static (string? Username, string? Password) ParseBasicAuthHeader(string? authHeader)
        {
            if (string.IsNullOrWhiteSpace (authHeader) ||
                (!authHeader.StartsWith ("Basic ", StringComparison.OrdinalIgnoreCase) &&
                !authHeader.StartsWith ("Bearer ", StringComparison.OrdinalIgnoreCase)))
            {
                return (null, null);
            }

            // Extract the base64-encoded credentials (after "Basic ")
            // In case of Bearer substring will get PAT with leading space which will be trimmed by Trim()
            var encodedCredentials = authHeader.Substring ("Basic ".Length).Trim ();

            try
            {
                var decodedBytes = Convert.FromBase64String (encodedCredentials);
                var decodedString = Encoding.UTF8.GetString (decodedBytes);

                // The decoded string is in the format "username:password"
                var separatorIndex = decodedString.IndexOf (':');
                if (separatorIndex == -1)
                {
                    return (null, authHeader);
                }

                var username = decodedString.Substring (0, separatorIndex);
                var password = decodedString.Substring (separatorIndex + 1);
                return (username, password);
            }
            catch
            {
                // Invalid base64 or format
                return (null, null);
            }
        }
        internal void ReplaceAuthHeader(RequestTransformContext context)
        {
            var authHeader = TakeHeader (context, "Authorization")![0];
            var request = context.HttpContext.Request;
            var (_, password) = ParseBasicAuthHeader (authHeader);
            if (password != null && password.StartsWith ("glpat-"))
            {
                Logger.LogDebug ("Replacing Auth header with Bearer");
                request.Headers.Remove ("Authorization");
                request.Headers.Authorization = $"Bearer {password}";
                AddHeader (context, "Authorization", request.Headers.Authorization);
            }
            else
            {
                Logger.LogWarning ("Auth header dosen't contain PRIVATE-TOKEN, copy as is");
                AddHeader (context, "Authorization", authHeader);
            }
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private ValueTask ProcessGetRequest(RequestTransformContext context)
        {
            var request = context.HttpContext.Request;
            // Trick to avoid double escaping if uri already url encoded.
            // See https://github.com/dotnet/yarp/issues/1419
            context.ProxyRequest.RequestUri = MakeDestinationAddress (context.DestinationPrefix, context.Path, context.Query.QueryString);
            var url = request.Path = context.ProxyRequest.RequestUri.AbsolutePath;

            if (request.Headers.NotSourceLinkUserAgent ())
            {
                Logger.LogDebug ("Not a SourceLink request. Skip processing.");
                return ValueTask.CompletedTask;
            }

            if (request.Headers.AuthHeadersMissing ())
            {
                Logger.LogDebug ("Authentication headers are missing. Return 401.");
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status401Unauthorized;
                response.Headers.WWWAuthenticate = "Basic realm=\"Gitlab\"";
                response.ContentLength = 0;

                return new ValueTask (Task.CompletedTask);
            }


            if (GitLabUrlMatcher.matchRawFileUrl.IsMatch (url))
            {
                Logger.LogDebug ("Matched raw file URL");
                var match = GitLabUrlMatcher.parseRawFileUrl.Match (url);
                if (match.Success)
                {
                    var project = match.Groups["project"].Value;
                    var path = match.Groups["path"].Value;
                    var commit = match.Groups["commit"].Value;
                    var newPath = BuildApiRawFileUrl (project, path);
                    var query = QueryHelpers.ParseQuery (request.QueryString.Value);
                    var queryDict = new Dictionary<string, StringValues> (query)
                    {
                        ["ref"] = commit
                    };
                    var newQueryString = QueryHelpers.AddQueryString ("", queryDict);
                    request.QueryString = new QueryString (newQueryString);

                    Logger.LogDebug ("Rewriting raw file URL to: {NewPath}", newPath);
                    request.Path = newPath;

                    ReplaceAuthHeader (context);
                }
                else
                {
                    Logger.LogWarning ("Failed to parse raw file URL: {Path}", request.Path);
                }
            }
            else if (GitLabUrlMatcher.matchApiRawileUrl.IsMatch (url))
            {
                ReplaceAuthHeader (context);
            }
            else
            {
                Logger.LogDebug ("No matching transform for URL: {Path}", request.Path);
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            Logger.LogInformation ("Transforming request: {Method} {Path}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            var r = context.HttpContext.Request.Method switch
            {
                @"GET" => ProcessGetRequest (context),
                _ => ValueTask.CompletedTask,
            };

            return r;
        }
    }
}
