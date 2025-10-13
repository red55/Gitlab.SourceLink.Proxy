using System.Runtime.CompilerServices;

namespace Gitlab.SourceLink.Proxy.Extensions
{
    //bGtvcm9raDpnbHBhdC1QT1ByTlRpQWtsODVESDZtUUlRdjVXODZNUXAxT2pFeENBLjAxLjB5MDdibnVrZg==
    public static class HeadersExtension
    {
        [MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool AuthHeadersPresent(this IHeaderDictionary headers)
        {
            return headers.ContainsKey ("Authorization")
                || headers.ContainsKey ("PRIVATE-TOKEN")
                || headers.ContainsKey ("JOB-TOKEN")
                || headers.ContainsKey ("Sudo");
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool AuthHeadersMissing(this IHeaderDictionary headers)
        {
            return !AuthHeadersPresent (headers);
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool SourceLinkUserAgent(this IHeaderDictionary headers)
        {
            if (headers.TryGetValue ("User-Agent", out var userAgent))
            {
                return userAgent.ToString ().Contains ("SourceLink");
            }
            return false;
        }
        [MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool NotSourceLinkUserAgent(this IHeaderDictionary headers)
        {
            return !SourceLinkUserAgent (headers);
        }
    }
}
