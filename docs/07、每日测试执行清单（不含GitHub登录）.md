# 每日测试执行清单（不含 GitHub 登录）

更新时间：2026-02-17  
适用项目：`/home/yueyuan/OrchardFramework`  
来源：`docs/03、管理租户与模块中的功能.md`（已排除 `GH-*` 用例）

## 1. 使用说明

- 本清单用于每日执行。
- `GitHub 账号登录` 测试已明确排除。
- 每天至少执行“必做冒烟”；建议每日下班前执行一次“全量回归”。

## 1.1 自动化执行（已实现）

- 脚本：`scripts/playwright_daily_check_no_github.mjs`
- 默认模式（冒烟）：
  - `node scripts/playwright_daily_check_no_github.mjs`
- 全量模式：
  - `SAAS_DAILY_MODE=full node scripts/playwright_daily_check_no_github.mjs`
- 指定站点：
  - `SAAS_BASE_URL=https://pty.addai.vip/saas node scripts/playwright_daily_check_no_github.mjs`
- 常用可选变量：
  - `SAAS_DAILY_CLEANUP=true|false`（默认 `true`，执行后自动清理测试租户/用户/角色）
  - `SAAS_ADMIN_USERNAME`、`SAAS_ADMIN_PASSWORD`
  - `SAAS_DAILY_OUTPUT_DIR`（默认输出到 `artifacts/saas-daily-check-<timestamp>`）

## 2. 每日准备（5~10 分钟）

- [ ] 确认后端服务可启动：`env -u version dotnet run --project src/OrchardFramework.Api`
- [ ] 确认前端可访问（如需 UI）：`npm --prefix frontend run dev`
- [ ] 访问并记录：
  - [ ] `GET /api/saas/summary` 返回 `ready=true`
  - [ ] `GET /api/management/tenants` 返回包含 `Default`
- [ ] 确认配置：
  - [ ] `SaaS:AllowAnonymousManagementApi=true`（功能冒烟）
  - [ ] `SaaS:DisableAdminPathAccess=true`（当前默认）

## 3. 每日必做冒烟（20~40 分钟）

## 3.1 租户生命周期（T）

- [ ] T-01 创建租户成功（`tenant-a`）
- [ ] T-04 编辑租户信息（`description/category`）
- [ ] T-05 禁用运行中租户
- [ ] T-08 删除租户成功（先禁用再删）
- [ ] T-09 删除默认租户拦截（应返回失败）

## 3.2 模块与功能（F）

- [ ] F-01 获取功能清单成功（`features[]` 非空）
- [ ] F-02 启用 1 个低风险功能（建议 `OrchardCore.Markdown`）
- [ ] F-03 禁用刚启用的功能并恢复
- [ ] F-04 禁用常驻功能被拦截（应返回 `400`）
- [ ] F-05 提交未知 Feature ID 被拦截（应返回 `400`）

## 3.3 用户与角色隔离（UR）

- [ ] UR-01 在 `tenant-a` 创建角色 `TenantAdmin`
- [ ] UR-02 在 `tenant-b` 创建同名角色 `TenantAdmin`（不冲突）
- [ ] UR-04 在 `tenant-a` 创建用户 `a_admin` 并绑定角色
- [ ] UR-05 在 `tenant-b` 创建用户 `b_admin` 并绑定角色
- [ ] UR-06 双租户用户列表互不可见
- [ ] UR-07 跨租户角色误用被拦截（应返回 `400`）

## 3.4 系统账号登录（L，不含 GitHub）

- [ ] L-01 `admin / Admin123!` 登录成功（`/Login`）
- [ ] L-02 错误密码登录失败
- [ ] L-03 租户本地账号登录成功（例如 `a_admin`）
- [ ] L-04 禁用用户后登录失败
- [ ] L-05 退出登录后访问受保护页面跳转到登录页

## 3.5 冒烟收尾快照

- [ ] `GET /api/saas/summary` 记录结果
- [ ] `GET /api/management/tenants` 记录结果
- [ ] `GET /api/management/features?tenant=Default` 记录结果

## 4. 每日全量回归（建议 60~90 分钟）

## 4.1 租户扩展校验

- [ ] T-02 重复租户名创建拦截（`409`）
- [ ] T-03 空租户名校验（`400`）
- [ ] T-06 未初始化租户启用拦截（`400`）
- [ ] T-07 运行中租户删除拦截（`400`）

## 4.2 Feature Profile 全链路

- [ ] F-06 创建 Feature Profile（`SaaSLean`）
- [ ] F-07 绑定 `tenant-b` 到该 Profile
- [ ] F-08 删除 Feature Profile
- [ ] F-09 指向未运行租户更新功能被拦截（`400`）

## 4.3 用户角色扩展校验

- [ ] UR-03 角色权限分配与保存（`PUT /roles/{id}/permissions`）
- [ ] UR-08 用户禁用后登录拦截
- [ ] UR-09 删除角色后的用户角色关系验证

## 4.4 本地自动化（建议）

- [ ] `env -u version dotnet test OrchardFramework.slnx -c Debug`
- [ ] `npm --prefix frontend run build`

## 5. 每日收尾与产物归档

- [ ] 清理当日临时租户与测试用户（避免影响次日）
- [ ] 保存请求响应与截图到：
  - [ ] `artifacts/test-plan-tenant-feature-login/<YYYY-MM-DD>/api`
  - [ ] `artifacts/test-plan-tenant-feature-login/<YYYY-MM-DD>/screenshots`
  - [ ] `artifacts/test-plan-tenant-feature-login/<YYYY-MM-DD>/summary.md`
- [ ] 在日报中记录：
  - [ ] 通过项数量
  - [ ] 失败项与阻塞原因
  - [ ] 是否出现跨租户数据可见/可写（必须为“否”）
  - [ ] 明日待验证修复项
