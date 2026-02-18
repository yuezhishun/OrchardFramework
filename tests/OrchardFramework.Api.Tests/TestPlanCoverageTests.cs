using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrchardFramework.Api.Tests;

public class TestPlanCoverageTests
{
    [Fact]
    public async Task TenantManagement_Cases_T01_T09_Should_Be_Covered()
    {
        using var factory = new ManagementAuthorizedWebApplicationFactory("SaaS.Base");
        using var client = CreateClient(factory, allowAutoRedirect: false, handleCookies: true);

        await WaitForReadySummaryAsync(client);

        // T-01
        var createTenantAResponse = await PostJsonAsync(client, "/api/management/tenants", new
        {
            name = "tenant-a",
            requestUrlPrefix = "tenant-a",
            category = "biz",
            description = "tenant a"
        });
        Assert.Equal(HttpStatusCode.Created, createTenantAResponse.StatusCode);
        using (var createTenantADocument = await ReadJsonAsync(createTenantAResponse))
        {
            Assert.Equal("tenant-a", createTenantADocument.RootElement.GetProperty("name").GetString());
            Assert.Equal("tenant-a", createTenantADocument.RootElement.GetProperty("requestUrlPrefix").GetString());
        }

        // T-02
        var duplicateResponse = await PostJsonAsync(client, "/api/management/tenants", new
        {
            name = "tenant-a",
            requestUrlPrefix = "tenant-a"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        // T-03
        var emptyNameResponse = await PostJsonAsync(client, "/api/management/tenants", new
        {
            name = "   ",
            requestUrlPrefix = "tenant-empty"
        });
        Assert.Equal(HttpStatusCode.BadRequest, emptyNameResponse.StatusCode);
        using (var emptyNameDocument = await ReadJsonAsync(emptyNameResponse))
        {
            Assert.Contains("Tenant name is required", emptyNameDocument.RootElement.GetProperty("message").GetString() ?? string.Empty);
        }

        // T-04
        var patchTenantAResponse = await PatchJsonAsync(client, "/api/management/tenants/tenant-a", new
        {
            description = "tenant a updated",
            category = "ops"
        });
        Assert.Equal(HttpStatusCode.OK, patchTenantAResponse.StatusCode);
        using (var patchTenantADocument = await ReadJsonAsync(patchTenantAResponse))
        {
            Assert.Equal("tenant a updated", patchTenantADocument.RootElement.GetProperty("description").GetString());
            Assert.Equal("ops", patchTenantADocument.RootElement.GetProperty("category").GetString());
        }

        await SetupTenantAsync(client, "tenant-a", "Tenant A", "a_root", "a_root@tenant-a.local", "A_root#2026");

        // T-05
        var disableTenantAResponse = await PatchJsonAsync(client, "/api/management/tenants/tenant-a", new
        {
            enabled = false
        });
        Assert.Equal(HttpStatusCode.OK, disableTenantAResponse.StatusCode);
        using (var disableTenantADocument = await ReadJsonAsync(disableTenantAResponse))
        {
            Assert.Equal("Disabled", disableTenantADocument.RootElement.GetProperty("state").GetString());
        }

        // T-06
        var createTenantBResponse = await PostJsonAsync(client, "/api/management/tenants", new
        {
            name = "tenant-b",
            requestUrlPrefix = "tenant-b"
        });
        Assert.Equal(HttpStatusCode.Created, createTenantBResponse.StatusCode);

        var enableUninitializedResponse = await PatchJsonAsync(client, "/api/management/tenants/tenant-b", new
        {
            enabled = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, enableUninitializedResponse.StatusCode);
        using (var enableUninitializedDocument = await ReadJsonAsync(enableUninitializedResponse))
        {
            Assert.Contains("must be setup", enableUninitializedDocument.RootElement.GetProperty("message").GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        // T-07
        var createTenantRunningResponse = await PostJsonAsync(client, "/api/management/tenants", new
        {
            name = "tenant-running",
            requestUrlPrefix = "tenant-running"
        });
        Assert.Equal(HttpStatusCode.Created, createTenantRunningResponse.StatusCode);

        await SetupTenantAsync(client, "tenant-running", "Tenant Running", "running_admin", "running_admin@tenant.local", "Running_admin#2026");

        var removeRunningTenantResponse = await PatchJsonAsync(client, "/api/management/tenants/tenant-running", new
        {
            operation = "remove"
        });
        Assert.Equal(HttpStatusCode.BadRequest, removeRunningTenantResponse.StatusCode);
        using (var removeRunningTenantDocument = await ReadJsonAsync(removeRunningTenantResponse))
        {
            Assert.Contains("Only disabled or uninitialized", removeRunningTenantDocument.RootElement.GetProperty("message").GetString() ?? string.Empty);
        }

        // T-08
        var disableRunningTenantResponse = await PatchJsonAsync(client, "/api/management/tenants/tenant-running", new
        {
            enabled = false
        });
        Assert.Equal(HttpStatusCode.OK, disableRunningTenantResponse.StatusCode);

        var removeDisabledTenantResponse = await PatchJsonAsync(client, "/api/management/tenants/tenant-running", new
        {
            operation = "remove"
        });
        Assert.Equal(HttpStatusCode.OK, removeDisabledTenantResponse.StatusCode);
        using (var removeDisabledTenantDocument = await ReadJsonAsync(removeDisabledTenantResponse))
        {
            Assert.Equal("tenant-running", removeDisabledTenantDocument.RootElement.GetProperty("removed").GetString());
        }

        var tenantsAfterRemovalResponse = await client.GetAsync("/api/management/tenants");
        tenantsAfterRemovalResponse.EnsureSuccessStatusCode();
        using (var tenantsAfterRemovalDocument = await ReadJsonAsync(tenantsAfterRemovalResponse))
        {
            Assert.DoesNotContain(tenantsAfterRemovalDocument.RootElement.EnumerateArray(), x =>
                string.Equals(x.GetProperty("name").GetString(), "tenant-running", StringComparison.OrdinalIgnoreCase));
        }

        // T-09
        var removeDefaultResponse = await PatchJsonAsync(client, "/api/management/tenants/Default", new
        {
            operation = "remove"
        });
        Assert.Equal(HttpStatusCode.BadRequest, removeDefaultResponse.StatusCode);
        using (var removeDefaultDocument = await ReadJsonAsync(removeDefaultResponse))
        {
            Assert.Contains("Default tenant cannot be removed", removeDefaultDocument.RootElement.GetProperty("message").GetString() ?? string.Empty);
        }
    }

    [Fact]
    public async Task FeatureManagement_Cases_F01_F09_Should_Be_Covered()
    {
        using var factory = new ManagementAuthorizedWebApplicationFactory("SaaS.Base");
        using var client = CreateClient(factory, allowAutoRedirect: false, handleCookies: true);

        await WaitForReadySummaryAsync(client);

        var createTenantBResponse = await PostJsonAsync(client, "/api/management/tenants", new
        {
            name = "tenant-b",
            requestUrlPrefix = "tenant-b"
        });
        Assert.Equal(HttpStatusCode.Created, createTenantBResponse.StatusCode);

        // F-01
        var featuresResponse = await client.GetAsync("/api/management/features?tenant=Default");
        featuresResponse.EnsureSuccessStatusCode();

        string toggleFeatureId;
        string alwaysEnabledFeatureId;
        using (var featuresDocument = await ReadJsonAsync(featuresResponse))
        {
            var featureItems = featuresDocument.RootElement.GetProperty("features").EnumerateArray().ToList();
            Assert.NotEmpty(featureItems);

            var toggleFeature = featureItems.FirstOrDefault(x =>
                !x.GetProperty("isAlwaysEnabled").GetBoolean() &&
                !x.GetProperty("enabled").GetBoolean());
            if (toggleFeature.ValueKind == JsonValueKind.Undefined)
            {
                toggleFeature = featureItems.First(x => !x.GetProperty("isAlwaysEnabled").GetBoolean());
            }

            toggleFeatureId = toggleFeature.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Unable to resolve toggle feature id.");

            alwaysEnabledFeatureId = featureItems
                .First(x => x.GetProperty("isAlwaysEnabled").GetBoolean())
                .GetProperty("id")
                .GetString()
                ?? throw new InvalidOperationException("Unable to resolve always-enabled feature id.");
        }

        // F-02
        var enableFeatureResponse = await PutJsonAsync(client, "/api/management/features", new
        {
            tenant = "Default",
            enable = new[] { toggleFeatureId }
        });
        enableFeatureResponse.EnsureSuccessStatusCode();
        using (var enableFeatureDocument = await ReadJsonAsync(enableFeatureResponse))
        {
            Assert.Contains(enableFeatureDocument.RootElement.GetProperty("changed").GetProperty("enabled").EnumerateArray(), x =>
                string.Equals(x.GetString(), toggleFeatureId, StringComparison.OrdinalIgnoreCase));
        }

        // F-03
        var disableFeatureResponse = await PutJsonAsync(client, "/api/management/features", new
        {
            tenant = "Default",
            disable = new[] { toggleFeatureId }
        });
        disableFeatureResponse.EnsureSuccessStatusCode();
        using (var disableFeatureDocument = await ReadJsonAsync(disableFeatureResponse))
        {
            Assert.Contains(disableFeatureDocument.RootElement.GetProperty("changed").GetProperty("disabled").EnumerateArray(), x =>
                string.Equals(x.GetString(), toggleFeatureId, StringComparison.OrdinalIgnoreCase));
        }

        // F-04
        var disableAlwaysEnabledResponse = await PutJsonAsync(client, "/api/management/features", new
        {
            tenant = "Default",
            disable = new[] { alwaysEnabledFeatureId }
        });
        Assert.Equal(HttpStatusCode.BadRequest, disableAlwaysEnabledResponse.StatusCode);
        using (var disableAlwaysEnabledDocument = await ReadJsonAsync(disableAlwaysEnabledResponse))
        {
            Assert.Contains(disableAlwaysEnabledDocument.RootElement.GetProperty("blocked").EnumerateArray(), x =>
                string.Equals(x.GetString(), alwaysEnabledFeatureId, StringComparison.OrdinalIgnoreCase));
        }

        // F-05
        var unknownFeatureResponse = await PutJsonAsync(client, "/api/management/features", new
        {
            tenant = "Default",
            enable = new[] { "Unknown.Feature.Case" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknownFeatureResponse.StatusCode);
        using (var unknownFeatureDocument = await ReadJsonAsync(unknownFeatureResponse))
        {
            Assert.Contains(unknownFeatureDocument.RootElement.GetProperty("unknown").EnumerateArray(), x =>
                string.Equals(x.GetString(), "Unknown.Feature.Case", StringComparison.OrdinalIgnoreCase));
        }

        // F-06
        var createProfileResponse = await PutJsonAsync(client, "/api/management/feature-profiles", new
        {
            id = "SaaSLean",
            name = "SaaSLean",
            featureRules = new[]
            {
                new { rule = "Include", expression = "OrchardCore.Localization" }
            }
        });
        createProfileResponse.EnsureSuccessStatusCode();
        using (var createProfileDocument = await ReadJsonAsync(createProfileResponse))
        {
            Assert.Equal("SaaSLean", createProfileDocument.RootElement.GetProperty("id").GetString());
        }

        // F-07
        var bindProfileResponse = await PatchJsonAsync(client, "/api/management/tenants/tenant-b", new
        {
            featureProfiles = new[] { "SaaSLean" }
        });
        bindProfileResponse.EnsureSuccessStatusCode();

        var profilesResponse = await client.GetAsync("/api/management/feature-profiles");
        profilesResponse.EnsureSuccessStatusCode();
        using (var profilesDocument = await ReadJsonAsync(profilesResponse))
        {
            var saasLeanProfile = profilesDocument.RootElement.EnumerateArray().First(x =>
                string.Equals(x.GetProperty("id").GetString(), "SaaSLean", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(saasLeanProfile.GetProperty("assignedTenants").EnumerateArray(), x =>
                string.Equals(x.GetString(), "tenant-b", StringComparison.OrdinalIgnoreCase));
        }

        // F-08
        var removeProfileResponse = await PutJsonAsync(client, "/api/management/feature-profiles", new
        {
            id = "SaaSLean",
            delete = true
        });
        removeProfileResponse.EnsureSuccessStatusCode();
        using (var removeProfileDocument = await ReadJsonAsync(removeProfileResponse))
        {
            Assert.Equal("SaaSLean", removeProfileDocument.RootElement.GetProperty("removed").GetString());
        }

        // F-09
        var updateFeatureOnUninitializedTenantResponse = await PutJsonAsync(client, "/api/management/features", new
        {
            tenant = "tenant-b",
            enable = new[] { toggleFeatureId }
        });
        Assert.Equal(HttpStatusCode.BadRequest, updateFeatureOnUninitializedTenantResponse.StatusCode);
        using (var updateFeatureOnUninitializedTenantDocument = await ReadJsonAsync(updateFeatureOnUninitializedTenantResponse))
        {
            Assert.Contains("not running", updateFeatureOnUninitializedTenantDocument.RootElement.GetProperty("message").GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task UserRoleIsolation_Cases_UR01_UR09_Should_Be_Covered()
    {
        using var factory = new ManagementAuthorizedWebApplicationFactory("SaaS.Base");
        using var client = CreateClient(factory, allowAutoRedirect: false, handleCookies: true);

        await WaitForReadySummaryAsync(client);

        await CreateAndSetupTenantAsync(client, "tenant-a", "tenant-a", "Tenant A", "a_root", "a_root@tenant-a.local", "A_root#2026");
        await CreateAndSetupTenantAsync(client, "tenant-b", "tenant-b", "Tenant B", "b_root", "b_root@tenant-b.local", "B_root#2026");

        // UR-01
        var createRoleInTenantAResponse = await PostJsonAsync(client, "/api/management/roles", new
        {
            tenant = "tenant-a",
            name = "TenantAdmin"
        });
        Assert.Equal(HttpStatusCode.Created, createRoleInTenantAResponse.StatusCode);
        string tenantAdminRoleId;
        using (var createRoleInTenantADocument = await ReadJsonAsync(createRoleInTenantAResponse))
        {
            tenantAdminRoleId = createRoleInTenantADocument.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Role id for tenant-a is missing.");
        }

        // UR-02
        var createRoleInTenantBResponse = await PostJsonAsync(client, "/api/management/roles", new
        {
            tenant = "tenant-b",
            name = "TenantAdmin"
        });
        Assert.Equal(HttpStatusCode.Created, createRoleInTenantBResponse.StatusCode);

        // UR-03
        var permissionsResponse = await client.GetAsync("/api/management/permissions?tenant=tenant-a");
        permissionsResponse.EnsureSuccessStatusCode();

        string selectedPermission;
        using (var permissionsDocument = await ReadJsonAsync(permissionsResponse))
        {
            selectedPermission = permissionsDocument.RootElement
                .EnumerateArray()
                .First()
                .GetProperty("name")
                .GetString()
                ?? throw new InvalidOperationException("Unable to select a permission name.");
        }

        var updateRolePermissionsResponse = await PutJsonAsync(client, $"/api/management/roles/{Uri.EscapeDataString(tenantAdminRoleId)}/permissions", new
        {
            tenant = "tenant-a",
            permissionNames = new[] { selectedPermission }
        });
        updateRolePermissionsResponse.EnsureSuccessStatusCode();
        using (var updateRolePermissionsDocument = await ReadJsonAsync(updateRolePermissionsResponse))
        {
            Assert.Contains(updateRolePermissionsDocument.RootElement.GetProperty("permissionNames").EnumerateArray(), x =>
                string.Equals(x.GetString(), selectedPermission, StringComparison.OrdinalIgnoreCase));
        }

        // UR-04
        var createUserInTenantAResponse = await PostJsonAsync(client, "/api/management/users", new
        {
            tenant = "tenant-a",
            userName = "a_admin",
            email = "a_admin@tenant-a.local",
            password = "A_admin#2026",
            roleNames = new[] { "TenantAdmin" }
        });
        Assert.Equal(HttpStatusCode.Created, createUserInTenantAResponse.StatusCode);
        string tenantAUserId;
        using (var createUserInTenantADocument = await ReadJsonAsync(createUserInTenantAResponse))
        {
            tenantAUserId = createUserInTenantADocument.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("User id for tenant-a is missing.");
            Assert.Contains(createUserInTenantADocument.RootElement.GetProperty("roleNames").EnumerateArray(), x =>
                string.Equals(x.GetString(), "TenantAdmin", StringComparison.OrdinalIgnoreCase));
        }

        // UR-05
        var createUserInTenantBResponse = await PostJsonAsync(client, "/api/management/users", new
        {
            tenant = "tenant-b",
            userName = "b_admin",
            email = "b_admin@tenant-b.local",
            password = "B_admin#2026",
            roleNames = new[] { "TenantAdmin" }
        });
        Assert.Equal(HttpStatusCode.Created, createUserInTenantBResponse.StatusCode);

        // UR-06
        var usersInTenantAResponse = await client.GetAsync("/api/management/users?tenant=tenant-a");
        usersInTenantAResponse.EnsureSuccessStatusCode();
        var usersInTenantBResponse = await client.GetAsync("/api/management/users?tenant=tenant-b");
        usersInTenantBResponse.EnsureSuccessStatusCode();

        using (var usersInTenantADocument = await ReadJsonAsync(usersInTenantAResponse))
        using (var usersInTenantBDocument = await ReadJsonAsync(usersInTenantBResponse))
        {
            Assert.Contains(usersInTenantADocument.RootElement.EnumerateArray(), x =>
                string.Equals(x.GetProperty("userName").GetString(), "a_admin", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(usersInTenantADocument.RootElement.EnumerateArray(), x =>
                string.Equals(x.GetProperty("userName").GetString(), "b_admin", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(usersInTenantBDocument.RootElement.EnumerateArray(), x =>
                string.Equals(x.GetProperty("userName").GetString(), "b_admin", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(usersInTenantBDocument.RootElement.EnumerateArray(), x =>
                string.Equals(x.GetProperty("userName").GetString(), "a_admin", StringComparison.OrdinalIgnoreCase));
        }

        // UR-07
        var createTenantBOnlyRoleResponse = await PostJsonAsync(client, "/api/management/roles", new
        {
            tenant = "tenant-b",
            name = "BOnlyRole"
        });
        Assert.Equal(HttpStatusCode.Created, createTenantBOnlyRoleResponse.StatusCode);

        var assignTenantBOnlyRoleInTenantAResponse = await PatchJsonAsync(client, $"/api/management/users/{Uri.EscapeDataString(tenantAUserId)}", new
        {
            tenant = "tenant-a",
            roleNames = new[] { "BOnlyRole" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, assignTenantBOnlyRoleInTenantAResponse.StatusCode);
        using (var assignTenantBOnlyRoleInTenantADocument = await ReadJsonAsync(assignTenantBOnlyRoleInTenantAResponse))
        {
            Assert.Contains(assignTenantBOnlyRoleInTenantADocument.RootElement.GetProperty("unknownRoles").EnumerateArray(), x =>
                string.Equals(x.GetString(), "BOnlyRole", StringComparison.OrdinalIgnoreCase));
        }

        // UR-08
        var disableTenantAUserResponse = await PatchJsonAsync(client, $"/api/management/users/{Uri.EscapeDataString(tenantAUserId)}", new
        {
            tenant = "tenant-a",
            isEnabled = false
        });
        disableTenantAUserResponse.EnsureSuccessStatusCode();
        using (var disableTenantAUserDocument = await ReadJsonAsync(disableTenantAUserResponse))
        {
            Assert.False(disableTenantAUserDocument.RootElement.GetProperty("isEnabled").GetBoolean());
        }

        // UR-09
        var removeRoleInTenantAResponse = await PatchJsonAsync(client, $"/api/management/roles/{Uri.EscapeDataString(tenantAdminRoleId)}", new
        {
            tenant = "tenant-a",
            operation = "remove"
        });
        removeRoleInTenantAResponse.EnsureSuccessStatusCode();

        var usersAfterRoleRemovalResponse = await client.GetAsync("/api/management/users?tenant=tenant-a");
        usersAfterRoleRemovalResponse.EnsureSuccessStatusCode();
        using (var usersAfterRoleRemovalDocument = await ReadJsonAsync(usersAfterRoleRemovalResponse))
        {
            var tenantAUser = usersAfterRoleRemovalDocument.RootElement.EnumerateArray().First(x =>
                string.Equals(x.GetProperty("userName").GetString(), "a_admin", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(tenantAUser.GetProperty("roleNames").EnumerateArray(), x =>
                string.Equals(x.GetString(), "TenantAdmin", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Login_Cases_L01_L05_Should_Be_Covered()
    {
        var sharedContentRoot = Path.Combine(Path.GetTempPath(), $"orchardframework-login-seed-{Guid.NewGuid():N}");
        using (var seedFactory = new ManagementAuthorizedWebApplicationFactory("SaaS.Base", sharedContentRoot, ownsContentRoot: false))
        using (var seedClient = CreateClient(seedFactory, allowAutoRedirect: false, handleCookies: true))
        {
            await WaitForReadySummaryAsync(seedClient);
            await CreateAndSetupTenantAsync(seedClient, "tenant-a", "tenant-a", "Tenant A", "a_root", "a_root@tenant-a.local", "A_root#2026");

            var seedCreateRoleResponse = await PostJsonAsync(seedClient, "/api/management/roles", new
            {
                tenant = "tenant-a",
                name = "TenantAdmin"
            });
            seedCreateRoleResponse.EnsureSuccessStatusCode();

            var seedCreateUserResponse = await PostJsonAsync(seedClient, "/api/management/users", new
            {
                tenant = "tenant-a",
                userName = "a_admin",
                email = "a_admin@tenant-a.local",
                password = "A_admin#2026",
                roleNames = new[] { "TenantAdmin" }
            });
            seedCreateUserResponse.EnsureSuccessStatusCode();
        }

        using var factory = new LoginEnabledWebApplicationFactory(sharedContentRoot, ownsContentRoot: true);
        using var client = CreateClient(factory, allowAutoRedirect: false, handleCookies: true);

        await WaitForReadySummaryAsync(client);

        string tenantAUserId;
        var usersInTenantAResponse = await client.GetAsync("/api/management/users?tenant=tenant-a");
        usersInTenantAResponse.EnsureSuccessStatusCode();
        using (var usersInTenantADocument = await ReadJsonAsync(usersInTenantAResponse))
        {
            tenantAUserId = usersInTenantADocument.RootElement.EnumerateArray().First(x =>
                    string.Equals(x.GetProperty("userName").GetString(), "a_admin", StringComparison.OrdinalIgnoreCase))
                .GetProperty("id")
                .GetString()
                ?? throw new InvalidOperationException("tenant-a user id is missing.");
        }

        // L-01
        var adminLogin = await SubmitLoginAsync(client, "/Login?ReturnUrl=%2FAdmin", "admin", "Admin123!");
        Assert.True(adminLogin.Success, BuildLoginFailureMessage("L-01", adminLogin));

        var adminPageAfterLogin = await client.GetAsync("/Admin");
        Assert.False(RedirectsToLogin(adminPageAfterLogin), "L-01: Admin page should not redirect to login after successful sign-in.");

        // L-05
        await LogOffAsync(client);
        var adminPageAfterLogOff = await client.GetAsync("/Admin");
        Assert.NotEqual(HttpStatusCode.OK, adminPageAfterLogOff.StatusCode);

        // L-02
        var wrongPasswordLogin = await SubmitLoginAsync(client, "/Login?ReturnUrl=%2FAdmin", "admin", "WrongPassword#2026");
        Assert.False(wrongPasswordLogin.Success, "L-02: Wrong password should not log in successfully.");

        // L-03
        var tenantALogin = await SubmitLoginAsync(client, "/tenant-a/Login?ReturnUrl=%2Ftenant-a%2FAdmin", "a_admin", "A_admin#2026");
        Assert.True(tenantALogin.Success, BuildLoginFailureMessage("L-03", tenantALogin));

        var tenantAAdminPageAfterLogin = await client.GetAsync("/tenant-a/Admin");
        Assert.False(RedirectsToLogin(tenantAAdminPageAfterLogin), "L-03: tenant-a admin page should not redirect to login after successful sign-in.");

        // L-04
        var disableUserResponse = await PatchJsonAsync(client, $"/api/management/users/{Uri.EscapeDataString(tenantAUserId)}", new
        {
            tenant = "tenant-a",
            isEnabled = false
        });
        disableUserResponse.EnsureSuccessStatusCode();

        await LogOffAsync(client);
        var disabledUserLogin = await SubmitLoginAsync(client, "/tenant-a/Login?ReturnUrl=%2Ftenant-a%2FAdmin", "a_admin", "A_admin#2026");
        Assert.False(disabledUserLogin.Success, "L-04: Disabled user should not log in successfully.");
    }

    private sealed record LoginAttemptResult(bool Success, HttpStatusCode StatusCode, string Location, string BodySnippet);

    private sealed class LoginEnabledWebApplicationFactory : TestWebApplicationFactory
    {
        public LoginEnabledWebApplicationFactory(string? contentRootPath = null, bool ownsContentRoot = true)
            : base("SaaS.Base", contentRootPath, ownsContentRoot)
        {
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SaaS:DisableAdminPathAccess"] = "false"
                });
            });
        }
    }

    private sealed class ManagementAuthorizedWebApplicationFactory : TestWebApplicationFactory
    {
        public ManagementAuthorizedWebApplicationFactory(
            string recipeName = "SaaS.Base",
            string? contentRootPath = null,
            bool ownsContentRoot = true)
            : base(recipeName, contentRootPath, ownsContentRoot)
        {
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IAuthorizationHandler, AllowAllAuthorizationHandler>();
            });
        }
    }

    private sealed class AllowAllAuthorizationHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, bool allowAutoRedirect, bool handleCookies)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = handleCookies
        });
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, object payload)
    {
        return await client.PostAsync(path, ToJsonContent(payload));
    }

    private static async Task<HttpResponseMessage> PutJsonAsync(HttpClient client, string path, object payload)
    {
        return await client.PutAsync(path, ToJsonContent(payload));
    }

    private static async Task<HttpResponseMessage> PatchJsonAsync(HttpClient client, string path, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = ToJsonContent(payload)
        };

        return await client.SendAsync(request);
    }

    private static StringContent ToJsonContent(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
    }

    private static async Task SetupTenantAsync(
        HttpClient client,
        string tenantName,
        string siteName,
        string userName,
        string email,
        string password)
    {
        var setupResponse = await PostJsonAsync(client, "/api/tenants/setup", new
        {
            name = tenantName,
            siteName,
            databaseProvider = "Sqlite",
            connectionString = string.Empty,
            tablePrefix = string.Empty,
            schema = string.Empty,
            userName,
            email,
            password,
            recipeName = "SaaS.Base",
            siteTimeZone = "UTC"
        });
        if (!setupResponse.IsSuccessStatusCode)
        {
            var responseBody = await setupResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Tenant setup failed for '{tenantName}' with status {(int)setupResponse.StatusCode}: {responseBody}");
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var tenantsResponse = await client.GetAsync("/api/management/tenants");
            tenantsResponse.EnsureSuccessStatusCode();

            using var tenantsDocument = await ReadJsonAsync(tenantsResponse);
            var tenant = tenantsDocument.RootElement.EnumerateArray().FirstOrDefault(x =>
                string.Equals(x.GetProperty("name").GetString(), tenantName, StringComparison.OrdinalIgnoreCase));
            if (tenant.ValueKind != JsonValueKind.Undefined &&
                string.Equals(tenant.GetProperty("state").GetString(), "Running", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Tenant '{tenantName}' did not reach Running state after setup.");
    }

    private static async Task CreateAndSetupTenantAsync(
        HttpClient client,
        string tenantName,
        string requestUrlPrefix,
        string siteName,
        string userName,
        string email,
        string password)
    {
        var createTenantResponse = await PostJsonAsync(client, "/api/management/tenants", new
        {
            name = tenantName,
            requestUrlPrefix
        });
        createTenantResponse.EnsureSuccessStatusCode();

        await SetupTenantAsync(client, tenantName, siteName, userName, email, password);
    }

    private static async Task<LoginAttemptResult> SubmitLoginAsync(HttpClient client, string loginUrl, string userName, string password)
    {
        var loginPageResponse = await client.GetAsync(loginUrl);
        if (loginPageResponse.StatusCode == HttpStatusCode.Redirect ||
            loginPageResponse.StatusCode == HttpStatusCode.Found)
        {
            var redirectLocation = loginPageResponse.Headers.Location?.ToString() ?? string.Empty;
            return new LoginAttemptResult(
                Success: !redirectLocation.Contains("login", StringComparison.OrdinalIgnoreCase),
                StatusCode: loginPageResponse.StatusCode,
                Location: redirectLocation,
                BodySnippet: string.Empty);
        }

        var loginPageHtml = await loginPageResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, loginPageResponse.StatusCode);

        var formAction = ExtractFormAction(loginPageHtml);
        if (string.IsNullOrWhiteSpace(formAction))
        {
            formAction = loginUrl;
        }

        var formValues = BuildLoginFormValues(loginPageHtml, userName, password);
        var postResponse = await client.PostAsync(formAction, new FormUrlEncodedContent(formValues));

        var location = postResponse.Headers.Location?.ToString() ?? string.Empty;
        var postBody = await postResponse.Content.ReadAsStringAsync();
        var success = (postResponse.StatusCode == HttpStatusCode.Redirect || postResponse.StatusCode == HttpStatusCode.Found) &&
                      !location.Contains("login", StringComparison.OrdinalIgnoreCase);

        return new LoginAttemptResult(
            Success: success,
            StatusCode: postResponse.StatusCode,
            Location: location,
            BodySnippet: postBody.Length > 280 ? postBody[..280] : postBody);
    }

    private static async Task LogOffAsync(HttpClient client)
    {
        var paths =
            new[]
            {
                "/Logout",
                "/LogOff",
                "/Account/LogOff",
                "/Users/Account/LogOff",
                "/Users/LogOff"
            };

        foreach (var path in paths)
        {
            foreach (var candidate in new[] { path, path + "?returnUrl=%2F" })
            {
                var logOffResponse = await client.GetAsync(candidate);
                if (logOffResponse.StatusCode == HttpStatusCode.Redirect ||
                    logOffResponse.StatusCode == HttpStatusCode.Found)
                {
                    return;
                }
            }
        }

        var loginPageResponse = await client.GetAsync("/Login");
        var loginPageHtml = await loginPageResponse.Content.ReadAsStringAsync();
        var requestVerificationToken = ExtractRequestVerificationToken(loginPageHtml);
        if (string.IsNullOrWhiteSpace(requestVerificationToken))
        {
            return;
        }

        foreach (var path in paths)
        {
            foreach (var candidate in new[] { path, path + "?returnUrl=%2F" })
            {
                var response = await client.PostAsync(candidate, new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = requestVerificationToken,
                    ["returnUrl"] = "/",
                    ["ReturnUrl"] = "/"
                }));

                if (response.StatusCode == HttpStatusCode.Redirect ||
                    response.StatusCode == HttpStatusCode.Found)
                {
                    return;
                }
            }
        }
    }

    private static bool RedirectsToLogin(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Redirect &&
            response.StatusCode != HttpStatusCode.Found)
        {
            return false;
        }

        var location = response.Headers.Location?.ToString() ?? string.Empty;
        return location.Contains("login", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildLoginFailureMessage(string caseId, LoginAttemptResult result)
    {
        return $"{caseId} login failed. status={(int)result.StatusCode}, location='{result.Location}', body='{result.BodySnippet}'";
    }

    private static string ExtractRequestVerificationToken(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var match = Regex.Match(
            html,
            "name=[\"']__RequestVerificationToken[\"'][^>]*value=[\"']([^\"']+)[\"']",
            RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        match = Regex.Match(
            html,
            "value=[\"']([^\"']+)[\"'][^>]*name=[\"']__RequestVerificationToken[\"']",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static Dictionary<string, string> BuildLoginFormValues(string loginPageHtml, string userName, string password)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inputMatches = Regex.Matches(loginPageHtml, "<input[^>]*>", RegexOptions.IgnoreCase);
        foreach (Match inputMatch in inputMatches)
        {
            var inputHtml = inputMatch.Value;
            var name = ExtractAttribute(inputHtml, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var type = ExtractAttribute(inputHtml, "type");
            var value = ExtractAttribute(inputHtml, "value");

            if (string.Equals(type, "hidden", StringComparison.OrdinalIgnoreCase))
            {
                values[name] = value;
                continue;
            }

            if (name.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                values[name] = password;
                continue;
            }

            if (name.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("login", StringComparison.OrdinalIgnoreCase))
            {
                values[name] = userName;
                continue;
            }

            if (name.Contains("remember", StringComparison.OrdinalIgnoreCase))
            {
                values[name] = "false";
            }
        }

        values.TryAdd("__RequestVerificationToken", ExtractRequestVerificationToken(loginPageHtml));
        values.TryAdd("UserName", userName);
        values.TryAdd("Input.UserName", userName);
        values.TryAdd("Email", userName);
        values.TryAdd("Input.Email", userName);
        values.TryAdd("Password", password);
        values.TryAdd("Input.Password", password);
        values.TryAdd("RememberMe", "false");
        values.TryAdd("Input.RememberMe", "false");

        return values
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static string ExtractFormAction(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var formMatch = Regex.Match(html, "<form[^>]*action=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
        return formMatch.Success ? formMatch.Groups[1].Value : string.Empty;
    }

    private static string ExtractAttribute(string html, string attributeName)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(attributeName))
        {
            return string.Empty;
        }

        var match = Regex.Match(
            html,
            attributeName + "=[\"']([^\"']*)[\"']",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static async Task<JsonElement> WaitForReadySummaryAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var summaryResponse = await client.GetAsync("/api/saas/summary");
            if (summaryResponse.StatusCode == HttpStatusCode.Found)
            {
                await client.GetAsync("/");
                await Task.Delay(300);
                continue;
            }

            summaryResponse.EnsureSuccessStatusCode();
            using var summaryDocument = JsonDocument.Parse(await summaryResponse.Content.ReadAsStringAsync());
            var summary = summaryDocument.RootElement.Clone();
            if (summary.GetProperty("ready").GetBoolean())
            {
                return summary;
            }

            await Task.Delay(300);
        }

        throw new TimeoutException("SaaS summary never became ready after auto-setup retries.");
    }
}
