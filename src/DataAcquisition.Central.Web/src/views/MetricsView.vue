<template>
  <div class="page">
    <header class="header">
      <div>
        <h1>Metrics（按 edge 下钻）</h1>
        <p class="sub">
          通过 <code>/api/edges/&lt;edgeId&gt;/metrics/json</code> 代理读取 Edge.Agent 的 <code>/metrics</code>。
        </p>
      </div>
      <div class="actions">
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
      </div>
      <div class="meta">
        <span v-if="lastUpdate">最后更新：{{ lastUpdate }}</span>
        <span v-else-if="loading">正在加载…</span>
      </div>
    </section>

    <p v-if="error" class="error">{{ error }}</p>

    <section v-if="edgeId && metrics" class="grid">
      <div class="kpi">
        <div class="kpiLabel">采集延迟</div>
        <div class="kpiValue">{{ fmtNum(getOne("data_acquisition_collection_latency_ms")) }} ms</div>
      </div>
      <div class="kpi">
        <div class="kpiLabel">采集频率</div>
        <div class="kpiValue">{{ fmtNum(getOne("data_acquisition_collection_rate")) }} points/s</div>
      </div>
      <div class="kpi">
        <div class="kpiLabel">队列深度</div>
        <div class="kpiValue">{{ fmtNum(getOne("data_acquisition_queue_depth")) }}</div>
      </div>
      <div class="kpi">
        <div class="kpiLabel">错误总数</div>
        <div class="kpiValue">{{ fmtNum(getOne("data_acquisition_errors_total")) }}</div>
      </div>
    </section>

    <section v-if="edgeId && metrics" class="card">
      <div class="row">
        <label class="label">搜索指标</label>
        <input v-model="keyword" placeholder="按指标名过滤…" />
      </div>

      <table class="table" v-if="filteredMetricNames.length">
        <thead>
          <tr>
            <th>metric</th>
            <th>type</th>
            <th>latest</th>
            <th>labels</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="name in filteredMetricNames" :key="name">
            <td><code>{{ name }}</code></td>
            <td>{{ metrics[name]?.type || "-" }}</td>
            <td>{{ fmtLatest(metrics[name]?.data) }}</td>
            <td class="labels">
              <span v-if="latestLabels(metrics[name]?.data)">
                <code>{{ JSON.stringify(latestLabels(metrics[name]?.data)) }}</code>
              </span>
              <span v-else>-</span>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-else class="empty">暂无指标数据（或该 edge 不可达）。</p>
    </section>
  </div>
</template>

<script>
import { getJson } from "../api/http";

export default {
  name: "MetricsView",
  data() {
    return {
      edgesLoading: false,
      edges: [],
      edgeId: "",
      loading: false,
      error: "",
      lastUpdate: "",
      metrics: null,
      keyword: "",
    };
  },
  computed: {
    filteredMetricNames() {
      const keys = this.metrics ? Object.keys(this.metrics) : [];
      const kw = (this.keyword || "").trim().toLowerCase();
      const filtered = kw ? keys.filter((k) => k.toLowerCase().includes(kw)) : keys;
      return filtered.sort();
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
    if (this.edgeId) await this.loadMetrics();
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
    async loadMetrics() {
      if (!this.edgeId) return;
      this.loading = true;
      this.error = "";
      try {
        const data = await getJson(`/api/edges/${encodeURIComponent(this.edgeId)}/metrics/json`);
        this.metrics = data.metrics || {};
        this.lastUpdate = new Date(data.timestamp || Date.now()).toLocaleString("zh-CN");
      } catch (e) {
        this.metrics = null;
        this.error = e?.message || String(e);
      } finally {
        this.loading = false;
      }
    },
    async onEdgeChange() {
      this.$router.replace({ path: "/metrics", query: { edgeId: this.edgeId } }).catch(() => {});
      await this.loadMetrics();
    },
    async reload() {
      await this.loadMetrics();
    },
    latestPoint(data) {
      if (!Array.isArray(data) || data.length === 0) return null;
      return data[data.length - 1];
    },
    latestLabels(data) {
      const p = this.latestPoint(data);
      return p && p.labels ? p.labels : null;
    },
    fmtLatest(data) {
      const p = this.latestPoint(data);
      if (!p) return "-";
      const v = typeof p.value === "number" ? p.value : Number(p.value);
      return Number.isFinite(v) ? this.fmtNum(v) : String(p.value);
    },
    fmtNum(v) {
      const n = typeof v === "number" ? v : Number(v);
      if (!Number.isFinite(n)) return "-";
      return n.toFixed(2);
    },
    getOne(metricName) {
      const m = this.metrics ? this.metrics[metricName] : null;
      const p = m ? this.latestPoint(m.data) : null;
      return p ? p.value : null;
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
  gap: 10px;
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
}
.label {
  font-size: 13px;
  color: #555;
  min-width: 80px;
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
.grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  margin-bottom: 16px;
}
.kpi {
  border: 1px solid #eee;
  border-radius: 8px;
  padding: 14px;
  background: #fff;
}
.kpiLabel {
  font-size: 12px;
  color: #666;
  margin-bottom: 8px;
}
.kpiValue {
  font-size: 22px;
  font-weight: 700;
  color: #0b57d0;
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
.labels {
  max-width: 520px;
  white-space: pre-wrap;
  word-break: break-word;
}
.empty {
  color: #666;
}
@media (max-width: 900px) {
  .grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>

