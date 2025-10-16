using System.Reflection;

using Gitlab.SourceLink.Proxy.Models;

using Microsoft.Extensions.Options;

using Serilog;
using Serilog.Exceptions;
using Serilog.Templates;
// curl https://gitlab.apps.yunqi.studio/lib/yunqi.common/-/raw/main/Yunqi.Common/Exceptions/YunqiException.cs
/*
< HTTP/2 302
< cache-control: no-cache
< content-security-policy:
< content-type: text/html; charset=utf-8
< date: Mon, 13 Oct 2025 04:20:07 GMT
< location: https://gitlab.apps.yunqi.studio/users/sign_in
< nel: {"max_age": 0}
< permissions-policy: interest-cohort=()
< referrer-policy: strict-origin-when-cross-origin
< server: nginx
< set-cookie: _gitlab_session=f362fd5231ea04f88c9b66c3a5cd6cb2; path=/; secure; HttpOnly; SameSite=None
< strict-transport-security: max-age=63072000
< x-content-type-options: nosniff
< x-download-options: noopen
< x-frame-options: SAMEORIGIN
< x-gitlab-meta: {"correlation_id":"01K7DWR3NM3R5QW45NCETGBX4J","version":"1"}
< x-permitted-cross-domain-policies: none
< x-request-id: 01K7DWR3NM3R5QW45NCETGBX4J
< x-runtime: 0.068927
< x-ua-compatible: IE=edge
< x-xss-protection: 1; mode=block
< content-length: 112

<html><body>You are being <a href="https://gitlab.apps.yunqi.studio/users/sign_in">redirected</a>.</body></html
*/

// curl https://gitlab.apps.yunqi.studio/api/v4/projects/lib%2Fyunqi.common/repository/files/Yunqi.Common%2FLogging%2FInitLog.cs/raw?ref=7fe9b03c0d01ee8108b9e11dc7b40006c94bb3ae
/* 
< HTTP / 2 404
< cache - control: no - cache
< content-type: application/json
< date: Mon, 13 Oct 2025 04:22:21 GMT
< nel: { "max_age": 0}
< server: nginx
< strict - transport - security: max - age = 63072000
< vary: Origin
< x-content-type-options: nosniff
< x-frame-options: SAMEORIGIN
< x-gitlab-meta: { "correlation_id":"01K7DWW62NXTNRQWV7DBKABES9","version":"1"}
< x-request-id: 01K7DWW62NXTNRQWV7DBKABES9
< x-runtime: 0.026497
< content-length: 35

*Connection #0 to host gitlab.apps.yunqi.studio left intact
{ "message":"404 Project Not Found"}% 
*/
const string DEFAULT_LOG_TEMPLATE = @"[{@t:yyyy-MM-ddTHH:mm:ss} {Coalesce(CorrelationId, '0000000000000:00000000')} {@l:u3}] {@m}\n{@x}";

Log.Logger = new LoggerConfiguration ()
    .WriteTo.Console (new ExpressionTemplate(DEFAULT_LOG_TEMPLATE))
    .Enrich.FromLogContext ()
    .Enrich.WithCorrelationId ()
    .Enrich.WithExceptionDetails ()
    .MinimumLevel.Information ()
    .CreateBootstrapLogger ();

var assembly = Assembly.GetExecutingAssembly ();
var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute> ()?.InformationalVersion;

Log.Logger.Information ("Starting up {Application} {Version}", assembly.GetName().Name, version);

var builder = WebApplication.CreateBuilder (args);

var environment = builder.Environment.EnvironmentName;

builder.Configuration.Sources.Clear ();
_ = builder.Configuration
    .AddEnvironmentVariables ("ASPNETCORE_")
    .AddCommandLine (args)
    .AddEnvironmentVariables ("DOTNET_")
    .AddYamlFile ("appsettings.yml", optional: false)
    .AddYamlFile ($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
    .AddYamlFile ($"appsettings.{environment}.Vault.yml", optional: true, reloadOnChange: true)
    .AddUserSecrets (Assembly.GetExecutingAssembly ())
    .AddEnvironmentVariables ("");

builder.Services
    .AddSerilog ()
    .AddHttpContextAccessor ();

var appConfigSection = builder.Configuration.GetRequiredSection (nameof (AppConfig));
var config = appConfigSection.Get<AppConfig> (o => o.ErrorOnUnknownConfiguration = true);

if (config is null)
{
    config = new AppConfig ();
    Log.Warning ("Configuration section {SectionName} is missing or invalid, using default configuration.",
        nameof (AppConfig));
}
var v = new ValidateAppConfig ();
var validation = v.Validate (nameof (AppConfig), config);
if (validation.Failed)
{
    throw new Exception (validation.FailureMessage);
}
_ = builder.Services.AddSingleton<IValidateOptions<AppConfig>> (v);

_ = builder.Services.AddOptions<AppConfig> ()
    .Bind (appConfigSection)
    .ValidateOnStart ();

_ = builder.Services
    .AddTransient<Gitlab.SourceLink.Proxy.Transforms.Request> ()
    .AddTransient<Gitlab.SourceLink.Proxy.Transforms.Response> ();


builder.Host.UseSerilog ((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration (context.Configuration)
        .ReadFrom.Services (services)
        .Enrich.FromLogContext ()
        .Enrich.WithCorrelationId ()
        .Enrich.WithExceptionDetails ()
        .WriteTo.Console (new ExpressionTemplate (DEFAULT_LOG_TEMPLATE)
    )
);

builder.Services.AddReverseProxy ()
    .LoadFromConfig (builder.Configuration.GetRequiredSection ("Yarp"))
    .AddTransforms (builder =>
    {
        builder.RequestTransforms.Add (builder.Services.GetRequiredService<Gitlab.SourceLink.Proxy.Transforms.Request> ());
        builder.ResponseTransforms.Add (builder.Services.GetRequiredService<Gitlab.SourceLink.Proxy.Transforms.Response> ());
    });




var app = builder.Build ();

app.MapReverseProxy ();

await app.RunAsync ();
