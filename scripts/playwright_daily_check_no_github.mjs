#!/usr/bin/env node
import { chromium } from "playwright";
import fs from "node:fs";
import path from "node:path";

const MODE_SMOKE = "smoke";
const MODE_FULL = "full";

const baseUrlRaw = process.env.SAAS_BASE_URL ?? "https://pty.addai.vip/saas";
const baseUrl = baseUrlRaw.endsWith("/") ? baseUrlRaw : `${baseUrlRaw}/`;
const runMode = (process.env.SAAS_DAILY_MODE ?? MODE_SMOKE).toLowerCase() === MODE_FULL ? MODE_FULL : MODE_SMOKE;
const cleanupEnabled = String(process.env.SAAS_DAILY_CLEANUP ?? "true").toLowerCase() !== "false";

const adminUsername = process.env.SAAS_ADMIN_USERNAME ?? "admin";
const adminPassword = process.env.SAAS_ADMIN_PASSWORD ?? "Admin123!";

const tenantAdminPassword = process.env.SAAS_TENANT_ADMIN_PASSWORD ?? "Admin123!";

const stamp = new Date().toISOString().replace(/[:.]/g, "-");
const token = stamp.replace(/[-TZ]/g, "").slice(0, 14);
const outputDir = path.resolve(
  process.env.SAAS_DAILY_OUTPUT_DIR ?? `./artifacts/saas-daily-check-${token}`
);
fs.mkdirSync(outputDir, { recursive: true });

const names = {
  lifecycle: process.env.SAAS_TENANT_LIFECYCLE ?? `tenant-lc-${token}`,
  tenantA: process.env.SAAS_TENANT_A ?? `tenant-a-${token}`,
  tenantB: process.env.SAAS_TENANT_B ?? `tenant-b-${token}`,
  uninitialized: process.env.SAAS_TENANT_UNINIT ?? `tenant-uninit-${token}`,
  runningBlock: process.env.SAAS_TENANT_RUNNING_BLOCK ?? `tenant-running-${token}`,
  profile: process.env.SAAS_FEATURE_PROFILE ?? `DailyProfile${token}`,
  roleShared: process.env.SAAS_ROLE_SHARED ?? `TenantAdmin${token}`,
  roleOnlyInB: process.env.SAAS_ROLE_ONLY_IN_B ?? `OnlyInB${token}`,
  userA: process.env.SAAS_USER_A ?? `aadmin${token}`,
  userB: process.env.SAAS_USER_B ?? `badmin${token}`,
};

const prefixes = {
  lifecycle: `lc-${token}`,
  tenantA: `a-${token}`,
  tenantB: `b-${token}`,
  uninitialized: `u-${token}`,
  runningBlock: `r-${token}`,
};

const state = {
  tenants: {
    lifecycle: null,
    tenantA: null,
    tenantB: null,
    uninitialized: null,
    runningBlock: null,
  },
  roles: {
    tenantA: [],
    tenantB: [],
  },
  users: {
    tenantA: [],
    tenantB: [],
  },
  featureToggleCandidate: null,
  alwaysEnabledCandidate: null,
  adminPage: null,
};

const results = [];

