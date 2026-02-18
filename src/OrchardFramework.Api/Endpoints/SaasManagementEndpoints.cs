using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Localization.Models;
using OrchardCore.OpenId;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Roles;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Tenants;
using OrchardCore.Tenants.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using FeatureProfileModel = OrchardCore.Environment.Shell.Models.FeatureProfile;
using FeaturesModulePermissions = OrchardCore.Features.Permissions;
using IYesSqlSession = YesSql.ISession;
using OpenIdModulePermissions = OrchardCore.OpenId.Permissions;
using RolesPermissions = OrchardCore.Roles.CommonPermissions;
using SiteSettingsPermissions = OrchardCore.Settings.Permissions;
using TenantsPermissions = OrchardCore.Tenants.Permissions;
using UsersPermissions = OrchardCore.Users.CommonPermissions;

namespace OrchardFramework.Api.Endpoints;

public static class SaasManagementEndpoints
{
    private const string DefaultRecipeName = "SaaS.Base";
    private const string DefaultDatabaseProvider = "Sqlite";

    private sealed record TenantItem(
        string Name,
        string State,
        bool IsDefault,
        string RequestUrlHost,
        string RequestUrlPrefix,
        string Category,
        string Description,
        string RecipeName,
        string DatabaseProvider,
        string[] FeatureProfiles);

    private sealed record FeatureItem(
        string Id,
        string Name,
        string Category,
        string Description,
        bool Enabled,
        bool IsAlwaysEnabled,
        bool EnabledByDependencyOnly,
        bool DefaultTenantOnly,
        string[] Dependencies);

    private sealed record FeaturePayload(string Tenant, DateTime UpdatedAtUtc, FeatureItem[] Features);

    private sealed record FeatureProfileRuleItem(string Rule, string Expression);

    private sealed record FeatureProfileItem(string Id, string Name, FeatureProfileRuleItem[] FeatureRules, string[] AssignedTenants);

    private sealed record ManagementUserItem(
        string Id,
        string UserName,
        string Email,
        bool IsEnabled,
        bool EmailConfirmed,
        string[] RoleNames);

    private sealed record ManagementRoleItem(
        string Id,
        string Name,
        string[] PermissionNames);

    private sealed record ManagementPermissionItem(
        string Name,
        string Description,
        string Category);

    private sealed record SiteSettingsItem(
        string SiteName,
        string TimeZoneId,
        string Calendar,
        string BaseUrl,
        int PageSize,
        int MaxPageSize,
        int MaxPagedCount,
        bool UseCdn,
        string CdnBaseUrl,
        bool AppendVersion,
        string ResourceDebugMode,
        string CacheMode);

    private sealed record ManagementLocalizationItem(
        string DefaultCulture,
        string[] SupportedCultures);

    private sealed record ManagementOpenIdApplicationItem(
        string Id,
        string ClientId,
        string DisplayName,
        string ClientType,
        string ConsentType,
        string[] RedirectUris,
        string[] PostLogoutRedirectUris,
        string[] ScopeNames,
        string[] PermissionNames,
        string[] RoleNames,
        string[] Requirements);

    private sealed record ManagementOpenIdScopeItem(
        string Id,
        string Name,
        string DisplayName,
        string Description,
        string[] Resources);

    private sealed record ManagementRecipeItem(
        string Id,
        string Name,
        string DisplayName,
        string Description,
        string BasePath,
        string FileName,
        string Author,
        string Website,
        string Version,
        string[] Categories,
        string[] Tags);

    private sealed class CreateTenantRequest
    {
        [Required]
        public string Name { get; init; } = string.Empty;
        public string RequestUrlHost { get; init; } = string.Empty;
        public string RequestUrlPrefix { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string DatabaseProvider { get; init; } = DefaultDatabaseProvider;
        public string ConnectionString { get; init; } = string.Empty;
        public string TablePrefix { get; init; } = string.Empty;
        public string Schema { get; init; } = string.Empty;
        public string RecipeName { get; init; } = DefaultRecipeName;
        public string[] FeatureProfiles { get; init; } = [];
    }

    private sealed class PatchTenantRequest
    {
        public string? RequestUrlHost { get; init; }
        public string? RequestUrlPrefix { get; init; }
        public string? Category { get; init; }
        public string? Description { get; init; }
        public string[]? FeatureProfiles { get; init; }
        public bool? Enabled { get; init; }
        public string? Operation { get; init; }
    }

    private sealed class UpdateFeaturesRequest
    {
        public string? Tenant { get; init; }
        public string[] Enable { get; init; } = [];
        public string[] Disable { get; init; } = [];
        public bool Force { get; init; } = true;
    }

    private sealed class UpsertFeatureProfileRequest
    {
        [Required]
        public string Id { get; init; } = string.Empty;
        public string? Name { get; init; }
        public FeatureProfileRuleItem[] FeatureRules { get; init; } = [];
        public bool Delete { get; init; }
    }

    private sealed class CreateManagementUserRequest
    {
        public string? Tenant { get; init; }
        [Required]
        public string UserName { get; init; } = string.Empty;
        [Required]
        public string Email { get; init; } = string.Empty;
        [Required]
        public string Password { get; init; } = string.Empty;
        public bool IsEnabled { get; init; } = true;
        public string[] RoleNames { get; init; } = [];
    }

    private sealed class PatchManagementUserRequest
    {
        public string? Tenant { get; init; }
        public string? Email { get; init; }
        public string? Password { get; init; }
        public bool? IsEnabled { get; init; }
        public string[]? RoleNames { get; init; }
        public string? Operation { get; init; }
    }

    private sealed class CreateManagementRoleRequest
    {
        public string? Tenant { get; init; }
        [Required]
        public string Name { get; init; } = string.Empty;
        public string[] PermissionNames { get; init; } = [];
    }

    private sealed class PatchManagementRoleRequest
    {
        public string? Tenant { get; init; }
        public string? Name { get; init; }
        public string? Operation { get; init; }
    }

    private sealed class UpdateRolePermissionsRequest
    {
        public string? Tenant { get; init; }
        public string[] PermissionNames { get; init; } = [];
    }

    private sealed class UpdateSiteSettingsRequest
    {
        public string? Tenant { get; init; }
        public string? SiteName { get; init; }
        public string? TimeZoneId { get; init; }
        public string? Calendar { get; init; }
        public string? BaseUrl { get; init; }
        public int? PageSize { get; init; }
        public int? MaxPageSize { get; init; }
        public int? MaxPagedCount { get; init; }
        public bool? UseCdn { get; init; }
        public string? CdnBaseUrl { get; init; }
        public bool? AppendVersion { get; init; }
        public string? ResourceDebugMode { get; init; }
        public string? CacheMode { get; init; }
    }

    private sealed class UpdateLocalizationSettingsRequest
    {
        public string? Tenant { get; init; }
        public string? DefaultCulture { get; init; }
        public string[]? SupportedCultures { get; init; }
    }

    private sealed class CreateOpenIdApplicationRequest
    {
        public string? Tenant { get; init; }
        [Required]
        public string ClientId { get; init; } = string.Empty;
        [Required]
        public string DisplayName { get; init; } = string.Empty;
        public string ClientType { get; init; } = OpenIddictConstants.ClientTypes.Confidential;
        public string ConsentType { get; init; } = OpenIddictConstants.ConsentTypes.Explicit;
        public string? ClientSecret { get; init; }
        public string[] RedirectUris { get; init; } = [];
        public string[] PostLogoutRedirectUris { get; init; } = [];
        public string[] ScopeNames { get; init; } = [];
        public string[] PermissionNames { get; init; } = [];
        public string[] RoleNames { get; init; } = [];
        public string[] Requirements { get; init; } = [];
    }

    private sealed class PatchOpenIdApplicationRequest
    {
        public string? Tenant { get; init; }
        public string? ClientId { get; init; }
        public string? DisplayName { get; init; }
        public string? ClientType { get; init; }
        public string? ConsentType { get; init; }
        public string? ClientSecret { get; init; }
        public string[]? RedirectUris { get; init; }
        public string[]? PostLogoutRedirectUris { get; init; }
        public string[]? ScopeNames { get; init; }
        public string[]? PermissionNames { get; init; }
        public string[]? RoleNames { get; init; }
        public string[]? Requirements { get; init; }
        public string? Operation { get; init; }
    }

    private sealed class CreateOpenIdScopeRequest
    {
        public string? Tenant { get; init; }
        [Required]
        public string Name { get; init; } = string.Empty;
        [Required]
        public string DisplayName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string[] Resources { get; init; } = [];
    }

    private sealed class PatchOpenIdScopeRequest
    {
        public string? Tenant { get; init; }
        public string? Name { get; init; }
        public string? DisplayName { get; init; }
        public string? Description { get; init; }
        public string[]? Resources { get; init; }
        public string? Operation { get; init; }
    }

    private sealed class ExecuteRecipeRequest
    {
        public string? Tenant { get; init; }
        public string? RecipeId { get; init; }
        public string? RecipeName { get; init; }
        public string? FileName { get; init; }
        public Dictionary<string, string>? Environment { get; init; }
        public bool ReleaseShellContext { get; init; } = true;
    }

    public static IEndpointRouteBuilder MapSaasManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/management").WithTags("SaaS Management");

        group.MapGet("/tenants", async (
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                TenantsPermissions.ManageTenants);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            if (!currentShellSettings.IsDefaultShell())
            {
                return Results.Forbid();
            }

            var tenants = shellHost.GetAllSettings()
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToTenantItem)
                .ToArray();

