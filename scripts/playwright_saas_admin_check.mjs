#!/usr/bin/env node
import { chromium } from "playwright";
import fs from "node:fs";
import path from "node:path";

const frontendBaseUrlRaw = process.env.SAAS_BASE_URL ?? "https://pty.addai.vip/saas";
const frontendBaseUrl = frontendBaseUrlRaw.endsWith("/") ? frontendBaseUrlRaw : `${frontendBaseUrlRaw}/`;

const timestamp = new Date().toISOString().replace(/[:.]/g, "-");
const outputDir = path.resolve(
  process.env.SAAS_CHECK_OUTPUT_DIR ?? `./artifacts/saas-headless-check-${timestamp}`
);
fs.mkdirSync(outputDir, { recursive: true });

const frontendChecks = [
  { name: "控制台总览", path: "" },
  { name: "租户管理页", path: "tenants" },
  { name: "功能管理页", path: "features" },
  { name: "功能模板页", path: "feature-profiles" },
  { name: "站点设置页", path: "site-settings" },
  { name: "本地化设置页", path: "localization" },
  { name: "OpenId 管理页", path: "openid" },
  { name: "配方管理页", path: "recipes" },
  { name: "能力探测页", path: "capabilities" },
  { name: "管理入口页", path: "admin-links" },
  { name: "后续迭代页", path: "graphiql" }
];

const apiChecks = [
  {
    name: "SaaS Summary API",
    method: "GET",
    path: "api/saas/summary",
    validate: ({ status, body }) => status === 200 && body?.ready === true
  },
  {
    name: "SaaS Features API",
    method: "GET",
    path: "api/saas/features",
    validate: ({ status, body }) =>
      status === 200 &&
      Array.isArray(body?.requiredSaasFeatures) &&
      Array.isArray(body?.cmsFeatures)
  },
  {
    name: "SaaS Capabilities API",
    method: "GET",
    path: "api/saas/capabilities",
    validate: ({ status, body }) =>
      status === 200 &&
      body?.mode === "headless-mixed" &&
      body?.managementUi === "/saas" &&
      Array.isArray(body?.availableAdapters) &&
      Array.isArray(body?.headlessFeatures) &&
      Array.isArray(body?.missingHeadlessFeatures)
  },
  {
    name: "SaaS Links API",
    method: "GET",
    path: "api/saas/links",
    validate: ({ status, body }) =>
      status === 200 &&
      Array.isArray(body) &&
      body.some((x) => x?.url === "/api/saas/capabilities")
  },
  {
    name: "Management Tenants API",
    method: "GET",
    path: "api/management/tenants",
    validate: ({ status, body }) =>
      status === 200 &&
      Array.isArray(body) &&
      body.some((x) => x?.name === "Default")
  },
  {
    name: "Management Features API",
    method: "GET",
    path: "api/management/features",
    validate: ({ status, body }) =>
      status === 200 &&
      body?.tenant === "Default" &&
      Array.isArray(body?.features)
  },
  {
    name: "Management Feature Profiles API",
    method: "GET",
    path: "api/management/feature-profiles",
    validate: ({ status, body }) => status === 200 && Array.isArray(body)
  },
  {
    name: "Management Site Settings API",
    method: "GET",
    path: "api/management/site-settings",
    validate: ({ status, body }) =>
      status === 200 &&
      typeof body?.siteName === "string"
  },
  {
    name: "Management Localization API",
    method: "GET",
    path: "api/management/localization",
    validate: ({ status, body }) =>
      status === 200 &&
      typeof body?.defaultCulture === "string" &&
      Array.isArray(body?.supportedCultures)
  },
  {
    name: "Management OpenId Applications API",
    method: "GET",
    path: "api/management/openid/applications",
    validate: ({ status, body }) => status === 200 && Array.isArray(body)
  },
  {
    name: "Management OpenId Scopes API",
    method: "GET",
    path: "api/management/openid/scopes",
    validate: ({ status, body }) => status === 200 && Array.isArray(body)
  },
  {
    name: "Management Recipes API",
    method: "GET",
    path: "api/management/recipes",
    validate: ({ status, body }) => status === 200 && Array.isArray(body)
  },
  {
    name: "Built-in Tenant API Reachability",
    method: "POST",
    path: "api/tenants/create",
    payload: {},
    validate: ({ status }) => [400, 401, 403].includes(status)
  },
  {
    name: "Admin Path Temporarily Disabled",
    method: "GET",
    path: "/saas-admin/Admin",
    validate: ({ status }) => [403, 404].includes(status)
  }
];

const results = [];

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({
  ignoreHTTPSErrors: true,
  viewport: { width: 1440, height: 900 }
});
const page = await context.newPage();

async function triggerAutoSetup() {
  const setupUrl = new URL("api/saas/summary", frontendBaseUrl).toString();
  for (let i = 0; i < 12; i += 1) {
    const response = await context.request.get(setupUrl, { timeout: 30000 });
    if (response.status() === 200) {
      const body = await response.json();
      if (body?.ready) {
        return;
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 600));
  }
}

try {
  await triggerAutoSetup();

  for (const check of frontendChecks) {
    const target = new URL(check.path, frontendBaseUrl).toString();
    let status = 0;
    let ok = false;
    let note = "";

    try {
      const response = await page.goto(target, {
        waitUntil: "domcontentloaded",
        timeout: 30000
      });
      status = response?.status() ?? 0;
      const finalUrl = page.url();
      ok = status > 0 && status < 400 && !finalUrl.includes("/Login") && !finalUrl.includes("/Error");
      note = `status=${status}, finalUrl=${finalUrl}`;
    } catch (error) {
      note = `error=${error instanceof Error ? error.message : String(error)}`;
    }

    const screenshotPath = path.join(
      outputDir,
      `${check.path.replaceAll("/", "_").replace(/^_+/, "") || "root"}.png`
    );
    await page.screenshot({ path: screenshotPath, fullPage: true });

    results.push({
      name: check.name,
      path: target,
      category: "frontend",
      ok,
      note,
      screenshot: screenshotPath
    });
  }

  for (const check of apiChecks) {
    const target = new URL(check.path, frontendBaseUrl).toString();
    let status = 0;
    let ok = false;
    let note = "";

    try {
      const response = await context.request.fetch(target, {
        method: check.method,
        data: check.payload,
        timeout: 30000
      });
      status = response.status();

      let body = null;
      const text = await response.text();
      try {
        body = text ? JSON.parse(text) : null;
      } catch {
        body = text;
      }

      ok = check.validate({ status, body });
      const compact =
        typeof body === "string"
          ? body.slice(0, 120)
          : JSON.stringify(body)?.slice(0, 160);
      note = `status=${status}, body=${compact ?? "null"}`;
    } catch (error) {
      note = `error=${error instanceof Error ? error.message : String(error)}`;
    }

    results.push({
      name: check.name,
      path: target,
      category: "api",
      ok,
      note
    });
  }
} finally {
  await context.close();
  await browser.close();
}

const summaryPath = path.join(outputDir, "summary.json");
fs.writeFileSync(summaryPath, JSON.stringify(results, null, 2), "utf8");

const passed = results.filter((x) => x.ok).length;
const failed = results.length - passed;
console.log(`Playwright check done. passed=${passed}, failed=${failed}`);
console.log(`Summary: ${summaryPath}`);
for (const row of results) {
  console.log(`${row.ok ? "PASS" : "FAIL"} | ${row.name} | ${row.path} | ${row.note}`);
}

if (failed > 0) {
  process.exit(1);
}
