# 前端（独立 Vue CLI）

## 本地开发

1) 启动中心后端（API）

```bash
dotnet run --project src/DataAcquisition.Central.Web
```

2) 启动前端（dev server）

```bash
cd frontend
npm install
npm run serve
```

说明：
- dev server 端口：`http://localhost:3000`
- 已在 `vue.config.js` 配置代理：`/api`、`/metrics` → `http://localhost:8000`

