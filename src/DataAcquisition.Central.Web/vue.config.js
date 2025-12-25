const { defineConfig } = require("@vue/cli-service");

module.exports = defineConfig({
  transpileDependencies: true,
  devServer: {
    port: 3000,
    proxy: {
      "^/api": {
        target: "http://localhost:8000",
        changeOrigin: true,
      },
      "^/metrics": {
        target: "http://localhost:8000",
        changeOrigin: true,
      },
      // Edge Agent（8001）：用于查看采集侧日志/指标（仅本地开发代理）
      "^/edge": {
        target: "http://localhost:8001",
        changeOrigin: true,
        pathRewrite: { "^/edge": "" },
      },
    },
  },
});

