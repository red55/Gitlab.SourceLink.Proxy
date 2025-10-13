using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Options;


namespace Gitlab.SourceLink.Proxy.Models
{
    [OptionsValidator]
    public partial class ValidateAppConfig : IValidateOptions<AppConfig> { }

    public class AppConfig
    {
        public static readonly string SectionName = nameof (AppConfig);
        public static readonly Uri EmptyUri = new ("about:blank");

        [Required]
        public Uri GitlabBaseUrl { get; set; } = EmptyUri;
    }
}
