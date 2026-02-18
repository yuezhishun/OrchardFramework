using Microsoft.AspNetCore.HttpOverrides;
using OrchardFramework.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
var adminBasePath = NormalizePathPrefix(builder.Configuration["SaaS:AdminBasePath"]);
var disableAdminPathAccess = builder.Configuration.GetValue("SaaS:DisableAdminPathAccess", true);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddResourceManagement();

builder.Services
    .AddOrchardCore()
    .AddMvc()
    .AddTheming()
    .AddDataAccess()
    .AddDataStorage()
    .AddDocumentManagement()
    .AddIdGeneration()
    .AddCommands()
    .AddEmailAddressValidator()
    .AddSecurity()
    .AddScripting()
    .AddSetupFeatures("OrchardCore.AutoSetup");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseForwardedHeaders();

var pathBase = builder.Configuration["ASPNETCORE_PATHBASE"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    if (!pathBase.StartsWith('/'))
    {
        pathBase = "/" + pathBase;
    }

    app.UsePathBase(pathBase);
}

if (disableAdminPathAccess)
{
    app.Use(async (context, next) =>
    {
        if (IsBlockedAdminPath(context.Request.Path, adminBasePath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });
}

app.UseStaticFiles();
app.MapSaasInspectionEndpoints();
app.MapSaasManagementEndpoints();
app.UseOrchardCore();

await app.RunAsync();

static bool IsBlockedAdminPath(PathString path, string adminBasePath)
{
    if (path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return !string.IsNullOrWhiteSpace(adminBasePath) &&
           path.StartsWithSegments(adminBasePath, StringComparison.OrdinalIgnoreCase);
}

static string NormalizePathPrefix(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return string.Empty;
    }

    var normalized = value.Trim().TrimEnd('/');
    if (!normalized.StartsWith('/'))
    {
        normalized = "/" + normalized;
    }

    return normalized;
}

public partial class Program;
