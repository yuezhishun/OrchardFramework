using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace OrchardFramework.Api.Endpoints;

public static class SaasInspectionEndpoints
{
    private sealed record SiteState(string SiteName, string TimeZoneId);
    private sealed record OpenIdState(int Applications, int Scopes, int Tokens, int Authorizations);
    private sealed record SaasState(
        bool Ready,
        DateTime LastUpdatedUtc,
        string Message,
        string DatabasePath,
        int TenantCount,
        string DefaultTenantState,
        SiteState Site,
        OpenIdState OpenId,
        FeatureStatus[] RequiredSaasFeatures,
        FeatureStatus[] CmsFeatures);

    private static readonly string[] RequiredSaasFeatures =
    [
        "OrchardCore.Admin",
        "OrchardCore.Resources",
        "OrchardCore.Themes",
        "TheAdmin",
        "OrchardCore.Tenants",
        "OrchardCore.Tenants.FeatureProfiles",
        "OrchardCore.Users",
        "OrchardCore.Roles",
        "OrchardCore.Settings",
        "OrchardCore.Localization",
        "OrchardCore.Features",
        "OrchardCore.Navigation",
        "OrchardCore.Recipes",
        "OrchardCore.Apis.GraphQL",
        "OrchardCore.OpenId",
        "OrchardCore.OpenId.Management",
        "OrchardCore.OpenId.Server",
        "OrchardCore.OpenId.Validation"
    ];

    private static readonly string[] CmsFeatureSet =
    [
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        "OrchardCore.ContentFields",
        "OrchardCore.Widgets",
        "OrchardCore.Flows",
        "OrchardCore.Media",
        "OrchardCore.Menu",
        "OrchardCore.Markdown",
        "OrchardCore.Shortcodes",
        "OrchardCore.Html",
        "OrchardCore.Liquid",
        "OrchardCore.Lists",
        "OrchardCore.Layers",
        "OrchardCore.Alias",
        "OrchardCore.Title",
        "OrchardCore.HomeRoute"
    ];

    private static readonly string[] HeadlessFeatureSet =
    [
        "OrchardCore.Localization",
        "OrchardCore.Apis.GraphQL",
        "OrchardCore.OpenId",
        "OrchardCore.OpenId.Management",
        "OrchardCore.OpenId.Server",
        "OrchardCore.OpenId.Validation"
    ];

    private sealed record FeatureStatus(string Name, bool Enabled);

    public static IEndpointRouteBuilder MapSaasInspectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/saas").WithTags("SaaS");

        group.MapGet("/summary", async (IHostEnvironment environment) =>
        {
            var state = await BuildStateAsync(environment);
            return Results.Ok(state);
        });

        group.MapGet("/features", async (IHostEnvironment environment) =>
        {
            var state = await BuildStateAsync(environment);
            return Results.Ok(new
            {
                state.Ready,
                state.LastUpdatedUtc,
                state.RequiredSaasFeatures,
                state.CmsFeatures
            });
        });

