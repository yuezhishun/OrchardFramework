# SaaS 迭代开发执行计划（可直接执行）

更新时间：2026-02-17  
适用环境：`/home/yueyuan/OrchardFramework`、`https://pty.addai.vip/saas`

## 1. 目标与边界

目标：按迭代方式完成 OrchardCore SaaS 平台开发，每一阶段均满足“可部署、可测试、可回滚”。

边界：
- 保留 OrchardCore 框架能力，不做自定义 CMS 内容功能开发。
- 管理功能以 Vue 前端为主入口（`/saas`）。
- Orchard 后台（`/saas-admin`）仅作为运维兜底入口。
- 开发阶段优先“可验证”和“可回滚”，不追求 7x24 无损发布。

线上入口：
- 前端控制台：`/saas`
- 后台兜底：`/saas-admin`
- 巡检 API：`/saas/api/saas/*`

## 2. 当前状态（截至 2026-02-17）

> 临时策略（2026-02-17）：`/saas-admin/*` 已关闭访问，管理动作统一走 `/saas`（Vue）与 `/saas/api/*`。

### 2.1 已完成基线

- Iteration 0（基线）：`SaaS.Iteration0.recipe.json`
  - 最小 MVC + Setup + Settings
  - 关闭 Admin/Tenants/Features/Users/Roles/CMS 内容模块
- Iteration 1（当前默认配方）：`SaaS.Base.recipe.json`
  - 启用 `OrchardCore.Admin/Tenants/FeatureProfiles/Features/Users/Roles`
  - CMS 内容模块保持禁用

### 2.2 Headless H1（本次会话已完成）

- 后端：新增 Headless 能力矩阵接口
  - `GET /api/saas/capabilities`
  - 文件：`src/OrchardFramework.Api/Endpoints/SaasInspectionEndpoints.cs`
- 后端：巡检链接补充
  - `GET /api/saas/links` 增加 `capabilities` 与内置租户 API 提示
- 前端：新增能力探测页面
  - 页面：`/saas/capabilities`
  - 文件：`frontend/src/pages/CapabilitiesPage.vue`
- 前端：路由与菜单同步
  - 文件：`frontend/src/router/index.ts`
  - 文件：`frontend/src/components/AppShell.vue`
- 自动化：Playwright 脚本切换为 Headless 验收
  - 脚本：`scripts/playwright_saas_admin_check.mjs`
  - 验收内容：Vue 页面 + Headless API（不再以 `/saas-admin` 页面为主验收）

### 2.3 Headless H2（本次会话已完成）

- 后端：新增 H2 薄适配层
  - `GET/POST/PATCH /api/management/tenants`
  - `GET/PUT /api/management/features`
  - `GET/PUT /api/management/feature-profiles`
  - 文件：`src/OrchardFramework.Api/Endpoints/SaasManagementEndpoints.cs`
- 后端：能力矩阵与巡检链接同步 H2
  - 文件：`src/OrchardFramework.Api/Endpoints/SaasInspectionEndpoints.cs`
  - 新增 `availableAdapters` 与 `allowAnonymousManagementApi`
- 前端：Vue 管理页接入 H2 API
  - 页面：`/saas/tenants`、`/saas/features`、`/saas/feature-profiles`
  - 文件：
    - `frontend/src/pages/TenantsPage.vue`
    - `frontend/src/pages/FeaturesPage.vue`
    - `frontend/src/pages/FeatureProfilesPage.vue`
    - `frontend/src/router/index.ts`
    - `frontend/src/components/AppShell.vue`
- 自动化与测试更新
  - 文件：`scripts/playwright_saas_admin_check.mjs`
  - 文件：`tests/OrchardFramework.Api.Tests/ApiFlowTests.cs`
  - 本地结果：
    - `dotnet test`：通过（8/8）
    - `frontend build`：通过

### 2.4 Headless H3（本次会话已完成）

- 后端：新增 H3 用户/角色/权限管理适配层
  - `GET/POST/PATCH /api/management/users`
  - `GET/POST/PATCH /api/management/roles`
  - `GET /api/management/permissions`
  - `PUT /api/management/roles/{id}/permissions`
  - 文件：`src/OrchardFramework.Api/Endpoints/SaasManagementEndpoints.cs`
- 后端：能力矩阵同步 H3 adapters
  - 文件：`src/OrchardFramework.Api/Endpoints/SaasInspectionEndpoints.cs`