function nowIso() {
  return new Date().toISOString();
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function asArray(value) {
  return Array.isArray(value) ? value : [];
}

function normalizePathPart(part) {
  return String(part ?? "").replace(/^\/+/, "");
}

function appUrl(relativePath = "") {
  return new URL(normalizePathPart(relativePath), baseUrl).toString();
}

function tenantUrl(prefix, relativePath = "") {
  const p = String(prefix ?? "").replace(/^\/+|\/+$/g, "");
  const relative = normalizePathPart(relativePath);
  return appUrl(p ? `${p}/${relative}` : relative);
}

function tenantAdminEmail(tenantName) {
  return `${tenantName}@daily-check.local`;
}

function userEmail(userName) {
  return `${userName}@daily-check.local`;
}

function containsIgnoreCase(list, value) {
  const target = String(value ?? "").toLowerCase();
  return asArray(list).some((item) => String(item ?? "").toLowerCase() === target);
}

function assertOrThrow(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

function recordResult({ id, name, status, note, stage, details }) {
  results.push({
    id,
    name,
    status,
    note,
    stage,
    details: details ?? null,
    timestampUtc: nowIso(),
  });
}

async function runCase({ id, name, stage, fullOnly = false }, testFn) {
  if (fullOnly && runMode !== MODE_FULL) {
    recordResult({
      id,
      name,
      stage,
      status: "skipped",
      note: "Skipped in smoke mode",
    });
    return;
  }

  try {
    const details = await testFn();
    recordResult({
      id,
      name,
      stage,
      status: "passed",
      note: "OK",
      details,
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    recordResult({
      id,
      name,
      stage,
      status: "failed",
      note: message,
    });
  }
}

async function apiCall(request, method, relativePath, payload) {
  const response = await request.fetch(appUrl(relativePath), {
    method,
    data: payload,
    timeout: 30000,
  });

  const status = response.status();
  const text = await response.text();
  let body = null;

  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = text;
    }
  }

  return { status, body, text };
}

async function ensureReady(request) {
  for (let i = 0; i < 25; i += 1) {
    const summary = await apiCall(request, "GET", "api/saas/summary");
    if (summary.status === 200 && summary.body?.ready === true) {
      return summary.body;
    }
    await sleep(700);
  }

  throw new Error("SaaS summary did not become ready");
}

async function listTenants(request) {
  const response = await apiCall(request, "GET", "api/management/tenants");
  assertOrThrow(response.status === 200 && Array.isArray(response.body), "Failed to list tenants");
  return response.body;
}

async function findTenant(request, tenantName) {
  const tenants = await listTenants(request);
  return tenants.find((x) => x?.name === tenantName) ?? null;
}

async function waitForTenantState(request, tenantName, expectedState, timeoutMs = 90000) {
  const deadline = Date.now() + timeoutMs;
  const expected = expectedState.toLowerCase();

  while (Date.now() < deadline) {
    const tenant = await findTenant(request, tenantName);
    if (tenant && String(tenant.state ?? "").toLowerCase() === expected) {
      return tenant;
    }
    await sleep(1200);
  }

  throw new Error(`Tenant ${tenantName} did not become ${expectedState}`);
}

async function createTenant(request, name, prefix, recipeName = "SaaS.Base") {
  const payload = {
    name,
    requestUrlPrefix: prefix,
    requestUrlHost: "",
    category: "daily-check",
    description: `created by daily automation ${token}`,
    recipeName,
    databaseProvider: "Sqlite",
  };

  const response = await apiCall(request, "POST", "api/management/tenants", payload);

  if (response.status === 201) {
    return response.body;
  }

  if (response.status === 409) {
    const existing = await findTenant(request, name);
    if (existing) {
      return existing;
    }
  }

  throw new Error(`Create tenant ${name} failed: status=${response.status}`);
}

async function patchTenant(request, name, payload) {
  const response = await apiCall(request, "PATCH", `api/management/tenants/${encodeURIComponent(name)}`, payload);
  return response;
}

async function setupTenant(authenticatedRequest, tenantName) {
  const payload = {
    name: tenantName,
    siteName: `${tenantName} Site`,
    databaseProvider: "Sqlite",
    connectionString: "",
    tablePrefix: "",
    schema: "",
    userName: `owner${token}`,
    email: tenantAdminEmail(tenantName),
    password: tenantAdminPassword,
    recipeName: "SaaS.Base",
    siteTimeZone: "UTC",
  };

  const response = await apiCall(authenticatedRequest, "POST", "api/tenants/setup", payload);
  if (response.status !== 200 && response.status !== 201) {
    throw new Error(`Setup tenant ${tenantName} failed: status=${response.status}`);
  }

  return response;
}

async function listFeatures(request, tenant) {
  const path = tenant
    ? `api/management/features?tenant=${encodeURIComponent(tenant)}`
    : "api/management/features";
  const response = await apiCall(request, "GET", path);
  assertOrThrow(response.status === 200 && Array.isArray(response.body?.features), "Failed to load features");
  return response.body;
}

async function updateFeatures(request, payload) {
  return apiCall(request, "PUT", "api/management/features", payload);
}

async function listFeatureProfiles(request) {
  const response = await apiCall(request, "GET", "api/management/feature-profiles");
  assertOrThrow(response.status === 200 && Array.isArray(response.body), "Failed to list feature profiles");
  return response.body;
}

async function upsertFeatureProfile(request, payload) {
  return apiCall(request, "PUT", "api/management/feature-profiles", payload);
}

async function listPermissions(request, tenant) {
  const path = tenant
    ? `api/management/permissions?tenant=${encodeURIComponent(tenant)}`
    : "api/management/permissions";
  const response = await apiCall(request, "GET", path);
  assertOrThrow(response.status === 200 && Array.isArray(response.body), "Failed to list permissions");
  return response.body;
}

async function createRole(request, tenant, name, permissionNames = []) {
  const response = await apiCall(request, "POST", "api/management/roles", {
    tenant,
    name,
    permissionNames,
  });

  assertOrThrow(response.status === 201, `Create role ${name} failed: status=${response.status}`);
  return response.body;
}

async function patchRole(request, tenant, id, payload) {
  return apiCall(request, "PATCH", `api/management/roles/${encodeURIComponent(id)}`, {
    tenant,
    ...payload,
  });
}

async function updateRolePermissions(request, tenant, roleId, permissionNames) {
  return apiCall(request, "PUT", `api/management/roles/${encodeURIComponent(roleId)}/permissions`, {
    tenant,
    permissionNames,
  });
}

async function listRoles(request, tenant) {
  const path = tenant
    ? `api/management/roles?tenant=${encodeURIComponent(tenant)}`
    : "api/management/roles";
  const response = await apiCall(request, "GET", path);
  assertOrThrow(response.status === 200 && Array.isArray(response.body), "Failed to list roles");
  return response.body;
}

async function createUser(request, tenant, userName, password, roleNames) {
  const response = await apiCall(request, "POST", "api/management/users", {
    tenant,
    userName,
    email: userEmail(userName),
    password,
    isEnabled: true,
    roleNames,
  });

  assertOrThrow(response.status === 201, `Create user ${userName} failed: status=${response.status}`);
  return response.body;
}

async function patchUser(request, tenant, id, payload) {
  return apiCall(request, "PATCH", `api/management/users/${encodeURIComponent(id)}`, {
    tenant,
    ...payload,
  });
}

async function listUsers(request, tenant) {
  const path = tenant
    ? `api/management/users?tenant=${encodeURIComponent(tenant)}`
    : "api/management/users";
  const response = await apiCall(request, "GET", path);
  assertOrThrow(response.status === 200 && Array.isArray(response.body), "Failed to list users");
  return response.body;
}

async function uiLogin(context, { tenantPrefix = "", username, password, expectSuccess, tag }) {
  const page = await context.newPage();
  const loginUrl = tenantUrl(tenantPrefix, "Login");

  await page.goto(loginUrl, { waitUntil: "domcontentloaded", timeout: 30000 });

  const userInput = page.locator("input[name='UserName'], input[type='text']").first();
  const passwordInput = page.locator("input[name='Password'], input[type='password']").first();

  await userInput.fill(username);
  await passwordInput.fill(password);

  await page.locator("form button[type='submit']").first().click();
  await page.waitForLoadState("domcontentloaded", { timeout: 30000 }).catch(() => {});
  await sleep(900);

  const current = page.url();
  const isOnLogin = new URL(current).pathname.toLowerCase().includes("/login");

  const screenshot = path.join(outputDir, `${tag}.png`);
  await page.screenshot({ path: screenshot, fullPage: true });

  if (expectSuccess) {
    assertOrThrow(!isOnLogin, `Expected login success but stayed on Login: ${current}`);
  } else {
    assertOrThrow(isOnLogin, `Expected login failure but left Login page: ${current}`);
  }

  return { page, current, screenshot };
}

async function uiLogoutAndVerify(page, tenantPrefix = "") {
  const logoutForm = page.locator("form[action*='Users/LogOff']").first();
  const formCount = await logoutForm.count();

  if (formCount > 0) {
    await logoutForm.locator("button[type='submit']").first().click();
    await page.waitForLoadState("domcontentloaded", { timeout: 30000 }).catch(() => {});
  } else {
    const fallback = tenantUrl(tenantPrefix, "Users/LogOff");
    const tokenInput = page.locator("input[name='__RequestVerificationToken']").first();
    const hasToken = await tokenInput.count();
    const tokenValue = hasToken > 0 ? await tokenInput.inputValue() : "";

    await page.request.post(fallback, {
      form: tokenValue ? { __RequestVerificationToken: tokenValue } : {},
      timeout: 30000,
    });
  }

  const protectedUrl = tenantUrl(tenantPrefix, "Users/ChangePassword");
  await page.goto(protectedUrl, { waitUntil: "domcontentloaded", timeout: 30000 });
  const redirectedToLogin = new URL(page.url()).pathname.toLowerCase().includes("/login");
  assertOrThrow(redirectedToLogin, "Expected redirect to Login after logout");
}

function summarize() {
  const passed = results.filter((x) => x.status === "passed").length;
  const failed = results.filter((x) => x.status === "failed").length;
  const skipped = results.filter((x) => x.status === "skipped").length;
  return { passed, failed, skipped, total: results.length };
}

async function cleanupArtifacts(request) {
  if (!cleanupEnabled) {
    return;
  }

  const cleanupLog = [];

  const removeUserIfExists = async (tenant, userId) => {
    if (!tenant || !userId) {
      return;
    }

    try {
      const response = await patchUser(request, tenant, userId, { operation: "remove" });
      cleanupLog.push({ type: "user", tenant, userId, status: response.status });
    } catch (error) {
      cleanupLog.push({ type: "user", tenant, userId, status: "error", error: String(error) });
    }
  };

  const removeRoleIfExists = async (tenant, roleId) => {
    if (!tenant || !roleId) {
      return;
    }

    try {
      const response = await patchRole(request, tenant, roleId, { operation: "remove" });
      cleanupLog.push({ type: "role", tenant, roleId, status: response.status });
    } catch (error) {
      cleanupLog.push({ type: "role", tenant, roleId, status: "error", error: String(error) });
    }
  };

  for (const user of state.users.tenantA) {
    await removeUserIfExists(state.tenants.tenantA?.name, user.id);
  }
  for (const user of state.users.tenantB) {
    await removeUserIfExists(state.tenants.tenantB?.name, user.id);
  }

  for (const role of state.roles.tenantA) {
    await removeRoleIfExists(state.tenants.tenantA?.name, role.id);
  }
  for (const role of state.roles.tenantB) {
    await removeRoleIfExists(state.tenants.tenantB?.name, role.id);
  }

  try {
    await upsertFeatureProfile(request, { id: names.profile, delete: true });
    cleanupLog.push({ type: "profile", id: names.profile, status: 200 });
  } catch {
    cleanupLog.push({ type: "profile", id: names.profile, status: "skip" });
  }

  const removeTenantIfExists = async (tenantName) => {
    if (!tenantName) {
      return;
    }

    const existing = await findTenant(request, tenantName);
    if (!existing || existing.isDefault) {
      return;
    }

    if (String(existing.state) === "Running") {
      await patchTenant(request, tenantName, { enabled: false });
      await waitForTenantState(request, tenantName, "Disabled", 45000);
    }

    await patchTenant(request, tenantName, { operation: "remove" });
    cleanupLog.push({ type: "tenant", tenant: tenantName, status: "removed" });
  };

  await removeTenantIfExists(state.tenants.tenantA?.name);
  await removeTenantIfExists(state.tenants.tenantB?.name);
  await removeTenantIfExists(state.tenants.uninitialized?.name);
  await removeTenantIfExists(state.tenants.runningBlock?.name);
  await removeTenantIfExists(state.tenants.lifecycle?.name);

  fs.writeFileSync(
    path.join(outputDir, "cleanup.json"),
    JSON.stringify(cleanupLog, null, 2),
    "utf8"
  );
}

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({
  ignoreHTTPSErrors: true,
  viewport: { width: 1440, height: 900 },
});

try {
  await runCase(
    { id: "P-01", name: "SaaS summary ready", stage: "prepare" },
    async () => {
      const summary = await ensureReady(context.request);
      return { tenantCount: summary.tenantCount, defaultTenantState: summary.defaultTenantState };
    }
  );

  await runCase(
    { id: "P-02", name: "Default tenant exists", stage: "prepare" },
    async () => {
      const tenants = await listTenants(context.request);
      assertOrThrow(tenants.some((x) => x.name === "Default"), "Default tenant not found");
      return { tenantCount: tenants.length };
    }
  );

  await runCase(
    { id: "L-01", name: "Default admin login succeeds", stage: "login" },
    async () => {
      const login = await uiLogin(context, {
        username: adminUsername,
        password: adminPassword,
        expectSuccess: true,
        tag: "L-01-admin-login",
      });
      state.adminPage = login.page;
      return { finalUrl: login.current, screenshot: login.screenshot };
    }
  );

  await runCase(
    { id: "L-02", name: "Wrong password login fails", stage: "login" },
    async () => {
      const isolated = await browser.newContext({ ignoreHTTPSErrors: true });
      try {
        const login = await uiLogin(isolated, {
          username: adminUsername,
          password: `${adminPassword}_wrong`,
          expectSuccess: false,
          tag: "L-02-wrong-password",
        });
        await login.page.close();
        return { finalUrl: login.current, screenshot: login.screenshot };
      } finally {
        await isolated.close();
      }
    }
  );

  const canUseTenantSetupApi = !!state.adminPage;

  const ensureOperationalTenant = async (key, name, prefix) => {
    state.tenants[key] = await createTenant(context.request, name, prefix);

    if (!canUseTenantSetupApi) {
      throw new Error("Admin login unavailable; cannot call /api/tenants/setup for cross-tenant checks");
    }

    await setupTenant(context.request, name);
    state.tenants[key] = await waitForTenantState(context.request, name, "Running", 90000);
    return state.tenants[key];
  };

  await runCase(
    { id: "T-01", name: "Create lifecycle tenant", stage: "tenant" },
    async () => {
      const tenant = await createTenant(context.request, names.lifecycle, prefixes.lifecycle);
      state.tenants.lifecycle = tenant;
      return { name: tenant.name, state: tenant.state };
    }
  );

  await runCase(
    { id: "T-02", name: "Duplicate tenant name blocked", stage: "tenant", fullOnly: true },
    async () => {
      const response = await apiCall(context.request, "POST", "api/management/tenants", {
        name: names.lifecycle,
        requestUrlPrefix: `${prefixes.lifecycle}-dup`,
        recipeName: "SaaS.Base",
      });
      assertOrThrow(response.status === 409, `Expected 409, got ${response.status}`);
      return { status: response.status };
    }
  );

  await runCase(
    { id: "T-03", name: "Empty tenant name validation", stage: "tenant", fullOnly: true },
    async () => {
      const response = await apiCall(context.request, "POST", "api/management/tenants", {
        name: "   ",
        requestUrlPrefix: `empty-${token}`,
      });
      assertOrThrow(response.status === 400, `Expected 400, got ${response.status}`);
      return { status: response.status };
    }
  );

  await runCase(
    { id: "T-04", name: "Patch tenant description/category", stage: "tenant" },
    async () => {
      const response = await patchTenant(context.request, names.lifecycle, {
        category: "daily-check-updated",
        description: `updated-${token}`,
      });
      assertOrThrow(response.status === 200, `Expected 200, got ${response.status}`);
      return { status: response.status, tenant: response.body?.name };
    }
  );

  await runCase(
    { id: "T-05", name: "Disable running tenant", stage: "tenant" },
    async () => {
      if (!canUseTenantSetupApi) {
        throw new Error("Admin login unavailable; cannot setup lifecycle tenant");
      }

      await setupTenant(context.request, names.lifecycle);
      await waitForTenantState(context.request, names.lifecycle, "Running", 90000);

      const disableResponse = await patchTenant(context.request, names.lifecycle, { enabled: false });
      assertOrThrow(disableResponse.status === 200, `Expected 200, got ${disableResponse.status}`);

      state.tenants.lifecycle = await waitForTenantState(context.request, names.lifecycle, "Disabled", 90000);
      return { state: state.tenants.lifecycle.state };
    }
  );

  await runCase(
    { id: "T-06", name: "Enable uninitialized tenant blocked", stage: "tenant", fullOnly: true },
    async () => {
      state.tenants.uninitialized = await createTenant(context.request, names.uninitialized, prefixes.uninitialized);
      const response = await patchTenant(context.request, names.uninitialized, { enabled: true });
      assertOrThrow(response.status === 400, `Expected 400, got ${response.status}`);
      return { status: response.status };
    }
  );

  await runCase(
    { id: "T-07", name: "Remove running tenant blocked", stage: "tenant", fullOnly: true },
    async () => {
      const tenant = await ensureOperationalTenant("runningBlock", names.runningBlock, prefixes.runningBlock);
      const response = await patchTenant(context.request, tenant.name, { operation: "remove" });
      assertOrThrow(response.status === 400, `Expected 400, got ${response.status}`);
      return { status: response.status };
    }
  );

  await runCase(
    { id: "T-08", name: "Remove disabled tenant succeeds", stage: "tenant" },
    async () => {
      const response = await patchTenant(context.request, names.lifecycle, { operation: "remove" });
      assertOrThrow(response.status === 200, `Expected 200, got ${response.status}`);
      state.tenants.lifecycle = null;
      return { removed: response.body?.removed ?? names.lifecycle };
    }
  );

  await runCase(
    { id: "T-09", name: "Remove default tenant blocked", stage: "tenant" },
    async () => {
      const response = await patchTenant(context.request, "Default", { operation: "remove" });
      assertOrThrow(response.status === 400, `Expected 400, got ${response.status}`);
      return { status: response.status };
    }
  );

  await runCase(
    { id: "UR-PRE", name: "Prepare tenant-a and tenant-b", stage: "users" },
    async () => {
      const tenantA = await ensureOperationalTenant("tenantA", names.tenantA, prefixes.tenantA);
      const tenantB = await ensureOperationalTenant("tenantB", names.tenantB, prefixes.tenantB);
      return {
        tenantA: { name: tenantA.name, prefix: tenantA.requestUrlPrefix, state: tenantA.state },
        tenantB: { name: tenantB.name, prefix: tenantB.requestUrlPrefix, state: tenantB.state },
      };
    }
  );

  await runCase(
    { id: "F-01", name: "Load features list", stage: "feature" },
    async () => {
      const payload = await listFeatures(context.request, "Default");
      state.featureToggleCandidate = payload.features.find(
        (x) => !x.enabled && !x.isAlwaysEnabled && !x.enabledByDependencyOnly
      ) || null;
      state.alwaysEnabledCandidate = payload.features.find((x) => x.isAlwaysEnabled) || null;
      assertOrThrow(payload.features.length > 0, "Feature list is empty");
      return {
        total: payload.features.length,
        toggleCandidate: state.featureToggleCandidate?.id ?? null,
        alwaysEnabled: state.alwaysEnabledCandidate?.id ?? null,
      };
    }
  );

  await runCase(
    { id: "F-02", name: "Enable one non-always feature", stage: "feature" },
    async () => {
      const candidate = state.featureToggleCandidate?.id;
      assertOrThrow(candidate, "No toggle candidate found");

      const response = await updateFeatures(context.request, {
        enable: [candidate],
        disable: [],
        force: true,
      });

      assertOrThrow(response.status === 200, `Expected 200, got ${response.status}`);
      assertOrThrow(containsIgnoreCase(response.body?.changed?.enabled, candidate), "Enabled list does not contain candidate");
      return { candidate };
    }
  );

  await runCase(
    { id: "F-03", name: "Disable previously enabled feature", stage: "feature" },
    async () => {
      const candidate = state.featureToggleCandidate?.id;
      assertOrThrow(candidate, "No toggle candidate found");

      const response = await updateFeatures(context.request, {
        enable: [],
        disable: [candidate],
        force: true,
      });

      assertOrThrow(response.status === 200, `Expected 200, got ${response.status}`);
      assertOrThrow(containsIgnoreCase(response.body?.changed?.disabled, candidate), "Disabled list does not contain candidate");
      return { candidate };
    }
  );

  await runCase(
    { id: "F-04", name: "Disable always-enabled feature blocked", stage: "feature" },
    async () => {
      const candidate = state.alwaysEnabledCandidate?.id;
      assertOrThrow(candidate, "No always-enabled feature found");

      const response = await updateFeatures(context.request, {
        enable: [],
        disable: [candidate],
        force: true,
      });

      assertOrThrow(response.status === 400, `Expected 400, got ${response.status}`);
      return { candidate };
    }
  );

  await runCase(
    { id: "F-05", name: "Unknown feature id blocked", stage: "feature" },
    async () => {
      const unknown = `Daily.Unknown.Feature.${token}`;
      const response = await updateFeatures(context.request, {
        enable: [unknown],
        disable: [],
        force: true,
      });

      assertOrThrow(response.status === 400, `Expected 400, got ${response.status}`);
      return { unknown };
    }
  );

  await runCase(
    { id: "F-06", name: "Create feature profile", stage: "feature", fullOnly: true },
    async () => {
      const response = await upsertFeatureProfile(context.request, {
        id: names.profile,
        name: names.profile,
        featureRules: [
          {
            rule: "OrchardCore.Markdown",
            expression: "disabled",
          },
        ],
      });

      assertOrThrow(response.status === 200, `Expected 200, got ${response.status}`);
      return { profileId: names.profile };
    }
  );

  await runCase(
    { id: "F-07", name: "Assign feature profile to tenant-b", stage: "feature", fullOnly: true },
    async () => {
      const tenantName = state.tenants.tenantB?.name;
      assertOrThrow(tenantName, "tenant-b is not prepared");

      const patchResponse = await patchTenant(context.request, tenantName, {
        featureProfiles: [names.profile],
      });
      assertOrThrow(patchResponse.status === 200, `Expected 200, got ${patchResponse.status}`);

      const profiles = await listFeatureProfiles(context.request);
      const profile = profiles.find((x) => x.id === names.profile);
      assertOrThrow(profile, "Feature profile missing after assignment");
      assertOrThrow(containsIgnoreCase(profile.assignedTenants, tenantName), "tenant-b is not assigned");
      return { profileId: names.profile, tenant: tenantName };
    }
  );

  await runCase(
    { id: "F-08", name: "Delete feature profile", stage: "feature", fullOnly: true },
    async () => {
      const response = await upsertFeatureProfile(context.request, {
        id: names.profile,
        delete: true,
      });

      assertOrThrow(response.status === 200, `Expected 200, got ${response.status}`);
      return { removed: names.profile };
    }
  );

  await runCase(
    { id: "F-09", name: "Update features on non-running tenant blocked", stage: "feature", fullOnly: true },
    async () => {
      if (!state.tenants.uninitialized) {
        state.tenants.uninitialized = await createTenant(context.request, names.uninitialized, prefixes.uninitialized);
      }

      const response = await updateFeatures(context.request, {
        tenant: names.uninitialized,
        enable: ["OrchardCore.Markdown"],
        disable: [],
        force: true,
      });

      assertOrThrow(response.status === 400, `Expected 400, got ${response.status}`);
      return { tenant: names.uninitialized };
    }
  );

  await runCase(
    { id: "UR-01", name: "Create role in tenant-a", stage: "users" },
    async () => {
      const tenant = state.tenants.tenantA?.name;
      assertOrThrow(tenant, "tenant-a missing");
      const role = await createRole(context.request, tenant, names.roleShared, []);
      state.roles.tenantA.push(role);
      return { roleId: role.id, name: role.name };
    }
  );

  await runCase(
    { id: "UR-02", name: "Create same role name in tenant-b", stage: "users" },
    async () => {
      const tenant = state.tenants.tenantB?.name;
      assertOrThrow(tenant, "tenant-b missing");
      const role = await createRole(context.request, tenant, names.roleShared, []);
      state.roles.tenantB.push(role);
      return { roleId: role.id, name: role.name };
    }
  );

  await runCase(
    { id: "UR-03", name: "Assign permissions to tenant-a role", stage: "users", fullOnly: true },
    async () => {
      const tenant = state.tenants.tenantA?.name;
      const roleId = state.roles.tenantA[0]?.id;
      assertOrThrow(tenant && roleId, "tenant-a role missing");

      const permissions = await listPermissions(context.request, tenant);
      const permissionName = permissions[0]?.name;
      assertOrThrow(permissionName, "No permission available");

      const response = await updateRolePermissions(context.request, tenant, roleId, [permissionName]);
      assertOrThrow(response.status === 200, `Expected 200, got ${response.status}`);
      assertOrThrow(containsIgnoreCase(response.body?.permissionNames, permissionName), "Permission not applied to role");
      return { roleId, permissionName };
    }
  );

  await runCase(
    { id: "UR-04", name: "Create user in tenant-a", stage: "users" },
    async () => {
      const tenant = state.tenants.tenantA?.name;
      assertOrThrow(tenant, "tenant-a missing");

      const user = await createUser(context.request, tenant, names.userA, tenantAdminPassword, [names.roleShared]);
      state.users.tenantA.push(user);
      return { userId: user.id, userName: user.userName };
    }
  );

  await runCase(
    { id: "UR-05", name: "Create user in tenant-b", stage: "users" },
    async () => {
      const tenant = state.tenants.tenantB?.name;
      assertOrThrow(tenant, "tenant-b missing");

      const user = await createUser(context.request, tenant, names.userB, tenantAdminPassword, [names.roleShared]);
      state.users.tenantB.push(user);

      const onlyInB = await createRole(context.request, tenant, names.roleOnlyInB, []);
      state.roles.tenantB.push(onlyInB);

      return { userId: user.id, userName: user.userName, extraRole: onlyInB.name };
    }
  );

  await runCase(
    { id: "UR-06", name: "Cross-tenant user isolation", stage: "users" },
    async () => {
      const tenantA = state.tenants.tenantA?.name;
      const tenantB = state.tenants.tenantB?.name;
      assertOrThrow(tenantA && tenantB, "tenant-a or tenant-b missing");

      const usersA = await listUsers(context.request, tenantA);
      const usersB = await listUsers(context.request, tenantB);

      assertOrThrow(usersA.some((x) => x.userName === names.userA), "tenant-a user missing in tenant-a list");
      assertOrThrow(usersB.some((x) => x.userName === names.userB), "tenant-b user missing in tenant-b list");
      assertOrThrow(!usersA.some((x) => x.userName === names.userB), "tenant-b user leaked into tenant-a list");
      assertOrThrow(!usersB.some((x) => x.userName === names.userA), "tenant-a user leaked into tenant-b list");

      return { usersInA: usersA.length, usersInB: usersB.length };
    }
  );

  await runCase(
    { id: "UR-07", name: "Unknown role assignment blocked", stage: "users" },
    async () => {
      const tenant = state.tenants.tenantA?.name;
      const userId = state.users.tenantA[0]?.id;
      assertOrThrow(tenant && userId, "tenant-a user missing");

      const response = await patchUser(context.request, tenant, userId, {
        roleNames: [names.roleOnlyInB],
      });

      assertOrThrow(response.status === 400, `Expected 400, got ${response.status}`);
      return { status: response.status };
    }
  );

  await runCase(
    { id: "L-03", name: "Tenant-a local account login succeeds", stage: "login" },
    async () => {
      const prefix = state.tenants.tenantA?.requestUrlPrefix;
      assertOrThrow(prefix, "tenant-a prefix missing");

      const isolated = await browser.newContext({ ignoreHTTPSErrors: true });
      try {
        const login = await uiLogin(isolated, {
          tenantPrefix: prefix,
          username: names.userA,
          password: tenantAdminPassword,
          expectSuccess: true,
          tag: "L-03-tenant-a-login",
        });
        await login.page.close();
        return { finalUrl: login.current, screenshot: login.screenshot };
      } finally {
        await isolated.close();
      }
    }
  );

  await runCase(
    { id: "UR-08", name: "Disable user in tenant-a", stage: "users", fullOnly: true },
    async () => {
      const tenant = state.tenants.tenantA?.name;
      const userId = state.users.tenantA[0]?.id;
      assertOrThrow(tenant && userId, "tenant-a user missing");

      const response = await patchUser(context.request, tenant, userId, {
        isEnabled: false,
      });

      assertOrThrow(response.status === 200, `Expected 200, got ${response.status}`);
      assertOrThrow(response.body?.isEnabled === false, "User still enabled");
      return { userId };
    }
  );

  await runCase(
    { id: "L-04", name: "Disabled user login fails", stage: "login" },
    async () => {
      const tenant = state.tenants.tenantA?.name;
      const userId = state.users.tenantA[0]?.id;
      const prefix = state.tenants.tenantA?.requestUrlPrefix;
      assertOrThrow(tenant && userId && prefix, "tenant-a user or prefix missing");

      const disableResponse = await patchUser(context.request, tenant, userId, { isEnabled: false });
      assertOrThrow(disableResponse.status === 200, `Disable failed: ${disableResponse.status}`);

      const isolated = await browser.newContext({ ignoreHTTPSErrors: true });
      try {
        const login = await uiLogin(isolated, {
          tenantPrefix: prefix,
          username: names.userA,
          password: tenantAdminPassword,
          expectSuccess: false,
          tag: "L-04-disabled-user-login",
        });
        await login.page.close();
        return { finalUrl: login.current, screenshot: login.screenshot };
      } finally {
        await isolated.close();
      }
    }
  );

  await runCase(
    { id: "UR-09", name: "Delete role and verify cleanup", stage: "users", fullOnly: true },
    async () => {
      const tenant = state.tenants.tenantB?.name;
      const role = state.roles.tenantB.find((x) => x.name === names.roleOnlyInB) ?? state.roles.tenantB[0];
      assertOrThrow(tenant && role?.id, "tenant-b role missing");

      const response = await patchRole(context.request, tenant, role.id, { operation: "remove" });
      assertOrThrow(response.status === 200, `Expected 200, got ${response.status}`);

      const roles = await listRoles(context.request, tenant);
      assertOrThrow(!roles.some((x) => x.id === role.id), "Role still exists after delete");
      return { removedRoleId: role.id };
    }
  );

  await runCase(
    { id: "L-05", name: "Logout then access protected page redirects to login", stage: "login" },
    async () => {
      assertOrThrow(state.adminPage, "Admin page unavailable for logout test");
      await uiLogoutAndVerify(state.adminPage);
      const screenshot = path.join(outputDir, "L-05-post-logout.png");
      await state.adminPage.screenshot({ path: screenshot, fullPage: true });
      return { finalUrl: state.adminPage.url(), screenshot };
    }
  );

  await runCase(
    { id: "P-03", name: "Post-run snapshots", stage: "finalize" },
    async () => {
      const summary = await apiCall(context.request, "GET", "api/saas/summary");
      const tenants = await apiCall(context.request, "GET", "api/management/tenants");
      const features = await apiCall(context.request, "GET", "api/management/features?tenant=Default");

      assertOrThrow(summary.status === 200, `summary status=${summary.status}`);
      assertOrThrow(tenants.status === 200, `tenants status=${tenants.status}`);
      assertOrThrow(features.status === 200, `features status=${features.status}`);

      const snapshot = {
        summary: summary.body,
        tenants: tenants.body,
        features: {
          tenant: features.body?.tenant,
          updatedAtUtc: features.body?.updatedAtUtc,
          count: Array.isArray(features.body?.features) ? features.body.features.length : 0,
        },
      };

      fs.writeFileSync(path.join(outputDir, "snapshot.json"), JSON.stringify(snapshot, null, 2), "utf8");
      return { snapshotFile: path.join(outputDir, "snapshot.json") };
    }
  );
} finally {
  await cleanupArtifacts(context.request).catch(() => {});

  if (state.adminPage) {
    await state.adminPage.close().catch(() => {});
  }

  await context.close();
  await browser.close();
}

const summary = summarize();
const summaryPath = path.join(outputDir, "summary.json");
fs.writeFileSync(
  summaryPath,
  JSON.stringify(
    {
      mode: runMode,
      baseUrl,
      cleanupEnabled,
      generatedAtUtc: nowIso(),
      names,
      counts: summary,
      results,
    },
    null,
    2
  ),
  "utf8"
);

const lines = [];
lines.push(`# Daily Checklist Automation (${runMode})`);
lines.push("");
lines.push(`- GeneratedAtUtc: ${nowIso()}`);
lines.push(`- BaseUrl: ${baseUrl}`);
lines.push(`- CleanupEnabled: ${cleanupEnabled}`);
lines.push(`- Passed: ${summary.passed}`);
lines.push(`- Failed: ${summary.failed}`);
lines.push(`- Skipped: ${summary.skipped}`);
lines.push("");
lines.push("| ID | Name | Status | Stage | Note |");
lines.push("|---|---|---|---|---|");
for (const item of results) {
  lines.push(`| ${item.id} | ${item.name} | ${item.status} | ${item.stage} | ${String(item.note).replace(/\|/g, "\\|")} |`);
}

const reportPath = path.join(outputDir, "summary.md");
fs.writeFileSync(reportPath, lines.join("\n"), "utf8");

console.log(`Daily checklist automation done. mode=${runMode}`);
console.log(`passed=${summary.passed}, failed=${summary.failed}, skipped=${summary.skipped}`);
console.log(`summary json: ${summaryPath}`);
console.log(`summary md : ${reportPath}`);

for (const item of results) {
  const flag = item.status === "passed" ? "PASS" : item.status === "failed" ? "FAIL" : "SKIP";
  console.log(`${flag} | ${item.id} | ${item.name} | ${item.note}`);
}

if (summary.failed > 0) {
  process.exit(1);
}