        group.MapGet("/links", (IConfiguration configuration) =>
        {
            var adminBasePath = NormalizeAdminBasePath(configuration["SaaS:AdminBasePath"]);
            var adminPathAccessDisabled = IsAdminPathAccessDisabled(configuration);

            return Results.Ok(new[]
            {
                new { Name = "SaaS 总览 API", Url = "/api/saas/summary", Description = "当前配方、租户数量、站点配置与功能状态", Enabled = true },
                new { Name = "功能状态 API", Url = "/api/saas/features", Description = "必需 SaaS 功能与 CMS 功能禁用状态", Enabled = true },
                new { Name = "Headless 能力矩阵", Url = "/api/saas/capabilities", Description = "GraphQL/OpenId 与管理能力可用性", Enabled = true },
                new { Name = "租户 API（Orchard 内置）", Url = "/api/tenants/create", Description = "OrchardCore.Tenants 内置 API 入口之一", Enabled = true },
                new { Name = "租户管理 API（适配层）", Url = "/api/management/tenants", Description = "Vue 管理台租户增改启停入口", Enabled = true },
                new { Name = "功能管理 API（适配层）", Url = "/api/management/features", Description = "Vue 管理台模块启停入口", Enabled = true },
                new { Name = "功能配置模板 API（适配层）", Url = "/api/management/feature-profiles", Description = "租户功能模板读取与更新入口", Enabled = true },
                new { Name = "站点设置 API（适配层）", Url = "/api/management/site-settings", Description = "站点基础配置读取与更新入口", Enabled = true },
                new { Name = "本地化 API（适配层）", Url = "/api/management/localization", Description = "默认文化与支持文化读取与更新入口", Enabled = true },
                new { Name = "OpenId 应用 API（适配层）", Url = "/api/management/openid/applications", Description = "OpenId 客户端应用管理入口", Enabled = true },
                new { Name = "OpenId Scope API（适配层）", Url = "/api/management/openid/scopes", Description = "OpenId Scope 管理入口", Enabled = true },
                new { Name = "配方列表 API（适配层）", Url = "/api/management/recipes", Description = "可执行配方列表与元数据读取入口", Enabled = true },
                new { Name = "配方执行 API（适配层）", Url = "/api/management/recipes/execute", Description = "按租户执行配方入口", Enabled = true },
                new { Name = "后台首页", Url = $"{adminBasePath}/Admin", Description = adminPathAccessDisabled ? "临时关闭：当前阶段仅开放 /saas 管理台。" : "OrchardCore 管理后台入口", Enabled = !adminPathAccessDisabled },
                new { Name = "租户管理", Url = $"{adminBasePath}/Admin/Tenants", Description = adminPathAccessDisabled ? "临时关闭：请改用 /saas 管理台。" : "租户列表与 URL 管理", Enabled = !adminPathAccessDisabled },
                new { Name = "功能管理", Url = $"{adminBasePath}/Admin/Features", Description = adminPathAccessDisabled ? "临时关闭：请改用 /saas 管理台。" : "模块启停管理", Enabled = !adminPathAccessDisabled },
                new { Name = "租户功能配置", Url = $"{adminBasePath}/Admin/TenantFeatureProfiles/Index", Description = adminPathAccessDisabled ? "临时关闭：请改用 /saas 管理台。" : "按租户配置功能开关模板", Enabled = !adminPathAccessDisabled }
            });
        });

        group.MapGet("/capabilities", async (IHostEnvironment environment, IConfiguration configuration) =>
        {
            var state = await BuildStateAsync(environment);
            var enabled = state.Ready && File.Exists(state.DatabasePath)
                ? await ReadEnabledFeaturesAsync(state.DatabasePath)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var adminBasePath = NormalizeAdminBasePath(configuration["SaaS:AdminBasePath"]);
            var adminPathAccessDisabled = IsAdminPathAccessDisabled(configuration);
            var allowAnonymousManagementApi = configuration.GetValue("SaaS:AllowAnonymousManagementApi", true);

            var headlessFeatures = HeadlessFeatureSet
                .Select(name => new FeatureStatus(name, enabled.Contains(name)))
                .ToArray();

            var missing = headlessFeatures
                .Where(x => !x.Enabled)
                .Select(x => x.Name)
                .ToArray();

            return Results.Ok(new
            {
                state.Ready,
                state.LastUpdatedUtc,
                mode = "headless-mixed",
                managementUi = "/saas",
                fallbackAdminUi = $"{adminBasePath}/Admin",
                adminPathAccessDisabled,
                builtInApis = new[]
                {
                    "/api/tenants/create",
                    "/api/tenants/edit",
                    "/api/tenants/enable/{tenantName}",
                    "/api/tenants/disable/{tenantName}",
                    "/api/tenants/remove/{tenantName}",
                    "/api/tenants/setup"
                },
                availableAdapters = new[]
                {
                    "/api/management/tenants",
                    "/api/management/features",
                    "/api/management/feature-profiles",
                    "/api/management/users",
                    "/api/management/roles",
                    "/api/management/permissions",
                    "/api/management/roles/{id}/permissions",
                    "/api/management/site-settings",
                    "/api/management/localization",
                    "/api/management/openid/applications",
                    "/api/management/openid/scopes",
                    "/api/management/recipes",
                    "/api/management/recipes/execute"
                },
                plannedAdapters = Array.Empty<string>(),
                allowAnonymousManagementApi,
                headlessFeatures,
                missingHeadlessFeatures = missing
            });
        });

