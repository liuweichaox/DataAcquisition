<template>
  <div class="page">
    <header class="header">
      <h1>指标（Central API）</h1>
      <div class="actions">
        <button @click="load" :disabled="loading">{{ loading ? "刷新中..." : "刷新" }}</button>
        <a class="raw" href="/metrics" target="_blank" rel="noreferrer">打开 /metrics 原始页</a>
      </div>
    </header>

    <p class="hint">
      数据源：<code>/api/metrics-data</code>（由 Central API 从 <code>/metrics</code> 解析为 JSON）。
    </p>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="payload" class="meta">
      <div><b>timestamp</b>: <code>{{ payload.timestamp }}</code></div>
      <div><b>metrics keys</b>: <code>{{ metricKeys.length }}</code></div>
    </div>

    <div v-if="payload" class="grid">
      <div v-for="k in metricKeys" :key="k" class="card">
        <div class="k"><code>{{ k }}</code></div>
        <div class="small">
          <div><b>type</b>: <code>{{ payload.metrics?.[k]?.type ?? "-" }}</code></div>
          <div><b>data</b>: <code>{{ payload.metrics?.[k]?.data?.length ?? 0 }}</code></div>
        </div>
        <details>
          <summary>展开 JSON</summary>
          <pre class="pre">{{ pretty(payload.metrics?.[k]) }}</pre>
        </details>
      </div>
    </div>

    <p v-else-if="!loading && !error" class="empty">暂无数据（确认 Central API 已启动）。</p>
  </div>
</template>

<script>
import { getJson } from "../api/http";

export default {
  name: "MetricsView",
  data() {
    return { payload: null, loading: false, error: "" };
  },
  computed: {
    metricKeys() {
      const keys = Object.keys(this.payload?.metrics || {});
      keys.sort();
      return keys;
    },
  },
  mounted() {
    this.load();
  },
  methods: {
    pretty(v) {
      return JSON.stringify(v, null, 2);
    },
    async load() {
      this.loading = true;
      this.error = "";
      try {
        this.payload = await getJson("/api/metrics-data");
      } catch (e) {
        this.error = e?.message || String(e);
      } finally {
        this.loading = false;
      }
    },
  },
};
</script>

<style scoped>
.page {
  max-width: 1100px;
  margin: 24px auto;
  padding: 0 16px;
  font-family: system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, "Noto Sans", "Liberation Sans",
    sans-serif;
}
.header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
}
.actions {
  display: flex;
  align-items: center;
  gap: 12px;
}
.hint {
  color: #555;
}
.error {
  color: #b00020;
  white-space: pre-wrap;
}
.empty {
  color: #666;
}
.meta {
  color: #333;
  margin: 12px 0;
}
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 12px;
}
.card {
  border: 1px solid #eee;
  border-radius: 8px;
  padding: 10px 12px;
  background: #fff;
}
.k {
  margin-bottom: 6px;
}
.small {
  font-size: 13px;
  color: #444;
  display: grid;
  gap: 4px;
  margin-bottom: 6px;
}
.pre {
  margin: 8px 0 0;
  padding: 10px;
  background: #fafafa;
  border: 1px solid #eee;
  border-radius: 6px;
  overflow: auto;
  font-size: 12px;
}
.raw {
  color: #0b57d0;
  text-decoration: none;
}
</style>

