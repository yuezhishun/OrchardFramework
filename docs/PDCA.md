# PDCA Implementation Log

## Plan
- Base MVP on the requirement document `SaaS平台功能需求文档（SQLite 版本）.md`.
- Keep SQLite as default storage for fast verification.
- Iterative delivery strategy:
  - Iteration 0: Orchard MVC baseline only
  - Iteration 1: Tenants + Feature management
  - Iteration 2+: OpenId + Users + Roles

## Do
- Refactored backend to Orchard-first baseline:
  - Removed custom JWT/user/role/template API implementation for this phase.
  - Kept Orchard host + AutoSetup + inspection endpoints.
- Added staged recipes:
  - `SaaS.Iteration0.recipe.json`: MVC baseline + settings only.
  - `SaaS.Base.recipe.json`: Iteration 1 with Admin/Tenants/FeatureProfiles/Features and no CMS content features.
- Updated frontend labels/content to match Iteration 1 testing goals.
- Updated Playwright script to validate Vue routes and Headless API behavior.

## Check
- Implemented integration tests in `tests/OrchardFramework.Api.Tests/ApiFlowTests.cs`:
  - recipe assertions for Iteration 0 and Iteration 1
  - AutoSetup readiness checks
  - feature enabled/disabled checks for Iteration 0/1
  - admin path checks for `/Admin*` and `/saas-admin*`
  - inspection links verification
- Verification command:
  - `env -u version dotnet test OrchardFramework.slnx -c Debug`

## Act
- Strategy update (2026-02-17):
  - Switch to Headless-first execution path: Vue as primary management UI, Orchard admin as fallback only.
  - Use `GraphQL + thin BFF adapter` pattern to maximize Orchard module reuse and avoid re-implementing full backend APIs.
- Next target:
  - Enable GraphQL/OpenId baseline and add capabilities inspection.
  - Replace `/saas-admin` acceptance checks with Vue page + API behavior checks.
- Keep deployment cadence as dev-stage script (`scripts/deploy_saas.sh`) with rollback on health-check failure.

## Iteration Records

### Iteration H1 - 2026-02-17
- 目标：
  - 建立 Headless 基线，提供 Vue 管理台可直接消费的能力探测接口。
  - 将自动化验收从后台页面巡检切换为 Vue + API 行为验收。
- 代码改动：
  - `src/OrchardFramework.Api/Endpoints/SaasInspectionEndpoints.cs`
    - 新增 `/api/saas/capabilities`
    - `links` 增加 headless 相关入口
  - `frontend/src/pages/CapabilitiesPage.vue`
    - 新增能力探测页面
  - `frontend/src/router/index.ts`
  - `frontend/src/components/AppShell.vue`
  - `frontend/src/api/service.ts`
  - `frontend/src/api/types.ts`
  - `scripts/playwright_saas_admin_check.mjs`
    - 切换为 Vue + API 验收
- 测试结果：
  - `dotnet test`: 通过
  - `frontend build`: 通过
  - Playwright（部署后）: 通过（10/10）
- 发布记录：
  - deploy time: 2026-02-17 03:28:43 ~ 03:29:18
  - backup path:
    - `/www/backup/saas-api-20260217032843`
    - `/www/backup/saas-frontend-20260217032843`
- 问题与修复：
  - 部署前首次验收失败（`/api/saas/capabilities` 为 404），原因是线上未发布新后端。
  - 发布后复验通过（10/10）。
- 下一步：
  - H2：接入租户与功能管理的 Vue 页面与薄适配 API。

### Iteration H2-Prep - 2026-02-17
- 目标：
  - 按 H2 路线推进前，临时关闭 `/saas-admin` 访问，管理入口统一收敛到 `/saas`。
