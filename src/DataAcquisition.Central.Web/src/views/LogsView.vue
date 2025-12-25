<template>
  <div class="page">
    <header class="header">
      <div>
        <h1>Logs（按 edge 下钻）</h1>
        <p class="sub">通过 <code>/api/edges/&lt;edgeId&gt;/logs</code> 代理读取 Edge.Agent 日志。</p>
      </div>
      <div class="actions">
        <label class="chk">
          <input type="checkbox" v-model="autoRefresh" @change="toggleAutoRefresh" />
          自动刷新（1s）
        </label>
        <button @click="reload" :disabled="loading || !edgeId">{{ loading ? "刷新中..." : "刷新" }}</button>
      </div>
    </header>

    <section class="card">
      <div class="row">
        <label class="label">选择 edge</label>
        <select v-model="edgeId" @change="onEdgeChange" :disabled="edgesLoading || edges.length === 0">
          <option value="" disabled>请选择…</option>
          <option v-for="e in edges" :key="e.edgeId" :value="e.edgeId">
            {{ e.edgeId }}{{ e.hostname ? ` (${e.hostname})` : "" }}
          </option>
        </select>

        <label class="label" style="margin-left: 14px;">Level</label>
        <select v-model="level" @change="applyFilters" :disabled="!edgeId">
          <option value="">全部</option>
          <option v-for="l in levels" :key="l" :value="l">{{ l }}</option>
        </select>

        <label class="label" style="margin-left: 14px;">关键字</label>
        <input v-model="keyword" @keyup.enter="applyFilters" placeholder="消息/源/异常…" :disabled="!edgeId" />

        <button @click="applyFilters" :disabled="loading || !edgeId">搜索</button>
        <button @click="clearFilters" :disabled="loading || !edgeId">清空</button>
      </div>

      <div class="meta">
        <span v-if="lastUpdate">最后更新：{{ lastUpdate }}</span>
        <span v-if="total > 0" style="margin-left: 12px;">共 {{ total }} 条</span>
      </div>
    </section>

    <p v-if="error" class="error">{{ error }}</p>

    <section v-if="edgeId && total > 0" class="stats">
      <div class="stat">
        <div class="statLabel">本页 Information</div>
        <div class="statValue info">{{ stats.information }}</div>
      </div>
      <div class="stat">
        <div class="statLabel">本页 Warning</div>
        <div class="statValue warn">{{ stats.warning }}</div>
      </div>
      <div class="stat">
        <div class="statLabel">本页 Error/Fatal</div>
        <div class="statValue err">{{ stats.error }}</div>
      </div>
    </section>

    <section v-if="edgeId" class="card">
      <div v-if="loading && logs.length === 0" class="loading">正在加载日志…</div>
      <div v-else-if="logs.length === 0" class="empty">暂无日志数据</div>

      <div v-else>
        <div v-for="(log, idx) in logs" :key="idx" class="log" :data-level="(log.level || '').toLowerCase()">
          <div class="logHead">
            <span class="ts">{{ fmtTs(log.timestamp) }}</span>
            <span class="lvl" :data-lvl="(log.level || '').toLowerCase()">{{ log.level }}</span>
            <span v-if="log.source" class="src">{{ log.source }}</span>
          </div>
          <div class="msg">{{ log.message }}</div>
          <div v-if="log.exception" class="ex">
            <button class="toggle" @click="toggleEx(idx)">{{ expanded[idx] ? "隐藏异常" : "显示异常" }}</button>
            <pre v-if="expanded[idx]" class="exbox">{{ log.exception }}</pre>
          </div>
        </div>

        <div class="pager">
          <button @click="prevPage" :disabled="page <= 1 || loading">上一页</button>
          <span class="pmeta">第 {{ page }} / {{ totalPages }} 页</span>
          <button @click="nextPage" :disabled="page >= totalPages || loading">下一页</button>

          <span style="margin-left: 16px;">每页</span>
          <select v-model.number="pageSize" @change="applyFilters" :disabled="loading">
            <option :value="50">50</option>
            <option :value="100">100</option>
            <option :value="200">200</option>
            <option :value="500">500</option>
          </select>
        </div>
      </div>
    </section>
  </div>