            return Results.Ok(tenants);
        });

        group.MapPost("/tenants", async (
            [FromBody] CreateTenantRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] IShellSettingsManager shellSettingsManager,
            [FromServices] ShellSettings currentShellSettings) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                TenantsPermissions.ManageTenants);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            if (!currentShellSettings.IsDefaultShell())
            {
                return Results.Forbid();
            }

            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return Results.BadRequest(new { message = "Tenant name is required." });
            }

            if (shellHost.TryGetSettings(name, out _))
            {
                return Results.Conflict(new { message = $"Tenant '{name}' already exists." });
            }

            using var shellSettings = shellSettingsManager.CreateDefaultSettings().AsUninitialized().AsDisposable();

            shellSettings.Name = name;
            shellSettings.RequestUrlHost = request.RequestUrlHost.Trim();
            shellSettings.RequestUrlPrefix = NormalizePathSegment(request.RequestUrlPrefix);
            shellSettings["Category"] = request.Category.Trim();
            shellSettings["Description"] = request.Description.Trim();
            shellSettings["DatabaseProvider"] = string.IsNullOrWhiteSpace(request.DatabaseProvider)
                ? DefaultDatabaseProvider
                : request.DatabaseProvider.Trim();
            shellSettings["ConnectionString"] = request.ConnectionString.Trim();
            shellSettings["TablePrefix"] = request.TablePrefix.Trim();
            shellSettings["Schema"] = request.Schema.Trim();
            shellSettings["Secret"] = Guid.NewGuid().ToString("N");
            shellSettings["RecipeName"] = string.IsNullOrWhiteSpace(request.RecipeName)
                ? DefaultRecipeName
                : request.RecipeName.Trim();
            shellSettings["FeatureProfile"] = string.Join(',', NormalizeNames(request.FeatureProfiles));

            await shellHost.UpdateShellSettingsAsync(shellSettings);

            if (!shellHost.TryGetSettings(name, out var created))
            {
                created = shellSettings;
            }

            return Results.Created($"/api/management/tenants/{Uri.EscapeDataString(name)}", ToTenantItem(created));
        });

        group.MapPatch("/tenants/{tenantName}", async (
            [FromRoute] string tenantName,
            [FromBody] PatchTenantRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                TenantsPermissions.ManageTenants);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            if (!currentShellSettings.IsDefaultShell())
            {
                return Results.Forbid();
            }

            if (!shellHost.TryGetSettings(tenantName, out var shellSettings))
            {
                return Results.NotFound(new { message = $"Tenant '{tenantName}' was not found." });
            }

            var operation = request.Operation?.Trim();
            if (string.Equals(operation, "remove", StringComparison.OrdinalIgnoreCase))
            {
                if (shellSettings.IsDefaultShell())
                {
                    return Results.BadRequest(new { message = "Default tenant cannot be removed." });
                }

                if (!shellSettings.IsRemovable())
                {
                    return Results.BadRequest(new { message = "Only disabled or uninitialized tenants can be removed." });
                }

                await shellHost.RemoveShellSettingsAsync(shellSettings);
                return Results.Ok(new { removed = tenantName });
            }

            var changed = false;

            if (request.RequestUrlHost is not null)
            {
                shellSettings.RequestUrlHost = request.RequestUrlHost.Trim();
                changed = true;
            }

            if (request.RequestUrlPrefix is not null)
            {
                shellSettings.RequestUrlPrefix = NormalizePathSegment(request.RequestUrlPrefix);
                changed = true;
            }

            if (request.Category is not null)
            {
                shellSettings["Category"] = request.Category.Trim();
                changed = true;
            }

            if (request.Description is not null)
            {
                shellSettings["Description"] = request.Description.Trim();
                changed = true;
            }

            if (request.FeatureProfiles is not null)
            {
                shellSettings["FeatureProfile"] = string.Join(',', NormalizeNames(request.FeatureProfiles));
                changed = true;
            }

            if (request.Enabled.HasValue)
            {
                if (request.Enabled.Value && shellSettings.IsDisabled())
                {
                    shellSettings.AsRunning();
                    changed = true;
                }
                else if (!request.Enabled.Value && shellSettings.IsRunning())
                {
                    shellSettings.AsDisabled();
                    changed = true;
                }
                else if (request.Enabled.Value && shellSettings.IsUninitialized())
                {
                    return Results.BadRequest(new { message = "Uninitialized tenant must be setup before enabling." });
                }
            }

            if (!changed)
            {
                return Results.Ok(ToTenantItem(shellSettings));
            }

            await shellHost.UpdateShellSettingsAsync(shellSettings);

            if (!shellHost.TryGetSettings(tenantName, out var updated))
            {
                updated = shellSettings;
            }

            return Results.Ok(ToTenantItem(updated));
        });

        group.MapGet("/features", async (
            [FromQuery] string? tenant,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                FeaturesModulePermissions.ManageFeatures);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, shellSettings) =>
                {
                    var shellFeaturesManager = serviceProvider.GetRequiredService<IShellFeaturesManager>();
                    var payload = await BuildFeaturePayloadAsync(shellFeaturesManager, shellSettings.Name);
                    return (IResult)Results.Ok(payload);
                });
        });

        group.MapPut("/features", async (
            [FromBody] UpdateFeaturesRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                FeaturesModulePermissions.ManageFeatures);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, shellSettings) =>
                {
                    var shellFeaturesManager = serviceProvider.GetRequiredService<IShellFeaturesManager>();
                    var available = (await shellFeaturesManager.GetAvailableFeaturesAsync())
                        .Where(x => !x.IsTheme())
                        .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

                    var enableIds = NormalizeNames(request.Enable);
                    var disableIds = NormalizeNames(request.Disable);
                    var changedIds = enableIds.Concat(disableIds).ToArray();

                    if (changedIds.Length > 0)
                    {
                        var unknown = changedIds.Where(id => !available.ContainsKey(id)).ToArray();
                        if (unknown.Length > 0)
                        {
                            return (IResult)Results.BadRequest(new
                            {
                                message = "Unknown feature ids detected.",
                                unknown
                            });
                        }
                    }

                    var alwaysEnabled = (await shellFeaturesManager.GetAlwaysEnabledFeaturesAsync())
                        .Select(x => x.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var blocked = disableIds.Where(alwaysEnabled.Contains).ToArray();
                    if (blocked.Length > 0)
                    {
                        return (IResult)Results.BadRequest(new
                        {
                            message = "Always enabled features cannot be disabled.",
                            blocked
                        });
                    }

                    var featuresToEnable = enableIds.Select(id => available[id]);
                    var featuresToDisable = disableIds.Select(id => available[id]);
                    var (disabledFeatures, enabledFeatures) = await shellFeaturesManager.UpdateFeaturesAsync(
                        featuresToDisable,
                        featuresToEnable,
                        request.Force);

                    var payload = await BuildFeaturePayloadAsync(shellFeaturesManager, shellSettings.Name);

                    return (IResult)Results.Ok(new
                    {
                        payload.Tenant,
                        payload.UpdatedAtUtc,
                        Changed = new
                        {
                            Enabled = enabledFeatures.Select(x => x.Id).OrderBy(x => x).ToArray(),
                            Disabled = disabledFeatures.Select(x => x.Id).OrderBy(x => x).ToArray()
                        },
                        payload.Features,
                        AppliedTenant = shellSettings.Name
                    });
                });
        });

        group.MapGet("/feature-profiles", async (
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IFeatureProfilesService featureProfilesService,
            [FromServices] IShellHost shellHost) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                TenantsPermissions.ManageTenantFeatureProfiles);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            var profiles = await featureProfilesService.GetFeatureProfilesAsync();
            var assignedMap = shellHost.GetAllSettings()
                .SelectMany(settings => settings.GetFeatureProfiles().Select(profileId => new
                {
                    ProfileId = profileId,
                    settings.Name
                }))
                .GroupBy(x => x.ProfileId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    groupItem => groupItem.Key,
                    groupItem => groupItem.Select(x => x.Name).OrderBy(x => x).ToArray(),
                    StringComparer.OrdinalIgnoreCase);

            var payload = profiles
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x =>
                {
                    assignedMap.TryGetValue(x.Key, out var assignedTenants);
                    return ToFeatureProfileItem(x.Key, x.Value, assignedTenants ?? []);
                })
                .ToArray();

            return Results.Ok(payload);
        });

        group.MapPut("/feature-profiles", async (
            [FromBody] UpsertFeatureProfileRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] FeatureProfilesManager featureProfilesManager,
            [FromServices] IShellHost shellHost) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                TenantsPermissions.ManageTenantFeatureProfiles);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            var id = request.Id.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                return Results.BadRequest(new { message = "Profile id is required." });
            }

            if (request.Delete)
            {
                await featureProfilesManager.RemoveFeatureProfileAsync(id);
                return Results.Ok(new { removed = id });
            }

            var invalidRules = request.FeatureRules
                .Where(x => string.IsNullOrWhiteSpace(x.Rule) || string.IsNullOrWhiteSpace(x.Expression))
                .ToArray();
            if (invalidRules.Length > 0)
            {
                return Results.BadRequest(new { message = "Feature rule and expression are both required." });
            }

            var profile = new FeatureProfileModel
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(request.Name) ? id : request.Name.Trim(),
                FeatureRules = request.FeatureRules.Select(x => new FeatureRule
                {
                    Rule = x.Rule.Trim(),
                    Expression = x.Expression.Trim()
                }).ToList()
            };

            await featureProfilesManager.UpdateFeatureProfileAsync(id, profile);

            var assignedTenants = shellHost.GetAllSettings()
                .Where(settings => settings.GetFeatureProfiles().Contains(id, StringComparer.OrdinalIgnoreCase))
                .Select(settings => settings.Name)
                .OrderBy(x => x)
                .ToArray();

            return Results.Ok(ToFeatureProfileItem(id, profile, assignedTenants));
        });

        group.MapGet("/users", async (
            [FromQuery] string? tenant,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                UsersPermissions.ManageUsers);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var userManager = serviceProvider.GetRequiredService<UserManager<IUser>>();
                    var session = serviceProvider.GetRequiredService<IYesSqlSession>();
                    var users = (await session.Query(collection: null!).For<User>(filterType: true).ListAsync()).ToList();
                    users.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.UserName, b.UserName));
                    var payload = new List<ManagementUserItem>(users.Count);

                    foreach (var user in users)
                    {
                        payload.Add(await ToManagementUserItemAsync(userManager, user));
                    }

                    return Results.Ok(payload);
                });
        });

        group.MapPost("/users", async (
            [FromBody] CreateManagementUserRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                UsersPermissions.ManageUsers);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var userName = request.UserName.Trim();
                    var email = request.Email.Trim();
                    var password = request.Password.Trim();
                    if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    {
                        return Results.BadRequest(new { message = "UserName, Email and Password are required." });
                    }

                    var userManager = serviceProvider.GetRequiredService<UserManager<IUser>>();
                    var roleManager = serviceProvider.GetRequiredService<RoleManager<IRole>>();

                    if (await userManager.FindByNameAsync(userName) is not null)
                    {
                        return Results.Conflict(new { message = $"User '{userName}' already exists." });
                    }

                    var user = new User
                    {
                        UserName = userName,
                        Email = email,
                        IsEnabled = request.IsEnabled,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(user, password);
                    if (!createResult.Succeeded)
                    {
                        return Results.BadRequest(new
                        {
                            message = "Failed to create user.",
                            errors = ToIdentityErrors(createResult)
                        });
                    }

                    var roleNames = NormalizeNames(request.RoleNames);
                    if (roleNames.Length > 0)
                    {
                        var unknownRoles = await GetUnknownRoleNamesAsync(roleManager, roleNames);
                        if (unknownRoles.Length > 0)
                        {
                            await userManager.DeleteAsync(user);
                            return Results.BadRequest(new
                            {
                                message = "Unknown role names detected.",
                                unknownRoles
                            });
                        }

                        var addRolesResult = await userManager.AddToRolesAsync(user, roleNames);
                        if (!addRolesResult.Succeeded)
                        {
                            await userManager.DeleteAsync(user);
                            return Results.BadRequest(new
                            {
                                message = "Failed to assign roles to user.",
                                errors = ToIdentityErrors(addRolesResult)
                            });
                        }
                    }

                    var payload = await ToManagementUserItemAsync(userManager, user);
                    return Results.Created($"/api/management/users/{Uri.EscapeDataString(payload.Id)}", payload);
                });
        });

        group.MapPatch("/users/{id}", async (
            [FromRoute] string id,
            [FromBody] PatchManagementUserRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                UsersPermissions.ManageUsers);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var userManager = serviceProvider.GetRequiredService<UserManager<IUser>>();
                    var roleManager = serviceProvider.GetRequiredService<RoleManager<IRole>>();
                    var user = await FindUserAsync(userManager, id);

                    if (user is null)
                    {
                        return Results.NotFound(new { message = $"User '{id}' was not found." });
                    }

                    if (string.Equals(request.Operation?.Trim(), "remove", StringComparison.OrdinalIgnoreCase))
                    {
                        var deleteResult = await userManager.DeleteAsync(user);
                        if (!deleteResult.Succeeded)
                        {
                            return Results.BadRequest(new
                            {
                                message = "Failed to remove user.",
                                errors = ToIdentityErrors(deleteResult)
                            });
                        }

                        var deletedId = await userManager.GetUserIdAsync(user) ?? id;
                        return Results.Ok(new { removed = deletedId });
                    }

                    var shouldUpdateUser = false;

                    if (request.Email is not null)
                    {
                        var updateEmailResult = await userManager.SetEmailAsync(user, request.Email.Trim());
                        if (!updateEmailResult.Succeeded)
                        {
                            return Results.BadRequest(new
                            {
                                message = "Failed to update user email.",
                                errors = ToIdentityErrors(updateEmailResult)
                            });
                        }
                    }

                    if (request.IsEnabled.HasValue && SetBooleanProperty(user, nameof(User.IsEnabled), request.IsEnabled.Value))
                    {
                        shouldUpdateUser = true;
                    }

                    if (!string.IsNullOrWhiteSpace(request.Password))
                    {
                        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                        var resetResult = await userManager.ResetPasswordAsync(user, resetToken, request.Password.Trim());
                        if (!resetResult.Succeeded)
                        {
                            return Results.BadRequest(new
                            {
                                message = "Failed to reset user password.",
                                errors = ToIdentityErrors(resetResult)
                            });
                        }
                    }

                    if (request.RoleNames is not null)
                    {
                        var targetRoles = NormalizeNames(request.RoleNames);
                        var unknownRoles = await GetUnknownRoleNamesAsync(roleManager, targetRoles);
                        if (unknownRoles.Length > 0)
                        {
                            return Results.BadRequest(new
                            {
                                message = "Unknown role names detected.",
                                unknownRoles
                            });
                        }

                        var currentRoles = (await userManager.GetRolesAsync(user))
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToArray();

                        var rolesToRemove = currentRoles
                            .Where(x => !targetRoles.Contains(x, StringComparer.OrdinalIgnoreCase))
                            .ToArray();
                        if (rolesToRemove.Length > 0)
                        {
                            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
                            if (!removeResult.Succeeded)
                            {
                                return Results.BadRequest(new
                                {
                                    message = "Failed to remove user roles.",
                                    errors = ToIdentityErrors(removeResult)
                                });
                            }
                        }

                        var rolesToAdd = targetRoles
                            .Where(x => !currentRoles.Contains(x, StringComparer.OrdinalIgnoreCase))
                            .ToArray();
                        if (rolesToAdd.Length > 0)
                        {
                            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
                            if (!addResult.Succeeded)
                            {
                                return Results.BadRequest(new
                                {
                                    message = "Failed to add user roles.",
                                    errors = ToIdentityErrors(addResult)
                                });
                            }
                        }
                    }

                    if (shouldUpdateUser)
                    {
                        var updateResult = await userManager.UpdateAsync(user);
                        if (!updateResult.Succeeded)
                        {
                            return Results.BadRequest(new
                            {
                                message = "Failed to update user.",
                                errors = ToIdentityErrors(updateResult)
                            });
                        }
                    }

                    return Results.Ok(await ToManagementUserItemAsync(userManager, user));
                });
        });

        group.MapGet("/roles", async (
            [FromQuery] string? tenant,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                RolesPermissions.ManageRoles);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var roleManager = serviceProvider.GetRequiredService<RoleManager<IRole>>();
                    var roles = roleManager.Roles
                        .ToList();
                    roles.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(GetRoleName(a), GetRoleName(b)));
                    var payload = new List<ManagementRoleItem>(roles.Count);

                    foreach (var role in roles)
                    {
                        payload.Add(await ToManagementRoleItemAsync(roleManager, role));
                    }

                    return Results.Ok(payload);
                });
        });

        group.MapPost("/roles", async (
            [FromBody] CreateManagementRoleRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                RolesPermissions.ManageRoles);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var roleName = request.Name.Trim();
                    if (string.IsNullOrWhiteSpace(roleName))
                    {
                        return Results.BadRequest(new { message = "Role name is required." });
                    }

                    var roleManager = serviceProvider.GetRequiredService<RoleManager<IRole>>();
                    if (await roleManager.FindByNameAsync(roleName) is not null)
                    {
                        return Results.Conflict(new { message = $"Role '{roleName}' already exists." });
                    }

                    var role = new Role { RoleName = roleName };
                    var createResult = await roleManager.CreateAsync(role);
                    if (!createResult.Succeeded)
                    {
                        return Results.BadRequest(new
                        {
                            message = "Failed to create role.",
                            errors = ToIdentityErrors(createResult)
                        });
                    }

                    var updatePermissionsResult = await UpdateRolePermissionsAsync(
                        serviceProvider,
                        roleManager,
                        role,
                        request.PermissionNames);
                    if (updatePermissionsResult is not null)
                    {
                        await roleManager.DeleteAsync(role);
                        return updatePermissionsResult;
                    }

                    var payload = await ToManagementRoleItemAsync(roleManager, role);
                    return Results.Created($"/api/management/roles/{Uri.EscapeDataString(payload.Id)}", payload);
                });
        });

        group.MapPatch("/roles/{id}", async (
            [FromRoute] string id,
            [FromBody] PatchManagementRoleRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                RolesPermissions.ManageRoles);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var roleManager = serviceProvider.GetRequiredService<RoleManager<IRole>>();
                    var role = await FindRoleAsync(roleManager, id);
                    if (role is null)
                    {
                        return Results.NotFound(new { message = $"Role '{id}' was not found." });
                    }

                    if (string.Equals(request.Operation?.Trim(), "remove", StringComparison.OrdinalIgnoreCase))
                    {
                        var deleteResult = await roleManager.DeleteAsync(role);
                        if (!deleteResult.Succeeded)
                        {
                            return Results.BadRequest(new
                            {
                                message = "Failed to remove role.",
                                errors = ToIdentityErrors(deleteResult)
                            });
                        }

                        var deletedId = await roleManager.GetRoleIdAsync(role) ?? id;
                        return Results.Ok(new { removed = deletedId });
                    }

                    var shouldUpdate = false;
                    if (request.Name is not null)
                    {
                        var roleName = request.Name.Trim();
                        if (string.IsNullOrWhiteSpace(roleName))
                        {
                            return Results.BadRequest(new { message = "Role name cannot be empty." });
                        }

                        var currentRoleName = GetRoleName(role);
                        if (!roleName.Equals(currentRoleName, StringComparison.OrdinalIgnoreCase))
                        {
                            var duplicate = await roleManager.FindByNameAsync(roleName);
                            if (duplicate is not null)
                            {
                                var duplicateId = await roleManager.GetRoleIdAsync(duplicate);
                                var currentId = await roleManager.GetRoleIdAsync(role);
                                if (!string.Equals(duplicateId, currentId, StringComparison.OrdinalIgnoreCase))
                                {
                                    return Results.Conflict(new { message = $"Role '{roleName}' already exists." });
                                }
                            }

                            var setNameResult = await roleManager.SetRoleNameAsync(role, roleName);
                            if (!setNameResult.Succeeded)
                            {
                                return Results.BadRequest(new
                                {
                                    message = "Failed to set role name.",
                                    errors = ToIdentityErrors(setNameResult)
                                });
                            }

                            shouldUpdate = true;
                        }
                    }

                    if (shouldUpdate)
                    {
                        var updateResult = await roleManager.UpdateAsync(role);
                        if (!updateResult.Succeeded)
                        {
                            return Results.BadRequest(new
                            {
                                message = "Failed to update role.",
                                errors = ToIdentityErrors(updateResult)
                            });
                        }
                    }

                    return Results.Ok(await ToManagementRoleItemAsync(roleManager, role));
                });
        });

        group.MapPut("/roles/{id}/permissions", async (
            [FromRoute] string id,
            [FromBody] UpdateRolePermissionsRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                RolesPermissions.ManageRoles);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var roleManager = serviceProvider.GetRequiredService<RoleManager<IRole>>();
                    var role = await FindRoleAsync(roleManager, id);
                    if (role is null)
                    {
                        return Results.NotFound(new { message = $"Role '{id}' was not found." });
                    }

                    var updateResult = await UpdateRolePermissionsAsync(
                        serviceProvider,
                        roleManager,
                        role,
                        request.PermissionNames);
                    if (updateResult is not null)
                    {
                        return updateResult;
                    }

                    return Results.Ok(await ToManagementRoleItemAsync(roleManager, role));
                });
        });

        group.MapGet("/permissions", async (
            [FromQuery] string? tenant,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                RolesPermissions.ManageRoles);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var permissions = await LoadPermissionItemsAsync(serviceProvider);
                    return Results.Ok(permissions);
                });
        });

        group.MapGet("/site-settings", async (
            [FromQuery] string? tenant,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                SiteSettingsPermissions.ManageSettings);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var siteService = serviceProvider.GetRequiredService<ISiteService>();
                    var site = await siteService.GetSiteSettingsAsync();
                    return Results.Ok(ToSiteSettingsItem(site));
                });
        });

        group.MapPut("/site-settings", async (
            [FromBody] UpdateSiteSettingsRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                SiteSettingsPermissions.ManageSettings);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var siteService = serviceProvider.GetRequiredService<ISiteService>();
                    var site = await siteService.LoadSiteSettingsAsync();
                    var patchError = ApplySiteSettingsPatch(site, request);
                    if (patchError is not null)
                    {
                        return patchError;
                    }

                    await siteService.UpdateSiteSettingsAsync(site);
                    return Results.Ok(ToSiteSettingsItem(site));
                });
        });

        group.MapGet("/localization", async (
            [FromQuery] string? tenant,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                SiteSettingsPermissions.ManageSettings);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var siteService = serviceProvider.GetRequiredService<ISiteService>();
                    var site = await siteService.GetSiteSettingsAsync();
                    return Results.Ok(ToManagementLocalizationItem(site.As<LocalizationSettings>()));
                });
        });

        group.MapPut("/localization", async (
            [FromBody] UpdateLocalizationSettingsRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                SiteSettingsPermissions.ManageSettings);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var siteService = serviceProvider.GetRequiredService<ISiteService>();
                    var site = await siteService.LoadSiteSettingsAsync();
                    var patchError = ApplyLocalizationPatch(site, request);
                    if (patchError is not null)
                    {
                        return patchError;
                    }

                    await siteService.UpdateSiteSettingsAsync(site);
                    return Results.Ok(ToManagementLocalizationItem(site.As<LocalizationSettings>()));
                });
        });

        group.MapGet("/openid/applications", async (
            [FromQuery] string? tenant,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                OpenIdModulePermissions.ManageApplications);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var applicationManager = serviceProvider.GetService<IOpenIdApplicationManager>();
                    if (applicationManager is null)
                    {
                        return (IResult)Results.BadRequest(new { message = "OpenId application management feature is not enabled." });
                    }

                    var payload = new List<ManagementOpenIdApplicationItem>();
                    await foreach (var application in applicationManager.ListAsync())
                    {
                        payload.Add(await ToManagementOpenIdApplicationItemAsync(applicationManager, application));
                    }

                    payload.Sort((a, b) =>
                    {
                        var byDisplay = StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName);
                        return byDisplay != 0 ? byDisplay : StringComparer.OrdinalIgnoreCase.Compare(a.ClientId, b.ClientId);
                    });

                    return (IResult)Results.Ok(payload);
                });
        });

        group.MapPost("/openid/applications", async (
            [FromBody] CreateOpenIdApplicationRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                OpenIdModulePermissions.ManageApplications);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var applicationManager = serviceProvider.GetService<IOpenIdApplicationManager>();
                    if (applicationManager is null)
                    {
                        return (IResult)Results.BadRequest(new { message = "OpenId application management feature is not enabled." });
                    }

                    var descriptorOrError = CreateOpenIdApplicationDescriptor(request);
                    if (descriptorOrError.Error is not null)
                    {
                        return descriptorOrError.Error;
                    }

                    if (await applicationManager.FindByClientIdAsync(descriptorOrError.Descriptor.ClientId!) is not null)
                    {
                        return (IResult)Results.Conflict(new { message = $"OpenId client '{descriptorOrError.Descriptor.ClientId}' already exists." });
                    }

                    await applicationManager.CreateAsync(descriptorOrError.Descriptor);
                    var created = await applicationManager.FindByClientIdAsync(descriptorOrError.Descriptor.ClientId!);
                    if (created is null)
                    {
                        return (IResult)Results.Problem("OpenId client created but could not be loaded.");
                    }

                    var payload = await ToManagementOpenIdApplicationItemAsync(applicationManager, created);
                    return (IResult)Results.Created($"/api/management/openid/applications/{Uri.EscapeDataString(payload.Id)}", payload);
                });
        });

        group.MapPatch("/openid/applications/{id}", async (
            [FromRoute] string id,
            [FromBody] PatchOpenIdApplicationRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                OpenIdModulePermissions.ManageApplications);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var applicationManager = serviceProvider.GetService<IOpenIdApplicationManager>();
                    if (applicationManager is null)
                    {
                        return (IResult)Results.BadRequest(new { message = "OpenId application management feature is not enabled." });
                    }

                    var normalizedId = id.Trim();
                    if (string.IsNullOrWhiteSpace(normalizedId))
                    {
                        return (IResult)Results.BadRequest(new { message = "Application id is required." });
                    }

                    var application = await applicationManager.FindByPhysicalIdAsync(normalizedId)
                        ?? await applicationManager.FindByIdAsync(normalizedId);
                    if (application is null)
                    {
                        return (IResult)Results.NotFound(new { message = $"OpenId application '{id}' was not found." });
                    }

                    if (string.Equals(request.Operation?.Trim(), "remove", StringComparison.OrdinalIgnoreCase))
                    {
                        await applicationManager.DeleteAsync(application);
                        return (IResult)Results.Ok(new { removed = normalizedId });
                    }

                    var descriptor = new OpenIdApplicationDescriptor();
                    await applicationManager.PopulateAsync(descriptor, application);
                    var patchError = ApplyOpenIdApplicationPatch(descriptor, request);
                    if (patchError is not null)
                    {
                        return patchError;
                    }

                    if (!string.Equals(descriptor.ClientId, await applicationManager.GetClientIdAsync(application), StringComparison.OrdinalIgnoreCase))
                    {
                        var other = await applicationManager.FindByClientIdAsync(descriptor.ClientId!);
                        if (other is not null)
                        {
                            var otherId = await applicationManager.GetIdAsync(other);
                            var currentId = await applicationManager.GetIdAsync(application);
                            if (!string.Equals(otherId, currentId, StringComparison.Ordinal))
                            {
                                return (IResult)Results.Conflict(new { message = $"OpenId client '{descriptor.ClientId}' already exists." });
                            }
                        }
                    }

                    await applicationManager.UpdateAsync(application, descriptor);
                    return (IResult)Results.Ok(await ToManagementOpenIdApplicationItemAsync(applicationManager, application));
                });
        });

        group.MapGet("/openid/scopes", async (
            [FromQuery] string? tenant,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                OpenIdModulePermissions.ManageScopes);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var scopeManager = serviceProvider.GetService<IOpenIdScopeManager>();
                    if (scopeManager is null)
                    {
                        return (IResult)Results.BadRequest(new { message = "OpenId scope management feature is not enabled." });
                    }

                    var payload = new List<ManagementOpenIdScopeItem>();
                    await foreach (var scope in scopeManager.ListAsync())
                    {
                        payload.Add(await ToManagementOpenIdScopeItemAsync(scopeManager, scope));
                    }

                    payload.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
                    return (IResult)Results.Ok(payload);
                });
        });

        group.MapPost("/openid/scopes", async (
            [FromBody] CreateOpenIdScopeRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                OpenIdModulePermissions.ManageScopes);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, tenantShellSettings) =>
                {
                    var scopeManager = serviceProvider.GetService<IOpenIdScopeManager>();
                    if (scopeManager is null)
                    {
                        return (IResult)Results.BadRequest(new { message = "OpenId scope management feature is not enabled." });
                    }

                    var name = request.Name.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        return (IResult)Results.BadRequest(new { message = "Scope name is required." });
                    }

                    if (await scopeManager.FindByNameAsync(name) is not null)
                    {
                        return (IResult)Results.Conflict(new { message = $"OpenId scope '{name}' already exists." });
                    }

                    var descriptorOrError = CreateOpenIdScopeDescriptor(request.DisplayName, name, request.Description, request.Resources, tenantShellSettings.Name);
                    if (descriptorOrError.Error is not null)
                    {
                        return descriptorOrError.Error;
                    }

                    await scopeManager.CreateAsync(descriptorOrError.Descriptor);
                    var created = await scopeManager.FindByNameAsync(name);
                    if (created is null)
                    {
                        return (IResult)Results.Problem("OpenId scope created but could not be loaded.");
                    }

                    var payload = await ToManagementOpenIdScopeItemAsync(scopeManager, created);
                    return (IResult)Results.Created($"/api/management/openid/scopes/{Uri.EscapeDataString(payload.Id)}", payload);
                });
        });

        group.MapPatch("/openid/scopes/{id}", async (
            [FromRoute] string id,
            [FromBody] PatchOpenIdScopeRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                OpenIdModulePermissions.ManageScopes);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, tenantShellSettings) =>
                {
                    var scopeManager = serviceProvider.GetService<IOpenIdScopeManager>();
                    if (scopeManager is null)
                    {
                        return (IResult)Results.BadRequest(new { message = "OpenId scope management feature is not enabled." });
                    }

                    var normalizedId = id.Trim();
                    if (string.IsNullOrWhiteSpace(normalizedId))
                    {
                        return (IResult)Results.BadRequest(new { message = "Scope id is required." });
                    }

                    var scope = await scopeManager.FindByPhysicalIdAsync(normalizedId)
                        ?? await scopeManager.FindByIdAsync(normalizedId)
                        ?? await scopeManager.FindByNameAsync(normalizedId);
                    if (scope is null)
                    {
                        return (IResult)Results.NotFound(new { message = $"OpenId scope '{id}' was not found." });
                    }

                    if (string.Equals(request.Operation?.Trim(), "remove", StringComparison.OrdinalIgnoreCase))
                    {
                        await scopeManager.DeleteAsync(scope);
                        return (IResult)Results.Ok(new { removed = normalizedId });
                    }

                    var descriptor = new OpenIdScopeDescriptor();
                    await scopeManager.PopulateAsync(descriptor, scope);

                    var patchError = ApplyOpenIdScopePatch(descriptor, request, tenantShellSettings.Name);
                    if (patchError is not null)
                    {
                        return patchError;
                    }

                    if (!string.Equals(descriptor.Name, await scopeManager.GetNameAsync(scope), StringComparison.OrdinalIgnoreCase))
                    {
                        var other = await scopeManager.FindByNameAsync(descriptor.Name!);
                        if (other is not null)
                        {
                            var otherId = await scopeManager.GetIdAsync(other);
                            var currentId = await scopeManager.GetIdAsync(scope);
                            if (!string.Equals(otherId, currentId, StringComparison.Ordinal))
                            {
                                return (IResult)Results.Conflict(new { message = $"OpenId scope '{descriptor.Name}' already exists." });
                            }
                        }
                    }

                    await scopeManager.UpdateAsync(scope, descriptor);
                    return (IResult)Results.Ok(await ToManagementOpenIdScopeItemAsync(scopeManager, scope));
                });
        });

        group.MapGet("/recipes", async (
            [FromQuery] string? tenant,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                RecipePermissions.ManageRecipes);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, _) =>
                {
                    var recipes = await LoadRecipesAsync(serviceProvider);
                    return (IResult)Results.Ok(recipes);
                });
        });

        group.MapPost("/recipes/execute", async (
            [FromBody] ExecuteRecipeRequest request,
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IAuthorizationService authorizationService,
            [FromServices] IShellHost shellHost,
            [FromServices] ShellSettings currentShellSettings,
            [FromServices] IServiceProvider services) =>
        {
            var permissionResult = await EnsureManagementAccessAsync(
                httpContext,
                configuration,
                authorizationService,
                RecipePermissions.ManageRecipes);
            if (permissionResult is not null)
            {
                return permissionResult;
            }

            return await ExecuteWithTenantAsync(
                request.Tenant,
                currentShellSettings,
                shellHost,
                services,
                async (serviceProvider, tenantShellSettings) =>
                {
                    var recipes = await LoadRecipeDescriptorsAsync(serviceProvider);
                    var matchResult = MatchRecipeDescriptor(
                        recipes,
                        request.RecipeId,
                        request.RecipeName,
                        request.FileName);
                    if (matchResult.Error is not null)
                    {
                        return matchResult.Error;
                    }

                    var recipeExecutor = serviceProvider.GetService<IRecipeExecutor>();
                    if (recipeExecutor is null)
                    {
                        return (IResult)Results.BadRequest(new { message = "Recipe execution feature is not enabled." });
                    }

                    var environmentProviders = serviceProvider.GetServices<IRecipeEnvironmentProvider>()
                        .OrderBy(x => x.Order)
                        .ToArray();
                    var environment = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                    foreach (var provider in environmentProviders)
                    {
                        await provider.PopulateEnvironmentAsync(environment);
                    }

                    if (request.Environment is not null)
                    {
                        foreach (var pair in request.Environment)
                        {
                            var key = pair.Key?.Trim();
                            if (string.IsNullOrWhiteSpace(key))
                            {
                                continue;
                            }

                            environment[key] = pair.Value ?? string.Empty;
                        }
                    }

                    var descriptor = matchResult.Recipe!;
                    var executionId = Guid.NewGuid().ToString("n");

                    try
                    {
                        var result = await recipeExecutor.ExecuteAsync(
                            executionId,
                            descriptor,
                            environment,
                            httpContext.RequestAborted);

                        if (request.ReleaseShellContext)
                        {
                            await shellHost.ReleaseShellContextAsync(tenantShellSettings);
                        }

                        return (IResult)Results.Ok(new
                        {
                            executionId,
                            result,
                            tenant = tenantShellSettings.Name,
                            recipeId = BuildRecipeId(descriptor),
                            recipeName = descriptor.Name ?? string.Empty,
                            displayName = string.IsNullOrWhiteSpace(descriptor.DisplayName) ? descriptor.Name ?? string.Empty : descriptor.DisplayName,
                            basePath = descriptor.BasePath ?? string.Empty,
                            fileName = descriptor.RecipeFileInfo?.Name ?? string.Empty,
                            releasedShellContext = request.ReleaseShellContext
                        });
                    }
                    catch (RecipeExecutionException exception)
                    {
                        return (IResult)Results.BadRequest(new
                        {
                            message = "Recipe execution failed.",
                            step = exception.StepResult.StepName,
                            errors = exception.StepResult.Errors
                        });
                    }
                });
        });

        return app;
    }

    private static TenantItem ToTenantItem(ShellSettings shellSettings)
    {
        return new TenantItem(
            Name: shellSettings.Name,
            State: shellSettings.State.ToString(),
            IsDefault: shellSettings.IsDefaultShell(),
            RequestUrlHost: shellSettings.RequestUrlHost,
            RequestUrlPrefix: shellSettings.RequestUrlPrefix,
            Category: shellSettings["Category"] ?? string.Empty,
            Description: shellSettings["Description"] ?? string.Empty,
            RecipeName: shellSettings["RecipeName"] ?? string.Empty,
            DatabaseProvider: shellSettings["DatabaseProvider"] ?? string.Empty,
            FeatureProfiles: shellSettings.GetFeatureProfiles());
    }

    private static async Task<FeaturePayload> BuildFeaturePayloadAsync(
        IShellFeaturesManager shellFeaturesManager,
        string tenantName)
    {
        var enabled = (await shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(x => x.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var alwaysEnabled = (await shellFeaturesManager.GetAlwaysEnabledFeaturesAsync())
            .Select(x => x.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var features = (await shellFeaturesManager.GetAvailableFeaturesAsync())
            .Where(x => !x.IsTheme())
            .OrderBy(x => x.Category ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name ?? x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => new FeatureItem(
                Id: x.Id,
                Name: x.Name ?? x.Id,
                Category: x.Category ?? string.Empty,
                Description: x.Description ?? string.Empty,
                Enabled: enabled.Contains(x.Id),
                IsAlwaysEnabled: alwaysEnabled.Contains(x.Id),
                EnabledByDependencyOnly: x.EnabledByDependencyOnly,
                DefaultTenantOnly: x.DefaultTenantOnly,
                Dependencies: x.Dependencies ?? []))
            .ToArray();

        return new FeaturePayload(
            Tenant: tenantName,
            UpdatedAtUtc: DateTime.UtcNow,
            Features: features);
    }

    private static FeatureProfileItem ToFeatureProfileItem(string key, FeatureProfileModel profile, string[] assignedTenants)
    {
        return new FeatureProfileItem(
            Id: string.IsNullOrWhiteSpace(profile.Id) ? key : profile.Id,
            Name: string.IsNullOrWhiteSpace(profile.Name) ? key : profile.Name,
            FeatureRules: (profile.FeatureRules ?? [])
                .Select(x => new FeatureProfileRuleItem(
                    Rule: x.Rule ?? string.Empty,
                    Expression: x.Expression ?? string.Empty))
                .ToArray(),
            AssignedTenants: assignedTenants);
    }

    private static async Task<ManagementUserItem> ToManagementUserItemAsync(UserManager<IUser> userManager, IUser user)
    {
        var id = await userManager.GetUserIdAsync(user) ?? user.UserName;
        var email = await userManager.GetEmailAsync(user) ?? string.Empty;
        var roleNames = (await userManager.GetRolesAsync(user))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ManagementUserItem(
            Id: id,
            UserName: user.UserName ?? string.Empty,
            Email: email,
            IsEnabled: GetBooleanProperty(user, nameof(User.IsEnabled)) ?? true,
            EmailConfirmed: GetBooleanProperty(user, nameof(User.EmailConfirmed)) ?? false,
            RoleNames: roleNames);
    }

    private static async Task<ManagementRoleItem> ToManagementRoleItemAsync(RoleManager<IRole> roleManager, IRole role)
    {
        var id = await roleManager.GetRoleIdAsync(role) ?? GetRoleName(role);
        var roleName = GetRoleName(role);
        var permissionNames = (await roleManager.GetClaimsAsync(role))
            .Where(IsPermissionClaim)
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ManagementRoleItem(
            Id: id,
            Name: roleName,
            PermissionNames: permissionNames);
    }

    private static SiteSettingsItem ToSiteSettingsItem(ISite site)
    {
        return new SiteSettingsItem(
            SiteName: site.SiteName ?? string.Empty,
            TimeZoneId: site.TimeZoneId ?? string.Empty,
            Calendar: site.Calendar ?? string.Empty,
            BaseUrl: site.BaseUrl ?? string.Empty,
            PageSize: site.PageSize,
            MaxPageSize: site.MaxPageSize,
            MaxPagedCount: site.MaxPagedCount,
            UseCdn: site.UseCdn,
            CdnBaseUrl: site.CdnBaseUrl ?? string.Empty,
            AppendVersion: site.AppendVersion,
            ResourceDebugMode: site.ResourceDebugMode.ToString(),
            CacheMode: site.CacheMode.ToString());
    }

    private static ManagementLocalizationItem ToManagementLocalizationItem(LocalizationSettings settings)
    {
        var normalized = NormalizeCultureNames(settings.SupportedCultures);
        var defaultCulture = string.IsNullOrWhiteSpace(settings.DefaultCulture)
            ? (normalized.Cultures.FirstOrDefault() ?? CultureInfo.InstalledUICulture.Name)
            : settings.DefaultCulture.Trim();

        return new ManagementLocalizationItem(
            DefaultCulture: defaultCulture,
            SupportedCultures: normalized.Cultures.Length > 0
                ? normalized.Cultures
                : [defaultCulture]);
    }

    private static IResult? ApplySiteSettingsPatch(ISite site, UpdateSiteSettingsRequest request)
    {
        if (request.SiteName is not null)
        {
            site.SiteName = request.SiteName.Trim();
        }

        if (request.TimeZoneId is not null)
        {
            var timeZoneId = request.TimeZoneId.Trim();
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                try
                {
                    _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                    return Results.BadRequest(new { message = $"Unknown time zone '{timeZoneId}'." });
                }
                catch (InvalidTimeZoneException)
                {
                    return Results.BadRequest(new { message = $"Invalid time zone '{timeZoneId}'." });
                }
            }

            site.TimeZoneId = timeZoneId;
        }

        if (request.Calendar is not null)
        {
            site.Calendar = request.Calendar.Trim();
        }

        if (request.BaseUrl is not null)
        {
            var baseUrl = request.BaseUrl.Trim();
            if (!string.IsNullOrWhiteSpace(baseUrl) && !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
            {
                return Results.BadRequest(new { message = "BaseUrl must be an absolute URL." });
            }

            site.BaseUrl = baseUrl;
        }

        if (request.PageSize.HasValue)
        {
            if (request.PageSize.Value <= 0)
            {
                return Results.BadRequest(new { message = "PageSize must be greater than 0." });
            }

            site.PageSize = request.PageSize.Value;
        }

        if (request.MaxPageSize.HasValue)
        {
            if (request.MaxPageSize.Value <= 0)
            {
                return Results.BadRequest(new { message = "MaxPageSize must be greater than 0." });
            }

            site.MaxPageSize = request.MaxPageSize.Value;
        }

        if (request.MaxPagedCount.HasValue)
        {
            if (request.MaxPagedCount.Value <= 0)
            {
                return Results.BadRequest(new { message = "MaxPagedCount must be greater than 0." });
            }

            site.MaxPagedCount = request.MaxPagedCount.Value;
        }

        if (request.UseCdn.HasValue)
        {
            site.UseCdn = request.UseCdn.Value;
        }

        if (request.CdnBaseUrl is not null)
        {
            var cdnBaseUrl = request.CdnBaseUrl.Trim();
            if (!string.IsNullOrWhiteSpace(cdnBaseUrl) && !Uri.TryCreate(cdnBaseUrl, UriKind.Absolute, out _))
            {
                return Results.BadRequest(new { message = "CdnBaseUrl must be an absolute URL." });
            }

            site.CdnBaseUrl = cdnBaseUrl;
        }

        if (request.AppendVersion.HasValue)
        {
            site.AppendVersion = request.AppendVersion.Value;
        }

        if (request.ResourceDebugMode is not null)
        {
            var resourceDebugMode = request.ResourceDebugMode.Trim();
            if (!Enum.TryParse<ResourceDebugMode>(resourceDebugMode, true, out var parsed))
            {
                return Results.BadRequest(new
                {
                    message = $"Invalid ResourceDebugMode '{resourceDebugMode}'.",
                    allowed = Enum.GetNames<ResourceDebugMode>()
                });
            }

            site.ResourceDebugMode = parsed;
        }

        if (request.CacheMode is not null)
        {
            var cacheMode = request.CacheMode.Trim();
            if (!Enum.TryParse<CacheMode>(cacheMode, true, out var parsed))
            {
                return Results.BadRequest(new
                {
                    message = $"Invalid CacheMode '{cacheMode}'.",
                    allowed = Enum.GetNames<CacheMode>()
                });
            }

            site.CacheMode = parsed;
        }

        if (site.PageSize > 0 && site.MaxPageSize > 0 && site.PageSize > site.MaxPageSize)
        {
            return Results.BadRequest(new { message = "PageSize cannot be greater than MaxPageSize." });
        }

        return null;
    }

    private static IResult? ApplyLocalizationPatch(ISite site, UpdateLocalizationSettingsRequest request)
    {
        var settings = site.As<LocalizationSettings>();

        var supportedCultures = settings.SupportedCultures ?? [];
        if (request.SupportedCultures is not null)
        {
            var normalizedCultures = NormalizeCultureNames(request.SupportedCultures);
            if (normalizedCultures.Invalid.Length > 0)
            {
                return Results.BadRequest(new
                {
                    message = "Unsupported culture names detected.",
                    invalidCultures = normalizedCultures.Invalid
                });
            }

            if (normalizedCultures.Cultures.Length == 0)
            {
                return Results.BadRequest(new { message = "SupportedCultures cannot be empty." });
            }

            supportedCultures = normalizedCultures.Cultures;
            settings.SupportedCultures = supportedCultures;
        }

        if (request.DefaultCulture is not null)
        {
            var normalizedDefaultCulture = NormalizeCultureNames([request.DefaultCulture]);
            if (normalizedDefaultCulture.Invalid.Length > 0)
            {
                return Results.BadRequest(new
                {
                    message = "DefaultCulture is invalid.",
                    invalidCultures = normalizedDefaultCulture.Invalid
                });
            }

            settings.DefaultCulture = normalizedDefaultCulture.Cultures.First();
        }

        var defaultCulture = string.IsNullOrWhiteSpace(settings.DefaultCulture)
            ? (supportedCultures.FirstOrDefault() ?? CultureInfo.InstalledUICulture.Name)
            : settings.DefaultCulture.Trim();
        if (!supportedCultures.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase))
        {
            supportedCultures = supportedCultures.Concat([defaultCulture]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            settings.SupportedCultures = supportedCultures;
        }

        settings.DefaultCulture = defaultCulture;
        return null;
    }

    private static async Task<ManagementOpenIdApplicationItem> ToManagementOpenIdApplicationItemAsync(
        IOpenIdApplicationManager applicationManager,
        object application)
    {
        var permissions = (await applicationManager.GetPermissionsAsync(application))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var scopeNames = ExtractScopeNames(permissions);
        var permissionNames = permissions
            .Where(x => !x.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var redirectUris = (await applicationManager.GetRedirectUrisAsync(application))
            .Select(x => x.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var postLogoutRedirectUris = (await applicationManager.GetPostLogoutRedirectUrisAsync(application))
            .Select(x => x.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var roleNames = (await applicationManager.GetRolesAsync(application))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var requirements = (await applicationManager.GetRequirementsAsync(application))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ManagementOpenIdApplicationItem(
            Id: await applicationManager.GetPhysicalIdAsync(application) ?? string.Empty,
            ClientId: await applicationManager.GetClientIdAsync(application) ?? string.Empty,
            DisplayName: await applicationManager.GetDisplayNameAsync(application) ?? string.Empty,
            ClientType: await applicationManager.GetClientTypeAsync(application) ?? string.Empty,
            ConsentType: await applicationManager.GetConsentTypeAsync(application) ?? string.Empty,
            RedirectUris: redirectUris,
            PostLogoutRedirectUris: postLogoutRedirectUris,
            ScopeNames: scopeNames,
            PermissionNames: permissionNames,
            RoleNames: roleNames,
            Requirements: requirements);
    }

    private static async Task<ManagementOpenIdScopeItem> ToManagementOpenIdScopeItemAsync(
        IOpenIdScopeManager scopeManager,
        object scope)
    {
        var resources = (await scopeManager.GetResourcesAsync(scope))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ManagementOpenIdScopeItem(
            Id: await scopeManager.GetPhysicalIdAsync(scope) ?? string.Empty,
            Name: await scopeManager.GetNameAsync(scope) ?? string.Empty,
            DisplayName: await scopeManager.GetDisplayNameAsync(scope) ?? string.Empty,
            Description: await scopeManager.GetDescriptionAsync(scope) ?? string.Empty,
            Resources: resources);
    }

    private static ManagementRecipeItem ToManagementRecipeItem(RecipeDescriptor descriptor)
    {
        return new ManagementRecipeItem(
            Id: BuildRecipeId(descriptor),
            Name: descriptor.Name ?? string.Empty,
            DisplayName: string.IsNullOrWhiteSpace(descriptor.DisplayName) ? descriptor.Name ?? string.Empty : descriptor.DisplayName,
            Description: descriptor.Description ?? string.Empty,
            BasePath: descriptor.BasePath ?? string.Empty,
            FileName: descriptor.RecipeFileInfo?.Name ?? string.Empty,
            Author: descriptor.Author ?? string.Empty,
            Website: descriptor.WebSite ?? string.Empty,
            Version: descriptor.Version ?? string.Empty,
            Categories: NormalizeNames(descriptor.Categories),
            Tags: NormalizeNames(descriptor.Tags));
    }

    private static async Task<ManagementRecipeItem[]> LoadRecipesAsync(IServiceProvider serviceProvider)
    {
        var descriptors = await LoadRecipeDescriptorsAsync(serviceProvider);
        return descriptors
            .Select(ToManagementRecipeItem)
            .ToArray();
    }

    private static async Task<RecipeDescriptor[]> LoadRecipeDescriptorsAsync(IServiceProvider serviceProvider)
    {
        var harvesters = serviceProvider.GetServices<IRecipeHarvester>().ToArray();
        if (harvesters.Length == 0)
        {
            return [];
        }

        var recipeCollections = await Task.WhenAll(harvesters.Select(x => x.HarvestRecipesAsync()));
        return recipeCollections
            .SelectMany(x => x ?? [])
            .Where(x => x is not null)
            .Where(x => x.RecipeFileInfo is not null && !x.RecipeFileInfo.IsDirectory)
            .Where(x => !x.IsSetupRecipe)
            .Where(x => x.Tags is null || !x.Tags.Contains("hidden", StringComparer.OrdinalIgnoreCase))
            .GroupBy(BuildRecipeId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.Name ?? x.RecipeFileInfo.Name : x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.RecipeFileInfo.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static (RecipeDescriptor? Recipe, IResult? Error) MatchRecipeDescriptor(
        IEnumerable<RecipeDescriptor> source,
        string? recipeId,
        string? recipeName,
        string? fileName)
    {
        var recipes = source.ToArray();
        if (recipes.Length == 0)
        {
            return (null, Results.NotFound(new { message = "No runnable recipes were found in current tenant scope." }));
        }

        var normalizedRecipeId = recipeId?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedRecipeId))
        {
            var recipe = recipes.FirstOrDefault(x => BuildRecipeId(x).Equals(normalizedRecipeId, StringComparison.OrdinalIgnoreCase));
            return recipe is null
                ? (null, Results.NotFound(new { message = $"Recipe '{normalizedRecipeId}' was not found." }))
                : (recipe, null);
        }

        var normalizedRecipeName = recipeName?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedRecipeName))
        {
            var byName = recipes
                .Where(x => (x.Name ?? string.Empty).Equals(normalizedRecipeName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (byName.Length == 1)
            {
                return (byName[0], null);
            }

            if (byName.Length > 1)
            {
                return (null, Results.Conflict(new
                {
                    message = $"Recipe name '{normalizedRecipeName}' is ambiguous. Use recipeId instead.",
                    candidates = byName.Select(BuildRecipeId).ToArray()
                }));
            }

            return (null, Results.NotFound(new { message = $"Recipe name '{normalizedRecipeName}' was not found." }));
        }

        var normalizedFileName = fileName?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedFileName))
        {
            var byFileName = recipes
                .Where(x => (x.RecipeFileInfo?.Name ?? string.Empty).Equals(normalizedFileName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (byFileName.Length == 1)
            {
                return (byFileName[0], null);
            }

            if (byFileName.Length > 1)
            {
                return (null, Results.Conflict(new
                {
                    message = $"Recipe file '{normalizedFileName}' is ambiguous. Use recipeId instead.",
                    candidates = byFileName.Select(BuildRecipeId).ToArray()
                }));
            }

            return (null, Results.NotFound(new { message = $"Recipe file '{normalizedFileName}' was not found." }));
        }

        return (null, Results.BadRequest(new { message = "RecipeId, RecipeName or FileName is required." }));
    }

    private static string BuildRecipeId(RecipeDescriptor descriptor)
    {
        return BuildRecipeId(descriptor.BasePath, descriptor.RecipeFileInfo?.Name);
    }

    private static string BuildRecipeId(string? basePath, string? fileName)
    {
        return $"{(basePath ?? string.Empty).Trim()}|{(fileName ?? string.Empty).Trim()}";
    }

    private static (OpenIdApplicationDescriptor Descriptor, IResult? Error) CreateOpenIdApplicationDescriptor(CreateOpenIdApplicationRequest request)
    {
        var descriptor = new OpenIdApplicationDescriptor();

        var clientId = request.ClientId.Trim();
        var displayName = request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(displayName))
        {
            return (descriptor, Results.BadRequest(new { message = "ClientId and DisplayName are required." }));
        }

        descriptor.ClientId = clientId;
        descriptor.DisplayName = displayName;
        descriptor.ClientType = string.IsNullOrWhiteSpace(request.ClientType)
            ? OpenIddictConstants.ClientTypes.Confidential
            : request.ClientType.Trim();
        descriptor.ConsentType = string.IsNullOrWhiteSpace(request.ConsentType)
            ? OpenIddictConstants.ConsentTypes.Explicit
            : request.ConsentType.Trim();

        var clientSecret = request.ClientSecret?.Trim();
        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            descriptor.ClientSecret = clientSecret;
        }

        if (string.Equals(descriptor.ClientType, OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(descriptor.ClientSecret))
        {
            return (descriptor, Results.BadRequest(new { message = "Public clients cannot have a client secret." }));
        }

        if (string.Equals(descriptor.ClientType, OpenIddictConstants.ClientTypes.Confidential, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(descriptor.ClientSecret))
        {
            return (descriptor, Results.BadRequest(new { message = "ClientSecret is required for confidential clients." }));
        }

        var redirectUris = NormalizeAbsoluteUris(request.RedirectUris);
        if (redirectUris.Invalid.Length > 0)
        {
            return (descriptor, Results.BadRequest(new
            {
                message = "Invalid redirect URIs detected.",
                invalidUris = redirectUris.Invalid
            }));
        }

        foreach (var redirectUri in redirectUris.Uris)
        {
            descriptor.RedirectUris.Add(redirectUri);
        }

        var postLogoutUris = NormalizeAbsoluteUris(request.PostLogoutRedirectUris);
        if (postLogoutUris.Invalid.Length > 0)
        {
            return (descriptor, Results.BadRequest(new
            {
                message = "Invalid post logout redirect URIs detected.",
                invalidUris = postLogoutUris.Invalid
            }));
        }

        foreach (var postLogoutUri in postLogoutUris.Uris)
        {
            descriptor.PostLogoutRedirectUris.Add(postLogoutUri);
        }

        var scopeNames = NormalizeNames(request.ScopeNames);
        var permissionNames = NormalizeNames(request.PermissionNames)
            .Where(x => !x.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.OrdinalIgnoreCase));
        foreach (var permissionName in permissionNames)
        {
            descriptor.Permissions.Add(permissionName);
        }

        foreach (var scopeName in scopeNames)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scopeName);
        }

        foreach (var roleName in NormalizeNames(request.RoleNames))
        {
            descriptor.Roles.Add(roleName);
        }

        foreach (var requirement in NormalizeNames(request.Requirements))
        {
            descriptor.Requirements.Add(requirement);
        }

        return (descriptor, null);
    }

    private static IResult? ApplyOpenIdApplicationPatch(OpenIdApplicationDescriptor descriptor, PatchOpenIdApplicationRequest request)
    {
        if (request.ClientId is not null)
        {
            var clientId = request.ClientId.Trim();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Results.BadRequest(new { message = "ClientId cannot be empty." });
            }

            descriptor.ClientId = clientId;
        }

        if (request.DisplayName is not null)
        {
            var displayName = request.DisplayName.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return Results.BadRequest(new { message = "DisplayName cannot be empty." });
            }

            descriptor.DisplayName = displayName;
        }

        if (request.ClientType is not null)
        {
            descriptor.ClientType = request.ClientType.Trim();
        }

        if (request.ConsentType is not null)
        {
            descriptor.ConsentType = request.ConsentType.Trim();
        }

        if (request.ClientSecret is not null)
        {
            descriptor.ClientSecret = string.IsNullOrWhiteSpace(request.ClientSecret)
                ? null
                : request.ClientSecret.Trim();
        }

        var existingScopeNames = ExtractScopeNames(descriptor.Permissions);
        var targetScopeNames = request.ScopeNames is null
            ? existingScopeNames
            : NormalizeNames(request.ScopeNames);
        var targetPermissionNames = request.PermissionNames is null
            ? descriptor.Permissions.Where(x => !x.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.OrdinalIgnoreCase))
                .ToArray()
            : NormalizeNames(request.PermissionNames)
                .Where(x => !x.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        descriptor.Permissions.Clear();
        foreach (var permissionName in targetPermissionNames)
        {
            descriptor.Permissions.Add(permissionName);
        }

        foreach (var scopeName in targetScopeNames)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scopeName);
        }

        if (request.RoleNames is not null)
        {
            descriptor.Roles.Clear();
            foreach (var roleName in NormalizeNames(request.RoleNames))
            {
                descriptor.Roles.Add(roleName);
            }
        }

        if (request.Requirements is not null)
        {
            descriptor.Requirements.Clear();
            foreach (var requirement in NormalizeNames(request.Requirements))
            {
                descriptor.Requirements.Add(requirement);
            }
        }

        if (request.RedirectUris is not null)
        {
            var redirectUris = NormalizeAbsoluteUris(request.RedirectUris);
            if (redirectUris.Invalid.Length > 0)
            {
                return Results.BadRequest(new
                {
                    message = "Invalid redirect URIs detected.",
                    invalidUris = redirectUris.Invalid
                });
            }

            descriptor.RedirectUris.Clear();
            foreach (var redirectUri in redirectUris.Uris)
            {
                descriptor.RedirectUris.Add(redirectUri);
            }
        }

        if (request.PostLogoutRedirectUris is not null)
        {
            var postLogoutRedirectUris = NormalizeAbsoluteUris(request.PostLogoutRedirectUris);
            if (postLogoutRedirectUris.Invalid.Length > 0)
            {
                return Results.BadRequest(new
                {
                    message = "Invalid post logout redirect URIs detected.",
                    invalidUris = postLogoutRedirectUris.Invalid
                });
            }

            descriptor.PostLogoutRedirectUris.Clear();
            foreach (var postLogoutRedirectUri in postLogoutRedirectUris.Uris)
            {
                descriptor.PostLogoutRedirectUris.Add(postLogoutRedirectUri);
            }
        }

        if (string.IsNullOrWhiteSpace(descriptor.ClientId) || string.IsNullOrWhiteSpace(descriptor.DisplayName))
        {
            return Results.BadRequest(new { message = "ClientId and DisplayName are required." });
        }

        if (string.Equals(descriptor.ClientType, OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(descriptor.ClientSecret))
        {
            return Results.BadRequest(new { message = "Public clients cannot have a client secret." });
        }

        return null;
    }

    private static (OpenIdScopeDescriptor Descriptor, IResult? Error) CreateOpenIdScopeDescriptor(
        string displayName,
        string name,
        string? description,
        IEnumerable<string>? resources,
        string tenantName)
    {
        var descriptor = new OpenIdScopeDescriptor
        {
            Name = name.Trim(),
            DisplayName = displayName.Trim(),
            Description = description?.Trim()
        };

        if (string.IsNullOrWhiteSpace(descriptor.Name) || string.IsNullOrWhiteSpace(descriptor.DisplayName))
        {
            return (descriptor, Results.BadRequest(new { message = "Scope Name and DisplayName are required." }));
        }

        var normalizedResources = NormalizeNames(resources);
        var protectedResource = OpenIdConstants.Prefixes.Tenant + tenantName;
        if (normalizedResources.Contains(protectedResource, StringComparer.OrdinalIgnoreCase))
        {
            return (descriptor, Results.BadRequest(new
            {
                message = "Resources cannot contain the current tenant reserved value.",
                reservedResource = protectedResource
            }));
        }

        foreach (var resource in normalizedResources)
        {
            descriptor.Resources.Add(resource);
        }

        return (descriptor, null);
    }

    private static IResult? ApplyOpenIdScopePatch(OpenIdScopeDescriptor descriptor, PatchOpenIdScopeRequest request, string tenantName)
    {
        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return Results.BadRequest(new { message = "Scope name cannot be empty." });
            }

            descriptor.Name = name;
        }

        if (request.DisplayName is not null)
        {
            var displayName = request.DisplayName.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return Results.BadRequest(new { message = "Scope display name cannot be empty." });
            }

            descriptor.DisplayName = displayName;
        }

        if (request.Description is not null)
        {
            descriptor.Description = request.Description.Trim();
        }

        if (request.Resources is not null)
        {
            var resources = NormalizeNames(request.Resources);
            var protectedResource = OpenIdConstants.Prefixes.Tenant + tenantName;
            if (resources.Contains(protectedResource, StringComparer.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new
                {
                    message = "Resources cannot contain the current tenant reserved value.",
                    reservedResource = protectedResource
                });
            }

            descriptor.Resources.Clear();
            foreach (var resource in resources)
            {
                descriptor.Resources.Add(resource);
            }
        }

        return null;
    }

    private static async Task<IUser?> FindUserAsync(UserManager<IUser> userManager, string idOrUserName)
    {
        var normalized = idOrUserName.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(normalized);
        if (user is not null)
        {
            return user;
        }

        return await userManager.FindByNameAsync(normalized);
    }

    private static async Task<IRole?> FindRoleAsync(RoleManager<IRole> roleManager, string idOrName)
    {
        var normalized = idOrName.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var role = await roleManager.FindByIdAsync(normalized);
        if (role is not null)
        {
            return role;
        }

        return await roleManager.FindByNameAsync(normalized);
    }

    private static string GetRoleName(IRole role)
    {
        return role.RoleName ?? string.Empty;
    }

    private static async Task<IResult?> UpdateRolePermissionsAsync(
        IServiceProvider serviceProvider,
        RoleManager<IRole> roleManager,
        IRole role,
        IEnumerable<string> requestedPermissionNames)
    {
        var targetPermissionNames = NormalizeNames(requestedPermissionNames);
        var availablePermissionNames = (await LoadPermissionItemsAsync(serviceProvider))
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownPermissions = targetPermissionNames
            .Where(name => !availablePermissionNames.Contains(name))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unknownPermissions.Length > 0)
        {
            return Results.BadRequest(new
            {
                message = "Unknown permission names detected.",
                unknownPermissions
            });
        }

        var claims = await roleManager.GetClaimsAsync(role);
        var currentPermissionClaims = claims
            .Where(IsPermissionClaim)
            .ToArray();
        var currentPermissionNames = currentPermissionClaims
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetPermissionMap = targetPermissionNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in currentPermissionClaims.Where(x => !targetPermissionMap.Contains(x.Value)).ToArray())
        {
            var removeResult = await roleManager.RemoveClaimAsync(role, claim);
            if (!removeResult.Succeeded)
            {
                return Results.BadRequest(new
                {
                    message = "Failed to remove role permission claims.",
                    errors = ToIdentityErrors(removeResult)
                });
            }
        }

        foreach (var permissionName in targetPermissionNames.Where(name => !currentPermissionNames.Contains(name)))
        {
            var addResult = await roleManager.AddClaimAsync(role, new Claim(Permission.ClaimType, permissionName));
            if (!addResult.Succeeded)
            {
                return Results.BadRequest(new
                {
                    message = "Failed to assign role permissions.",
                    errors = ToIdentityErrors(addResult)
                });
            }
        }

        return null;
    }

    private static async Task<string[]> GetUnknownRoleNamesAsync(RoleManager<IRole> roleManager, IEnumerable<string> roleNames)
    {
        var unknown = new List<string>();
        foreach (var roleName in roleNames)
        {
            if (await roleManager.FindByNameAsync(roleName) is null)
            {
                unknown.Add(roleName);
            }
        }

        return unknown
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<ManagementPermissionItem[]> LoadPermissionItemsAsync(IServiceProvider serviceProvider)
    {
        var providers = serviceProvider.GetServices<IPermissionProvider>();
        var map = new Dictionary<string, ManagementPermissionItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            foreach (var permission in await ReadPermissionsFromProviderAsync(provider))
            {
                if (string.IsNullOrWhiteSpace(permission.Name))
                {
                    continue;
                }

                map[permission.Name] = new ManagementPermissionItem(
                    Name: permission.Name,
                    Description: permission.Description ?? string.Empty,
                    Category: permission.Category ?? provider.GetType().Name);
            }
        }

        return map.Values
            .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IEnumerable<Permission>> ReadPermissionsFromProviderAsync(IPermissionProvider provider)
    {
        return await provider.GetPermissionsAsync();
    }

    private static bool IsPermissionClaim(Claim claim)
    {
        return claim.Type.Equals(Permission.ClaimType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool? GetBooleanProperty(object target, string propertyName)
    {
        var value = target.GetType().GetProperty(propertyName)?.GetValue(target);
        return value is bool boolValue ? boolValue : null;
    }

    private static bool SetBooleanProperty(object target, string propertyName, bool value)
    {
        var propertyInfo = target.GetType().GetProperty(propertyName);
        if (propertyInfo is null || !propertyInfo.CanWrite || propertyInfo.PropertyType != typeof(bool))
        {
            return false;
        }

        var current = propertyInfo.GetValue(target);
        if (current is bool boolValue && boolValue == value)
        {
            return false;
        }

        propertyInfo.SetValue(target, value);
        return true;
    }

    private static string[] ToIdentityErrors(IdentityResult result)
    {
        return result.Errors
            .Select(x => string.IsNullOrWhiteSpace(x.Description) ? x.Code : x.Description)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static async Task<IResult> ExecuteWithTenantAsync(
        string? tenant,
        ShellSettings currentShellSettings,
        IShellHost shellHost,
        IServiceProvider rootServiceProvider,
        Func<IServiceProvider, ShellSettings, Task<IResult>> action)
    {
        if (!string.IsNullOrWhiteSpace(tenant) &&
            currentShellSettings.IsDefaultShell() &&
            !tenant.Equals(currentShellSettings.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (!shellHost.TryGetSettings(tenant, out var tenantSettings))
            {
                return Results.NotFound(new { message = $"Tenant '{tenant}' was not found." });
            }

            if (!tenantSettings.IsRunning())
            {
                return Results.BadRequest(new { message = $"Tenant '{tenant}' is not running." });
            }

            IResult? result = null;
            var scope = await shellHost.GetScopeAsync(tenantSettings);
            await scope.UsingAsync(async scopeContext =>
            {
                result = await action(scopeContext.ServiceProvider, scopeContext.ShellContext.Settings);
            });

            return result ?? Results.Problem("No result returned from tenant scope.");
        }

        return await action(rootServiceProvider, currentShellSettings);
    }

    private static async Task<IResult?> EnsureManagementAccessAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        IAuthorizationService authorizationService,
        Permission permission)
    {
        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;
        var allowAnonymous = configuration.GetValue("SaaS:AllowAnonymousManagementApi", true);

        if (!isAuthenticated && allowAnonymous)
        {
            return null;
        }

        if (await authorizationService.AuthorizeAsync(httpContext.User, permission))
        {
            return null;
        }

        return isAuthenticated ? Results.Forbid() : Results.Unauthorized();
    }

    private static string NormalizePathSegment(string value)
    {
        return value.Trim().Trim('/');
    }

    private static (string[] Cultures, string[] Invalid) NormalizeCultureNames(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return ([], []);
        }

        var normalized = new List<string>();
        var invalid = new List<string>();

        foreach (var value in values)
        {
            var candidate = value?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            try
            {
                var culture = CultureInfo.GetCultureInfo(candidate);
                normalized.Add(culture.Name);
            }
            catch (CultureNotFoundException)
            {
                invalid.Add(candidate);
            }
        }

        return (
            normalized
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            invalid
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static (Uri[] Uris, string[] Invalid) NormalizeAbsoluteUris(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return ([], []);
        }

        var uris = new List<Uri>();
        var invalid = new List<string>();

        foreach (var value in values)
        {
            var candidate = value?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                invalid.Add(candidate);
                continue;
            }

            uris.Add(uri);
        }

        return (
            uris
                .GroupBy(x => x.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderBy(x => x.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            invalid
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static string[] ExtractScopeNames(IEnumerable<string> permissionNames)
    {
        var scopePrefix = OpenIddictConstants.Permissions.Prefixes.Scope;
        return permissionNames
            .Where(x => x.StartsWith(scopePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(x => x[scopePrefix.Length..])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] NormalizeNames(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