- 代码改动：
  - `src/OrchardFramework.Api/Program.cs`
    - 新增 `/saas-admin*` 与 `/Admin*` 路径拦截（返回 404）。
  - `src/OrchardFramework.Api/appsettings.json`
    - 新增 `SaaS:DisableAdminPathAccess=true`。
  - `src/OrchardFramework.Api/Endpoints/SaasInspectionEndpoints.cs`
    - `links` 接口增加 `enabled` 字段并标记后台入口临时关闭。
    - `capabilities` 接口增加 `adminPathAccessDisabled` 字段。
  - `frontend/src/pages/AdminLinksPage.vue`
  - `frontend/src/pages/CapabilitiesPage.vue`
  - `frontend/src/api/types.ts`
    - 前端展示“后台入口临时关闭”状态。
  - `scripts/playwright_saas_admin_check.mjs`
    - 增加 `/saas-admin/Admin` 关闭校验。
  - `tests/OrchardFramework.Api.Tests/ApiFlowTests.cs`
  - `tests/OrchardFramework.Api.Tests/TestWebApplicationFactory.cs`
    - 集成测试从“后台可达”改为“后台入口关闭”校验。
- 测试结果：
  - `dotnet test`: 通过（7/7）
  - `frontend build`: 通过
  - Playwright: 未执行（待部署后执行）
- 问题与修复：
  - `Program.cs` 顶层语句顺序导致编译失败，已通过调整 `partial Program` 声明位置修复。
- 下一步：
  - H2：优先落地 `/api/management/tenants|features|feature-profiles` 薄适配层与 Vue 管理页替换。

### Iteration H2 - 2026-02-17
- 目标：
  - 落地 H2 薄适配 API，并将租户/功能/功能模板管理切换到 Vue 页面。
- 代码改动：
  - `src/OrchardFramework.Api/Endpoints/SaasManagementEndpoints.cs`
    - 新增 `/api/management/tenants|features|feature-profiles`
    - 增加开发阶段访问策略：`SaaS:AllowAnonymousManagementApi`（默认 `true`）
  - `src/OrchardFramework.Api/Program.cs`
    - 注册 `MapSaasManagementEndpoints()`
  - `src/OrchardFramework.Api/Endpoints/SaasInspectionEndpoints.cs`
    - `capabilities` 增加 `availableAdapters`、`allowAnonymousManagementApi`
    - `links` 增加 H2 适配 API 入口
  - `src/OrchardFramework.Api/appsettings.json`
  - `frontend/src/pages/TenantsPage.vue`
  - `frontend/src/pages/FeaturesPage.vue`
  - `frontend/src/pages/FeatureProfilesPage.vue`
  - `frontend/src/components/AppShell.vue`
  - `frontend/src/router/index.ts`
  - `frontend/src/api/service.ts`
  - `frontend/src/api/types.ts`
  - `scripts/playwright_saas_admin_check.mjs`
  - `tests/OrchardFramework.Api.Tests/ApiFlowTests.cs`
- 测试结果：
  - `dotnet test`: 通过（8/8）
  - `frontend build`: 通过
  - Playwright: 未执行（待部署后执行）
- 问题与修复：
  - 初次测试失败：Minimal API 参数绑定歧义（`ShellSettings` 被误判为 body 参数）。
  - 修复：为 H2 端点参数补充 `[FromServices]/[FromBody]/[FromRoute]/[FromQuery]`。
- 下一步：
  - H3：用户与角色管理 API（`/api/management/users|roles|permissions`）与 Vue 页面。

### Iteration H4 - 2026-02-17
- 目标：
  - 补齐站点设置、本地化、OpenId 应用与 Scope 的 Headless 管理能力。
