<template>
  <div class="page">
    <header class="header">
      <h1>边缘节点（edge_id）</h1>
      <button @click="load" :disabled="loading">{{ loading ? "刷新中..." : "刷新" }}</button>
    </header>

    <p class="hint">
      后端：<code>DataAcquisition.Central.Web</code>（默认 <code>http://localhost:8000</code>）；
      前端 devServer 通过代理访问 <code>/api</code>。
    </p>

    <p v-if="error" class="error">{{ error }}</p>

    <table v-if="edges.length" class="table">
      <thead>
        <tr>
          <th>edge_id</th>
          <th>agent_base_url</th>
          <th>hostname</th>
          <th>version</th>
          <th>last_seen_utc</th>
          <th>buffer_backlog</th>
          <th>last_error</th>
          <th>actions</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="e in edges" :key="e.edgeId">
          <td><code>{{ e.edgeId }}</code></td>
          <td class="urlcell">
            <code v-if="e.agentBaseUrl">{{ e.agentBaseUrl }}</code>
            <span v-else>-</span>
          </td>
          <td>{{ e.hostname || "-" }}</td>
          <td>{{ e.version || "-" }}</td>
          <td>{{ e.lastSeenUtc }}</td>
          <td>{{ e.bufferBacklog ?? "-" }}</td>
          <td class="errcell">{{ e.lastError || "-" }}</td>
          <td class="actioncell">
            <router-link :to="{ path: '/metrics', query: { edgeId: e.edgeId } }">Metrics</router-link>
            <span class="sep">|</span>
            <router-link :to="{ path: '/logs', query: { edgeId: e.edgeId } }">Logs</router-link>
          </td>
        </tr>
      </tbody>
    </table>

    <p v-else-if="!loading && !error" class="empty">暂无边缘节点（先调用 /api/edges/register 或 /api/edges/heartbeat）。</p>
  </div>
</template>

<script>
import { getJson } from "../api/http";

export default {
  name: "EdgesView",
  data() {
    return { edges: [], loading: false, error: "" };
  },
  mounted() {
    this.load();
  },
  methods: {
    async load() {
      this.loading = true;
      this.error = "";
      try {
        this.edges = await getJson("/api/edges");
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
  align-items: center;
  justify-content: space-between;
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
.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}
.table th,
.table td {
  border: 1px solid #eee;
  padding: 8px 10px;
  text-align: left;
  vertical-align: top;
}
.table thead th {
  background: #fafafa;
}
.errcell {
  max-width: 380px;
  white-space: pre-wrap;
  word-break: break-word;
}
.urlcell {
  max-width: 280px;
  white-space: pre-wrap;
  word-break: break-word;
}
.actioncell {
  white-space: nowrap;
}
.sep {
  color: #aaa;
  margin: 0 6px;
}
</style>

