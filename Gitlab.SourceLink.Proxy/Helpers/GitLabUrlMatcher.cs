using System.Text.RegularExpressions;

namespace Gitlab.SourceLink.Proxy.Helpers;

public static partial class GitLabUrlMatcher
{
    [GeneratedRegex (@"%[0-9A-Fa-f]{2}")]
    private static partial Regex matchUrlEncoded { get; }

    public static bool IsUrlEncoded(string url)
    {
        return matchUrlEncoded.IsMatch (url);
    }
    [GeneratedRegex (@"^/(?<project>.+)/-/raw/(?<commit>\w+)/(?<path>.+)")]
    public static partial Regex parseRawFileUrl { get; }

    [GeneratedRegex (@"^/.+/-/raw/")]
    public static partial Regex matchRawFileUrl { get; }

    [GeneratedRegex (@"^/api/v4/projects/(?<project>.+)/repository/files/(?<path>.+)/raw\?ref=(?<commit>\w+)")]
    public static partial Regex parseApiRawFileUrl { get; }

    [GeneratedRegex (@"^/api/v4/projects/.+/repository/files/")]
    public static partial Regex matchApiRawileUrl { get; }

}