- 前端：Vue 管理页接入 H3 API
  - 页面：`/saas/users`、`/saas/roles`、`/saas/permissions`
  - 文件：
    - `frontend/src/pages/UsersPage.vue`
    - `frontend/src/pages/RolesPage.vue`
    - `frontend/src/pages/PermissionsPage.vue`
    - `frontend/src/api/service.ts`
    - `frontend/src/api/types.ts`
    - `frontend/src/router/index.ts`
    - `frontend/src/components/AppShell.vue`
- 自动化与测试更新
  - 文件：`tests/OrchardFramework.Api.Tests/ApiFlowTests.cs`
  - 本地结果：
    - `dotnet test`：通过（8/8）
    - `frontend build`：通过

### 2.5 上次部署与验收结果（H1）

- 部署命令：`./scripts/deploy_saas.sh`
- 部署时间：2026-02-17 03:28:43 ~ 03:29:18
- 备份目录：
  - `/www/backup/saas-api-20260217032843`
  - `/www/backup/saas-frontend-20260217032843`
- 验收命令：`node scripts/playwright_saas_admin_check.mjs`
- 验收结果：`passed=10, failed=0`
- 报告文件：
  - `artifacts/saas-headless-check-2026-02-16T19-29-27-962Z/summary.json`

### 2.6 Headless H4（本次会话已完成）

- 后端：新增 H4 设置与 OpenId 薄适配层
  - `GET/PUT /api/management/site-settings`
  - `GET/PUT /api/management/localization`
  - `GET/POST/PATCH /api/management/openid/applications`
  - `GET/POST/PATCH /api/management/openid/scopes`
  - 文件：`src/OrchardFramework.Api/Endpoints/SaasManagementEndpoints.cs`
- 后端：能力矩阵、巡检链接与配方同步 H4
  - 文件：
    - `src/OrchardFramework.Api/Endpoints/SaasInspectionEndpoints.cs`
    - `src/OrchardFramework.Api/Recipes/SaaS.Base.recipe.json`
- 前端：新增 H4 管理页面
  - 页面：`/saas/site-settings`、`/saas/localization`、`/saas/openid`
  - 文件：
    - `frontend/src/pages/SiteSettingsPage.vue`
    - `frontend/src/pages/LocalizationPage.vue`
    - `frontend/src/pages/OpenIdPage.vue`
    - `frontend/src/router/index.ts`
    - `frontend/src/components/AppShell.vue`
    - `frontend/src/api/service.ts`
    - `frontend/src/api/types.ts`
- 自动化与测试更新
  - 文件：
    - `tests/OrchardFramework.Api.Tests/ApiFlowTests.cs`
    - `scripts/playwright_saas_admin_check.mjs`
  - 本地结果：
    - `dotnet test`：通过（8/8）
    - `frontend build`：通过
  - 部署后结果：
    - `node scripts/playwright_saas_admin_check.mjs`：通过（25/25）
    - 备份目录：
      - `/www/backup/saas-api-20260217053421`
      - `/www/backup/saas-frontend-20260217053421`
  - 本地结果：
    - `dotnet test`：通过（8/8）
    - `frontend build`：通过

### 2.7 Headless H5（本次会话已完成）

- 后端：新增 H5 Recipes 薄适配层与巡检增强
  - `GET /api/management/recipes`
  - `POST /api/management/recipes/execute`
  - `/api/saas/summary` 的 OpenId 统计改为真实读取（applications/scopes/tokens/authorizations）
  - 文件：
    - `src/OrchardFramework.Api/Endpoints/SaasManagementEndpoints.cs`
    - `src/OrchardFramework.Api/Endpoints/SaasInspectionEndpoints.cs`
- 前端：新增 H5 配方管理页并修复 GraphQL 页子路径链接
  - 页面：`/saas/recipes`
  - 文件：
    - `frontend/src/pages/RecipesPage.vue`
    - `frontend/src/pages/GraphiqlPage.vue`
    - `frontend/src/pages/DashboardPage.vue`
    - `frontend/src/router/index.ts`
    - `frontend/src/components/AppShell.vue`
    - `frontend/src/api/service.ts`
    - `frontend/src/api/types.ts`
- 自动化与测试更新
  - 文件：
    - `tests/OrchardFramework.Api.Tests/ApiFlowTests.cs`
    - `scripts/playwright_saas_admin_check.mjs`

## 3. 迭代执行总规则

每个迭代按固定流程执行：

1. 代码改造
2. 本地验证（`dotnet test` + `frontend build`）
3. 发布（`./scripts/deploy_saas.sh`）
4. 线上验证（健康检查 + Playwright）
5. 记录结果（更新 `docs/PDCA.md`）

## 4. Headless 路线（后续执行）

### 4.1 H2：租户与功能管理（Vue 首批替换，已完成）

目标：
- Vue 页面直接完成租户与功能管理，减少对 `/saas-admin` 的依赖。

