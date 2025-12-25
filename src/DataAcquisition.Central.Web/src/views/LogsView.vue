<template>
  <div class="page">
    <header class="header">
      <h1>采集日志（Edge Agent）</h1>
      <button @click="load" :disabled="loading">{{ loading ? "刷新中..." : "刷新" }}</button>
    </header>

    <p class="hint">
      数据源：<code>/edge/api/logs</code>（通过 devServer 代理到 <code>http://localhost:8001/api/logs</code>）。
    </p>

    <section class="filters">
      <label class="f">
        <span>level</span>
        <select v-model="level">
          <option value="">(all)</option>
          <option v-for="l in levels" :key="l" :value="l">{{ l }}</option>
        </select>
      </label>

      <label class="f">
        <span>keyword</span>
        <input v-model="keyword" placeholder="模糊搜索（message/source/exception）" />
      </label>

      <label class="f">
        <span>pageSize</span>
        <select v-model.number="pageSize">
          <option :value="50">50</option>
          <option :value="100">100</option>
          <option :value="200">200</option>
        </select>
      </label>

      <button @click="page = 1; load()" :disabled="loading">查询</button>
    </section>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="resp" class="meta">
      <div><b>Total</b>: <code>{{ resp.Total }}</code></div>
      <div><b>Page</b>: <code>{{ resp.Page }}</code>/<code>{{ resp.TotalPages }}</code></div>
    </div>

    <table v-if="rows.length" class="table">
      <thead>
        <tr>
          <th>time</th>
          <th>level</th>
          <th>source</th>
          <th style="width: 55%">message</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(r, idx) in rows" :key="idx">
          <td><code>{{ r.timestamp || r.time || r.createdAt || "-" }}</code></td>
          <td><code>{{ r.level || "-" }}</code></td>
          <td><code>{{ r.sourceContext || r.source || "-" }}</code></td>
          <td class="msg">
            <div class="m">{{ r.message || r.renderedMessage || "-" }}</div>
            <details v-if="r.exception">
              <summary>exception</summary>
              <pre class="pre">{{ r.exception }}</pre>
            </details>
          </td>
        </tr>
      </tbody>
    </table>

    <p v-else-if="!loading && !error" class="empty">暂无日志（确认 Edge Agent 已启动并产生日志）。</p>

    <div v-if="resp" class="pager">
      <button @click="prev" :disabled="loading || page <= 1">上一页</button>
      <button @click="next" :disabled="loading || page >= (resp.TotalPages || 1)">下一页</button>
    </div>
  </div>
</template>

<script>
import { getJson } from "../api/http";

export default {
  name: "LogsView",
  data() {
    return {
      loading: false,
      error: "",
      levels: [],
      level: "",
      keyword: "",
      page: 1,
      pageSize: 100,
      resp: null,
    };
  },
  computed: {
    rows() {
      return this.resp?.Data || [];
    },
  },
  async mounted() {
    await this.loadLevels();
    await this.load();
  },
  methods: {
    async loadLevels() {
      try {
        const data = await getJson("/edge/api/logs/levels");
        this.levels = Array.isArray(data) ? data : [];
      } catch {
        // levels 获取失败不影响主流程（后端可能还没启动）
        this.levels = [];
      }
    },
    async load() {
      this.loading = true;
      this.error = "";
      try {
        const q = new URLSearchParams();
        if (this.level) q.set("level", this.level);
        if (this.keyword) q.set("keyword", this.keyword);
        q.set("page", String(this.page));
        q.set("pageSize", String(this.pageSize));
        this.resp = await getJson(`/edge/api/logs?${q.toString()}`);
      } catch (e) {
        this.error = e?.message || String(e);
        this.resp = null;
      } finally {
        this.loading = false;
      }
    },
    async prev() {
      if (this.page <= 1) return;
      this.page -= 1;
      await this.load();
    },
    async next() {
      const totalPages = this.resp?.TotalPages || 1;
      if (this.page >= totalPages) return;
      this.page += 1;
      await this.load();
    },
  },
};
</script>

<style scoped>
.page {
  max-width: 1200px;
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
.hint {
  color: #555;
}
.filters {
  display: flex;
  flex-wrap: wrap;
  gap: 10px 12px;
  align-items: end;
  padding: 10px 12px;
  border: 1px solid #eee;
  border-radius: 8px;
  background: #fff;
  margin: 12px 0;
}
.f {
  display: grid;
  gap: 4px;
}
.f span {
  font-size: 12px;
  color: #444;
}
input,
select {
  padding: 6px 8px;
  border: 1px solid #ddd;
  border-radius: 6px;
  min-width: 180px;
}
.error {
  color: #b00020;
  white-space: pre-wrap;
}
.empty {
  color: #666;
}
.meta {
  margin: 8px 0;
  color: #333;
  display: flex;
  gap: 16px;
}
.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
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
.msg .m {
  white-space: pre-wrap;
  word-break: break-word;
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
.pager {
  margin: 12px 0 24px;
  display: flex;
  gap: 8px;
}
</style>