</template>

<script>
import { getJson } from "../api/http";

export default {
  name: "LogsView",
  data() {
    return {
      edgesLoading: false,
      edges: [],
      edgeId: "",
      levels: [],
      level: "",
      keyword: "",
      logs: [],
      total: 0,
      page: 1,
      pageSize: 100,
      totalPages: 1,
      loading: false,
      error: "",
      lastUpdate: "",
      autoRefresh: false,
      timer: null,
      expanded: {},
    };
  },
  computed: {
    stats() {
      const out = { information: 0, warning: 0, error: 0 };
      for (const l of this.logs) {
        const lv = (l.level || "").toLowerCase();
        if (lv === "information") out.information += 1;
        else if (lv === "warning") out.warning += 1;
        else if (lv === "error" || lv === "fatal") out.error += 1;
      }
      return out;
    },
  },
  async mounted() {
    await this.loadEdges();
    const fromQuery = this.$route?.query?.edgeId;
    if (typeof fromQuery === "string" && fromQuery) {
      this.edgeId = fromQuery;
    } else if (this.edges.length) {
      this.edgeId = this.edges[0].edgeId;
    }
    if (this.edgeId) {
      await this.loadLevels();
      await this.loadLogs();
    }
  },
  beforeUnmount() {
    if (this.timer) clearInterval(this.timer);
  },
  methods: {
    async loadEdges() {
      this.edgesLoading = true;
      try {
        this.edges = await getJson("/api/edges");
      } catch (e) {
        this.error = e?.message || String(e);
      } finally {
        this.edgesLoading = false;
      }
    },
    async loadLevels() {
      if (!this.edgeId) return;
      try {
        this.levels = await getJson(`/api/edges/${encodeURIComponent(this.edgeId)}/logs/levels`);
      } catch {
        this.levels = [];
      }
    },
    async loadLogs() {
      if (!this.edgeId) return;
      this.loading = true;
      this.error = "";
      try {
        const params = new URLSearchParams({
          page: String(this.page),
          pageSize: String(this.pageSize),
        });
        if (this.level) params.set("level", this.level);
        if (this.keyword) params.set("keyword", this.keyword);

        const data = await getJson(`/api/edges/${encodeURIComponent(this.edgeId)}/logs?${params.toString()}`);
        this.logs = data.data || [];
        this.total = data.total || 0;
        this.totalPages = data.totalPages || 1;
        this.lastUpdate = new Date().toLocaleString("zh-CN");
        this.expanded = {};
      } catch (e) {
        this.logs = [];
        this.total = 0;
        this.totalPages = 1;
        this.error = e?.message || String(e);
      } finally {
        this.loading = false;
      }
    },
    async onEdgeChange() {
      this.$router.replace({ path: "/logs", query: { edgeId: this.edgeId } }).catch(() => {});
      this.page = 1;
      this.level = "";
      this.keyword = "";
      await this.loadLevels();
      await this.loadLogs();
    },
    async applyFilters() {
      this.page = 1;
      await this.loadLogs();
    },
    async clearFilters() {
      this.level = "";
      this.keyword = "";
      this.page = 1;
      await this.loadLogs();
    },
    async reload() {
      await this.loadLogs();
    },
    toggleEx(idx) {
      this.expanded[idx] = !this.expanded[idx];
      this.expanded = { ...this.expanded };
    },
    fmtTs(ts) {
      if (!ts) return "";
      const d = new Date(ts);
      if (Number.isNaN(d.getTime())) return String(ts);
      const pad = (n, w = 2) => String(n).padStart(w, "0");
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(
        d.getMinutes()
      )}:${pad(d.getSeconds())}.${pad(d.getMilliseconds(), 3)}`;
    },
    async prevPage() {
      if (this.page <= 1) return;
      this.page -= 1;
      await this.loadLogs();
      window.scrollTo({ top: 0, behavior: "smooth" });
    },
    async nextPage() {
      if (this.page >= this.totalPages) return;
      this.page += 1;
      await this.loadLogs();
      window.scrollTo({ top: 0, behavior: "smooth" });
    },
    toggleAutoRefresh() {
      if (this.autoRefresh) {
        if (this.timer) clearInterval(this.timer);
        this.timer = setInterval(() => this.loadLogs(), 1000);
      } else {
        if (this.timer) clearInterval(this.timer);
        this.timer = null;
      }
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
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
}
.sub {
  margin: 6px 0 0;
  color: #555;
  font-size: 13px;
}
.actions {
  display: flex;
  gap: 12px;
  align-items: center;
}
.chk {
  font-size: 13px;
  color: #555;
  display: inline-flex;
  align-items: center;
  gap: 8px;
}
.card {
  border: 1px solid #eee;
  border-radius: 8px;
  padding: 14px;
  background: #fff;
  margin-bottom: 16px;
}
.row {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.label {
  font-size: 13px;
  color: #555;
}
.meta {
  margin-top: 10px;
  color: #666;
  font-size: 12px;
}
.error {
  color: #b00020;
  white-space: pre-wrap;
  margin: 10px 0 16px;
}
.stats {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
  margin-bottom: 16px;
}
.stat {
  border: 1px solid #eee;
  border-radius: 8px;
  padding: 14px;
  background: #fff;
}
.statLabel {
  font-size: 12px;
  color: #666;
  margin-bottom: 8px;
}
.statValue {
  font-size: 22px;
  font-weight: 700;
}
.statValue.info {
  color: #0b57d0;
}
.statValue.warn {
  color: #b26a00;
}
.statValue.err {
  color: #b00020;
}
.loading,
.empty {
  color: #666;
  padding: 18px 0;
}
.log {
  border: 1px solid #eee;
  border-left-width: 4px;
  border-radius: 6px;
  padding: 12px;
  margin-bottom: 12px;
  background: #fff;
}
.log[data-level="information"] {
  border-left-color: #0b57d0;
}
.log[data-level="warning"] {
  border-left-color: #b26a00;
}
.log[data-level="error"],
.log[data-level="fatal"] {
  border-left-color: #b00020;
  background: #fff5f6;
}
.logHead {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 6px;
  flex-wrap: wrap;
}
.ts {
  color: #666;
  font-size: 12px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
}
.lvl {
  font-size: 12px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 999px;
  border: 1px solid #ddd;
}
.lvl[data-lvl="information"] {
  background: #eef4ff;
  color: #0b57d0;
  border-color: #c7dafc;
}
.lvl[data-lvl="warning"] {
  background: #fff4e5;
  color: #b26a00;
  border-color: #ffd8a8;
}
.lvl[data-lvl="error"],
.lvl[data-lvl="fatal"] {
  background: #ffecef;
  color: #b00020;
  border-color: #ffc2cc;
}
.src {
  color: #444;
  font-size: 12px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
}
.msg {
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.5;
  font-size: 13px;
}
.ex {
  margin-top: 8px;
}
.toggle {
  font-size: 12px;
}
.exbox {
  margin-top: 8px;
  padding: 10px;
  background: #f7f7f7;
  border-radius: 6px;
  overflow: auto;
  max-height: 320px;
  white-space: pre-wrap;
  word-break: break-word;
}
.pager {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  margin-top: 16px;
  flex-wrap: wrap;
}
.pmeta {
  color: #555;
  font-size: 13px;
}
@media (max-width: 900px) {
  .stats {
    grid-template-columns: 1fr;
  }
}
</style>

