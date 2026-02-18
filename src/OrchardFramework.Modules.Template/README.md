# OrchardFramework.Modules.Template

这是一个可复用的新模块脚手架模板，包含以下最小能力：

- `Manifest.cs`：模块与 Feature 定义
- `Startup.cs`：服务注册 + 端点挂载
- `Permissions/TemplatePermissions.cs`：权限模板
- `Migrations/TemplateMigrations.cs`：数据迁移模板
- `Endpoints/TemplateModuleEndpoints.cs`：REST 端点模板
- `GraphQL/*`：GraphQL Schema 扩展模板

## 1. Feature 列表

- `OrchardFramework.ModuleTemplate`
- `OrchardFramework.ModuleTemplate.GraphQL`

## 2. 验证方式

启用 Feature 后，验证 REST 端点：

```bash
curl http://localhost:5019/api/module-template/ping
```

启用 GraphQL 子 Feature 后，验证 GraphQL 字段：

```bash
curl -X POST http://localhost:5019/api/graphql \
  -H "Content-Type: application/json" \
  -d '{"query":"{ moduleTemplateHealth }"}'
```

## 3. 复用步骤（推荐）

1. 复制本目录为新模块目录，例如 `src/OrchardFramework.Modules.Billing`
2. 全局替换：
- `OrchardFramework.Modules.Template` -> `OrchardFramework.Modules.Billing`
- `OrchardFramework.ModuleTemplate` -> `OrchardFramework.Billing`
- `Template` 前缀类名 -> `Billing`
3. 在 `OrchardFramework.slnx` 添加新项目
4. 在 `src/OrchardFramework.Api/OrchardFramework.Api.csproj` 添加 `ProjectReference`
5. 在配方或运行时启用新 Feature

## 4. 生产建议

- 将示例端点改为业务端点，并加权限校验
- 将 `TemplateMigrations` 补齐为真实数据迁移
- 为新 Feature 增加集成测试（租户隔离、权限、异常分支）