        return app;
    }

    private static async Task<SaasState> BuildStateAsync(IHostEnvironment environment)
    {
        var now = DateTime.UtcNow;
        var appDataPath = Path.Combine(environment.ContentRootPath, "App_Data");
        var dbPath = Path.Combine(appDataPath, "Sites", "Default", "OrchardCore.db");

        if (!File.Exists(dbPath))
        {
            return new SaasState(
                Ready: false,
                LastUpdatedUtc: now,
                Message: "Default tenant database not found. Trigger setup by requesting /api/saas/summary once.",
                DatabasePath: dbPath,
                TenantCount: 0,
                DefaultTenantState: "Unknown",
                Site: new SiteState("", ""),
                OpenId: new OpenIdState(0, 0, 0, 0),
                RequiredSaasFeatures: RequiredSaasFeatures.Select(name => new FeatureStatus(name, false)).ToArray(),
                CmsFeatures: CmsFeatureSet.Select(name => new FeatureStatus(name, false)).ToArray());
        }

        var enabled = await ReadEnabledFeaturesAsync(dbPath);
        var siteSettings = await ReadSiteSettingsAsync(dbPath);
        var openIdState = await ReadOpenIdStateAsync(dbPath);
        var (tenantCount, defaultState) = ReadTenantState(appDataPath);

        return new SaasState(
            Ready: true,
            LastUpdatedUtc: now,
            Message: "",
            DatabasePath: dbPath,
            TenantCount: tenantCount,
            DefaultTenantState: defaultState,
            Site: new SiteState(siteSettings.SiteName, siteSettings.TimeZoneId),
            OpenId: openIdState,
            RequiredSaasFeatures: RequiredSaasFeatures
                .Select(name => new FeatureStatus(name, enabled.Contains(name)))
                .ToArray(),
            CmsFeatures: CmsFeatureSet
                .Select(name => new FeatureStatus(name, enabled.Contains(name)))
                .ToArray());
    }

    private static async Task<OpenIdState> ReadOpenIdStateAsync(string dbPath)
    {
        var applications = await ReadTableCountAsync(dbPath, "OpenId_OpenIdApplicationIndex");
        var scopes = await ReadTableCountAsync(dbPath, "OpenId_OpenIdScopeIndex");
        var tokens = await ReadTableCountAsync(dbPath, "OpenId_OpenIdTokenIndex");
        var authorizations = await ReadTableCountAsync(dbPath, "OpenId_OpenIdAuthorizationIndex");

        return new OpenIdState(applications, scopes, tokens, authorizations);
    }

    private static async Task<HashSet<string>> ReadEnabledFeaturesAsync(string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "select Content from Document where Type like 'OrchardCore.Environment.Shell.Descriptor.Models.ShellDescriptor%' limit 1;";

        var json = await command.ExecuteScalarAsync() as string;
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("Id", out var idElement))
            {
                continue;
            }

            var id = idElement.GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                set.Add(id);
            }
        }

        return set;
    }

    private static async Task<(string SiteName, string TimeZoneId)> ReadSiteSettingsAsync(string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "select Content from Document where Type like 'OrchardCore.Settings.SiteSettings%' limit 1;";

        var json = await command.ExecuteScalarAsync() as string;
        if (string.IsNullOrWhiteSpace(json))
        {
            return ("", "");
        }

        using var doc = JsonDocument.Parse(json);
        var siteName = doc.RootElement.TryGetProperty("SiteName", out var siteNameElement)
            ? siteNameElement.GetString() ?? ""
            : "";

        var timeZoneId = doc.RootElement.TryGetProperty("TimeZoneId", out var timeZoneElement)
            ? timeZoneElement.GetString() ?? ""
            : "";

        return (siteName, timeZoneId);
    }

    private static (int TenantCount, string DefaultTenantState) ReadTenantState(string appDataPath)
    {
        var tenantFile = Path.Combine(appDataPath, "tenants.json");
        if (!File.Exists(tenantFile))
        {
            return (0, "Unknown");
        }

        using var stream = File.OpenRead(tenantFile);
        using var doc = JsonDocument.Parse(stream);

        var count = 0;
        var defaultState = "Unknown";

        foreach (var tenantProperty in doc.RootElement.EnumerateObject())
        {
            count++;
            if (!tenantProperty.Name.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (tenantProperty.Value.TryGetProperty("State", out var state))
            {
                defaultState = state.GetString() ?? "Unknown";
            }
        }

        return (count, defaultState);
    }

    private static async Task<int> ReadTableCountAsync(string dbPath, string tableName)
    {
        try
        {
            await using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = $"select count(1) from [{tableName}]";

            var value = await command.ExecuteScalarAsync();
            return value switch
            {
                null => 0,
                int intValue => intValue,
                long longValue => unchecked((int)longValue),
                _ => Convert.ToInt32(value)
            };
        }
        catch (SqliteException exception)
            when (exception.SqliteErrorCode == 1 &&
                  exception.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
    }

    private static string NormalizeAdminBasePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim().TrimEnd('/');
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        return trimmed;
    }

    private static bool IsAdminPathAccessDisabled(IConfiguration configuration)
    {
        return configuration.GetValue("SaaS:DisableAdminPathAccess", true);
    }
}
