# OrchardFramework Frontend (Vue)

Vue 3 + Vite frontend for the SaaS modules defined in `SaaS平台功能需求文档（SQLite 版本）.md`.

## Development
1. Start backend API first:
   ```bash
   env -u version dotnet run --project ../src/OrchardFramework.Api
   ```
2. Start frontend:
   ```bash
   npm install
   npm run dev
   ```

Vite dev server proxies `/api` requests to `http://localhost:5019`.

## Build
```bash
npm run build
```

## Implemented Pages
- 登录
- 总览统计
- 租户管理
- 用户管理
- 角色管理（含权限分配）
- 权限管理
- 终端模板管理