后端改造：
- 租户优先复用内置 API：`/api/tenants/create|edit|enable|disable|remove|setup`
- 提供统一薄适配层：
  - `GET/POST/PATCH /api/management/tenants`
  - `GET/PUT /api/management/features`
  - `GET/PUT /api/management/feature-profiles`

前端改造：
- 用 Vue 管理页替换对 `/saas-admin/Admin/Tenants|Features` 的跳转依赖。

验收：
- Vue 页面可完成租户增改启停与功能启停。
- Playwright 通过（Vue+API）。

### 4.2 H3：用户与角色管理（已完成）

后端改造：
- `GET/POST/PATCH /api/management/users`
- `GET/POST/PATCH /api/management/roles`
- `GET /api/management/permissions`
- `PUT /api/management/roles/{id}/permissions`

前端改造：
- 补齐用户、角色、权限分配页面与交互。

验收：
- 用户与角色管理全部经 Vue 页面可用。
- 覆盖权限拒绝与边界场景测试。

### 4.3 H4：站点设置/本地化/OpenId 管理（已完成）

后端改造：
- 站点设置与本地化查询/更新 API
- OpenId 应用与 Scope 管理 API

验收：
- Vue 页面可完成设置项维护与 OpenId 管理。

### 4.4 H5：Recipes 管理与统计增强（已完成）

后端改造：
- `GET /api/management/recipes`
- `POST /api/management/recipes/execute`
- `GET /api/saas/summary` 返回实时 OpenId 统计

前端改造：
- 新增 `/saas/recipes` 配方管理页（列表 + 执行）
- 修复 `/saas/graphiql` 中 `/saas` 子路径部署下的链接跳转

验收：
- Vue 页面可查看与执行可用配方。
- Summary 中 OpenId 统计与管理 API 返回一致。

## 5. 统一验收标准

以后续迭代统一采用以下标准：

1. 业务管理流程以 Vue 页面可用为主。
2. `/saas-admin/*` 不作为主验收项，仅作为运维兜底。
3. 所有管理动作走 Orchard 权限校验。
4. 集成测试覆盖租户隔离、权限拒绝、关键边界条件。

## 6. 每次发布的标准操作（复制即可执行）

### 6.1 本地验证

```bash
cd /home/yueyuan/OrchardFramework
env -u version dotnet test OrchardFramework.slnx -c Debug --nologo
npm --prefix frontend run build
```

### 6.2 部署

```bash
cd /home/yueyuan/OrchardFramework
./scripts/deploy_saas.sh
```

### 6.3 健康检查

```bash
curl -s https://pty.addai.vip/saas/api/saas/summary
curl -s https://pty.addai.vip/saas/api/saas/features
curl -s https://pty.addai.vip/saas/api/saas/capabilities
curl -s https://pty.addai.vip/saas/api/saas/links
```

### 6.4 Headless 自动验收（Vue + API）

```bash
cd /home/yueyuan/OrchardFramework
node scripts/playwright_saas_admin_check.mjs
```

## 7. 配方变更后的特别操作（开发阶段）

当你修改了 recipe 的 feature 列表，线上已存在租户不会自动重跑 recipe。开发阶段建议重建默认租户数据后再验证：

```bash
TS=$(date '+%Y%m%d%H%M%S')
APP_DATA='/www/wwwroot/pty.addai.vip/saas-api/App_Data'
BACKUP="/www/backup/saas-api-appdata-$TS"
systemctl stop orchardframework-saas.service
mv "$APP_DATA" "$BACKUP"
mkdir -p "$APP_DATA"
systemctl start orchardframework-saas.service
```

## 8. 回滚策略

优先使用部署脚本自动回滚。若需要手动回滚：

1. 停服务：`systemctl stop orchardframework-saas.service`
2. 恢复最近备份：
   - `/www/backup/saas-api-*` -> `/www/wwwroot/pty.addai.vip/saas-api`
   - `/www/backup/saas-frontend-*` -> `/www/wwwroot/pty.addai.vip/saas`
3. 启服务：`systemctl start orchardframework-saas.service`
4. 复查：`/saas/api/saas/summary`

## 9. 执行记录模板（每次迭代填写）

建议在 `docs/PDCA.md` 追加以下内容：

```md
### Iteration X - YYYY-MM-DD
- 目标：
- 代码改动：
  - 文件 A
  - 文件 B
- 测试结果：
  - dotnet test: 通过/失败
  - frontend build: 通过/失败
  - Playwright: 通过/失败（通过数/总数）
- 发布记录：
  - deploy time:
  - backup path:
- 问题与修复：
- 下一步：
```