- 代码改动：
  - `src/OrchardFramework.Api/Endpoints/SaasManagementEndpoints.cs`
    - 新增：
      - `GET/PUT /api/management/site-settings`
      - `GET/PUT /api/management/localization`
      - `GET/POST/PATCH /api/management/openid/applications`
      - `GET/POST/PATCH /api/management/openid/scopes`
  - `src/OrchardFramework.Api/Endpoints/SaasInspectionEndpoints.cs`
    - `links/capabilities` 增加 H4 adapters 与模块能力项
  - `src/OrchardFramework.Api/Recipes/SaaS.Base.recipe.json`
    - 启用 `Localization/GraphQL/OpenId.*`
  - `src/OrchardFramework.Api/OrchardFramework.Api.csproj`
    - 增加 Localization/OpenId/OpenIddict 相关依赖
  - `frontend/src/pages/SiteSettingsPage.vue`
  - `frontend/src/pages/LocalizationPage.vue`
  - `frontend/src/pages/OpenIdPage.vue`
  - `frontend/src/router/index.ts`
  - `frontend/src/components/AppShell.vue`
  - `frontend/src/pages/GraphiqlPage.vue`
  - `frontend/src/api/service.ts`
  - `frontend/src/api/types.ts`
  - `scripts/playwright_saas_admin_check.mjs`
  - `tests/OrchardFramework.Api.Tests/ApiFlowTests.cs`
- 测试结果：
  - `dotnet test`: 通过（8/8）
  - `frontend build`: 通过
  - Playwright: 未执行（本地未做线上部署）
- 问题与修复：
  - 编译阶段缺少 H4 依赖包与权限类型引用，已补全依赖并改为模块权限常量。
- 下一步：
  - 部署后执行 Playwright 验收并更新发布记录。

### Iteration H5 - 2026-02-17
- 目标：
  - 补齐 Recipes 管理适配层，并让 Summary 返回实时 OpenId 统计数据。
- 代码改动：
  - `src/OrchardFramework.Api/Endpoints/SaasManagementEndpoints.cs`
    - 新增：
      - `GET /api/management/recipes`
      - `POST /api/management/recipes/execute`
  - `src/OrchardFramework.Api/Endpoints/SaasInspectionEndpoints.cs`
    - `links/capabilities` 增加 Recipes adapters
    - `summary` 改为读取 OpenId 索引表计数
  - `frontend/src/pages/RecipesPage.vue`
  - `frontend/src/pages/GraphiqlPage.vue`
  - `frontend/src/pages/DashboardPage.vue`
  - `frontend/src/router/index.ts`
  - `frontend/src/components/AppShell.vue`
  - `frontend/src/api/service.ts`
  - `frontend/src/api/types.ts`
  - `tests/OrchardFramework.Api.Tests/ApiFlowTests.cs`
  - `scripts/playwright_saas_admin_check.mjs`
- 测试结果：
  - `dotnet test`: 通过（8/8）
  - `frontend build`: 通过
  - Playwright: 通过（25/25）
- 发布记录：
  - deploy time: 2026-02-17 05:34:21 ~ 05:35:04
  - backup path:
    - `/www/backup/saas-api-20260217053421`
    - `/www/backup/saas-frontend-20260217053421`
- 问题与修复：
  - 修复了 GraphQL 页面在 `/saas` 子路径部署下的绝对路径跳转问题。
- 下一步：
  - 继续观察能力矩阵中 `OrchardCore.OpenId`（dependency-only）显示口径，避免与实际可用性产生误判。

### Ops - 2026-02-17 GraphQL 启用
- 目标：
  - 在线启用 `OrchardCore.Apis.GraphQL`。
- 代码改动：
  - `src/OrchardFramework.Api/OrchardFramework.Api.csproj`
    - 增加 `OrchardCore.Apis.GraphQL` 包引用。
- 发布记录：
  - deploy time: 2026-02-17 05:48:37 ~ 05:49:23
  - backup path:
    - `/www/backup/saas-api-20260217054837`
    - `/www/backup/saas-frontend-20260217054837`
- 操作结果：
  - `PUT /saas/api/management/features` 已成功启用 `OrchardCore.Apis.GraphQL`。
  - `GET /saas/api/saas/capabilities` 显示 `OrchardCore.Apis.GraphQL=true`。
  - `POST /saas/api/graphql` 返回 `401 Bearer`（端点已启用且受鉴权保护）。
  - Playwright 验收：通过（25/25），报告：
    - `artifacts/saas-headless-check-2026-02-16T21-50-52-858Z/summary.json`
