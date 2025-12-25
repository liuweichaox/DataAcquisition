<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h2 class="title">系统指标监控</h2>
        <div class="refresh-info">
          <span v-if="lastUpdate">最后更新: {{ lastUpdate }}</span>
          <span v-else>正在加载...</span>
        </div>
      </div>
      <div class="header-actions">
        <el-button type="primary" @click="loadMetrics" :loading="loading">
          刷新
        </el-button>
        <el-link class="raw" :underline="false" href="/metrics" target="_blank" rel="noreferrer">
          打开 /metrics 原始页
        </el-link>
      </div>
    </div>

    <el-card class="filter-card" shadow="never">
      <el-row :gutter="20">
        <el-col :span="12">
          <el-select v-model="selectedMetrics" multiple clearable collapse-tags filterable placeholder="选择要展示的指标" style="width: 100%;">
            <el-option v-for="name in availableMetrics" :key="name" :label="getMetricTitle(name)" :value="name" />
          </el-select>
        </el-col>
        <el-col :span="6">
          <el-input v-model="searchKeyword" placeholder="搜索指标名称" clearable @keyup.enter="searchMetrics">
            <template #prefix>🔎</template>
          </el-input>
        </el-col>
        <el-col :span="6">
          <div class="filter-actions">
            <el-button type="primary" @click="searchMetrics" :loading="loading">搜索</el-button>
            <el-button @click="clearFilters">清空</el-button>
          </div>
        </el-col>
      </el-row>
    </el-card>

    <el-alert v-if="error" :title="error" type="error" :closable="false" class="mb" />

    <div v-if="loading && !metrics" class="center">
      <el-text type="info">正在加载指标数据...</el-text>
    </div>

    <div v-else-if="metrics && Object.keys(metrics).length === 0" class="center">
      <el-empty description="暂无指标数据，请确保系统正在运行并产生指标" />
    </div>

    <div v-else-if="metrics">
      <el-card class="metric-card" shadow="hover">
        <template #header>
          <div class="metric-header">
            <span class="metric-title">关键指标概览</span>
          </div>
        </template>
        <div class="stats-grid">
          <el-statistic title="采集延迟" :value="getMetricValue('data_acquisition_collection_latency_ms')" suffix="ms" />
          <el-statistic title="采集频率" :value="getMetricValue('data_acquisition_collection_rate')" suffix="points/s" />
          <el-statistic title="队列深度" :value="getMetricValue('data_acquisition_queue_depth')" suffix="messages" />
          <el-statistic title="错误总数" :value="getMetricValue('data_acquisition_errors_total')" />
        </div>
      </el-card>

      <el-card v-for="(metric, metricName) in filteredMetrics" :key="metricName" class="metric-card" shadow="hover">
        <template #header>
          <div class="metric-header">
            <span class="metric-title">{{ getMetricTitle(metricName) }}</span>
            <el-tag v-if="metric?.type" :type="getMetricType(metric.type)">{{ metric.type }}</el-tag>
          </div>
        </template>

        <div v-if="metric?.data && metric.data.length > 0">
          <div class="metric-value">
            {{ formatMetricValue(metricName, getLatestValue(metric.data)?.value ?? getLatestValue(metric.data)) }}
          </div>

          <div class="metric-description" v-if="metric.help">{{ metric.help }}</div>

          <div class="metric-tags" v-if="getLatestValue(metric.data)?.labels">
            <el-tag
              v-for="(value, key) in getLatestValue(metric.data).labels"
              :key="key"
              size="small"
              style="margin-right: 8px; margin-bottom: 8px;"
            >
              <strong>{{ key }}:</strong> {{ value }}
            </el-tag>
          </div>

          <div class="chart-wrapper" v-if="metric.data.length > 1">
            <el-divider />
            <div :id="'chart-' + metricName" style="height: 250px;"></div>
          </div>

          <el-table v-if="metric.data.length > 0" :data="metric.data.slice(-10).reverse()" size="small" style="margin-top: 15px;" max-height="200">
            <el-table-column prop="value" label="值" width="140">
              <template #default="scope">
                {{ formatMetricValue(metricName, scope.row.value) }}
              </template>
            </el-table-column>
            <el-table-column label="标签" min-width="200">
              <template #default="scope">
                <el-tag v-for="(value, key) in scope.row.labels" :key="key" size="small" style="margin-right: 5px;">
                  {{ key }}: {{ value }}
                </el-tag>
              </template>
            </el-table-column>
          </el-table>
        </div>

        <el-empty v-else description="暂无数据" :image-size="80" />
      </el-card>
    </div>
  </div>
</template>

<script>
import { getJson } from "../api/http";
import * as echarts from "echarts";

export default {
  name: "MetricsView",
  data() {
    const coreMetrics = [
      "data_acquisition_collection_latency_ms",
      "data_acquisition_collection_rate",
      "data_acquisition_queue_depth",
      "data_acquisition_errors_total",
    ];

    const importantMetrics = [
      "data_acquisition_processing_latency_ms",
      "data_acquisition_write_latency_ms",
      "data_acquisition_batch_write_efficiency",
      "data_acquisition_connection_status_changes_total",
      "data_acquisition_connection_duration_seconds",
    ];

    let selectedMetrics = [];
    const storedSelected = localStorage.getItem("metrics-selected");
    if (storedSelected) {
      try {
        const parsed = JSON.parse(storedSelected);
        if (Array.isArray(parsed)) selectedMetrics = parsed.filter((x) => !coreMetrics.includes(x));
      } catch {
        // ignore
      }
    }

    return {
      loading: false,
      error: "",
      metrics: null,
      lastUpdate: "",
      coreMetrics,
      importantMetrics,
      selectedMetrics,
      searchKeyword: "",
      availableMetrics: [...importantMetrics],
      chartInstances: {},
    };
  },
  mounted() {
    this.loadMetrics();
    window.addEventListener("resize", this.resizeCharts);
  },
  beforeUnmount() {
    window.removeEventListener("resize", this.resizeCharts);
    Object.values(this.chartInstances).forEach((c) => c?.dispose?.());
  },
  watch: {
    selectedMetrics: {
      deep: true,
      handler(val) {
        localStorage.setItem("metrics-selected", JSON.stringify(val || []));
        this.$nextTick(() => this.renderCharts());
      },
    },
    searchKeyword() {
      this.$nextTick(() => this.renderCharts());
    },
  },
  computed: {
    filteredMetrics() {
      const result = {};
      if (!this.metrics) return result;

      let sourceKeys =
        this.selectedMetrics && this.selectedMetrics.length > 0 ? this.selectedMetrics : this.availableMetrics;

      if (this.searchKeyword && this.searchKeyword.trim()) {
        const keyword = this.searchKeyword.toLowerCase();
        sourceKeys = sourceKeys.filter((name) => {
          const title = this.getMetricTitle(name).toLowerCase();
          return title.includes(keyword) || name.toLowerCase().includes(keyword);
        });
      }

      sourceKeys.forEach((name) => {
        if (this.metrics[name]) result[name] = this.metrics[name];
        else
          result[name] = {
            type: "",
            help: "暂无数据",
            data: [],
          };
      });

      if (Object.keys(result).length === 0 && this.metrics) {
        Object.keys(this.metrics).forEach((name) => {
          if (!this.coreMetrics.includes(name)) result[name] = this.metrics[name];
        });
      }

      return result;
    },
  },
  methods: {
    async loadMetrics() {
      this.loading = true;
      this.error = "";
      try {
        const data = await getJson("/api/metrics-data");
        this.metrics = data.metrics || {};
        const keys = Object.keys(this.metrics || {});
        this.availableMetrics = Array.from(
          new Set([...this.importantMetrics, ...keys.filter((k) => !this.coreMetrics.includes(k))]),
        );
        this.lastUpdate = new Date(data.timestamp).toLocaleString("zh-CN");

        this.$nextTick(() => this.renderCharts());
      } catch (e) {
        this.error = e?.message || String(e);
        this.metrics = null;
      } finally {
        this.loading = false;
      }
    },
    renderCharts() {
      if (!this.metrics) return;

      Object.keys(this.filteredMetrics).forEach((metricName) => {
        const metric = this.filteredMetrics[metricName];
        if (!metric?.data || metric.data.length < 2) return;

        const chartId = "chart-" + metricName;
        const dom = document.getElementById(chartId);
        if (!dom) return;

        if (this.chartInstances[metricName]) this.chartInstances[metricName].dispose();
        const chart = echarts.init(dom);

        const values = metric.data.map((d) => d.value);
        const labels = metric.data.map((_, i) => `#${i + 1}`);

        chart.setOption({
          tooltip: {
            trigger: "axis",
            formatter: (params) => {
              const point = metric.data[params[0].dataIndex];
              let text = this.formatMetricValue(metricName, params[0].value);
              if (point?.labels) {
                text +=
                  "<br/>" +
                  Object.entries(point.labels)
                    .map(([k, v]) => `${k}: ${v}`)
                    .join("<br/>");
              }
              return text;
            },
          },
          grid: { left: "3%", right: "4%", bottom: "3%", containLabel: true },
          xAxis: { type: "category", data: labels, boundaryGap: false },
          yAxis: { type: "value", name: this.getMetricUnit(metricName) },
          series: [
            {
              name: this.getMetricTitle(metricName),
              type: "line",
              smooth: true,
              data: values,
              areaStyle: {
                color: {
                  type: "linear",
                  x: 0,
                  y: 0,
                  x2: 0,
                  y2: 1,
                  colorStops: [
                    { offset: 0, color: "rgba(64, 158, 255, 0.30)" },
                    { offset: 1, color: "rgba(64, 158, 255, 0.10)" },
                  ],
                },
              },
              lineStyle: { color: "#409eff", width: 2 },
              symbol: "circle",
              symbolSize: 6,
            },
          ],
        });

        this.chartInstances[metricName] = chart;
      });
    },
    resizeCharts() {
      Object.values(this.chartInstances).forEach((c) => c?.resize?.());
    },
    searchMetrics() {
      // 过滤由 computed 自动生效，这里主要触发重绘
      this.$nextTick(() => this.renderCharts());
    },
    clearFilters() {
      this.selectedMetrics = [];
      this.searchKeyword = "";
      localStorage.removeItem("metrics-selected");
      this.$nextTick(() => this.renderCharts());
    },
    getMetricTitle(metricName) {
      const titles = {
        data_acquisition_collection_latency_ms: "采集延迟",
        data_acquisition_collection_rate: "采集频率",
        data_acquisition_queue_depth: "队列深度（总）",
        data_acquisition_processing_latency_ms: "处理延迟",
        data_acquisition_write_latency_ms: "写入延迟",
        data_acquisition_batch_write_efficiency: "批量写入效率",
        data_acquisition_errors_total: "错误总数",
        data_acquisition_connection_status_changes_total: "连接状态变化",
        data_acquisition_connection_duration_seconds: "连接持续时间",
      };
      return titles[metricName] || metricName;
    },
    getMetricUnit(metricName) {
      if (metricName.includes("latency_ms") || metricName.includes("_ms")) return "毫秒";
      if (metricName.includes("_rate")) return "points/s";
      if (metricName.includes("_seconds")) return "秒";
      if (metricName.includes("_total")) return "次";
      if (metricName.includes("_depth")) return "消息数";
      return "";
    },
    formatMetricValue(metricName, value) {
      if (value && typeof value === "object" && "value" in value) value = value.value;
      if (typeof value !== "number") return value;

      const unit = this.getMetricUnit(metricName);
      const fixed = value.toFixed(2);

      if (unit === "毫秒") return fixed + " ms";
      if (unit === "points/s") return fixed + " points/s";
      if (unit === "秒") return fixed + " s";
      if (unit === "消息数") return fixed + " messages";
      if (unit === "次") return fixed + " 次";
      if (metricName.includes("efficiency")) return fixed + " points/ms";

      return fixed;
    },
    getLatestValue(data) {
      if (!data || data.length === 0) return { value: 0 };
      return data[data.length - 1];
    },
    getMetricValue(metricName) {
      const metric = this.metrics?.[metricName];
      if (!metric || !metric.data || metric.data.length === 0) return 0;
      const v = this.getLatestValue(metric.data).value;
      return typeof v === "number" ? v : 0;
    },
    getMetricType(type) {
      const types = { histogram: "success", counter: "warning", gauge: "info" };
      return types[type] || "";
    },
  },
};
</script>

<style scoped>
.page {
  max-width: 1400px;
  margin: 0 auto;
  padding: 20px;
  background: #f5f7fa;
  min-height: calc(100vh - 60px);
}
.page-header {
  background: white;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  padding: 20px;
  margin-bottom: 20px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.title {
  margin: 0;
  color: #303133;
  font-size: 20px;
  font-weight: 600;
}
.refresh-info {
  color: #909399;
  font-size: 12px;
  margin-top: 5px;
}
.header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}
.filter-card {
  margin-bottom: 20px;
}
.filter-actions {
  display: flex;
  gap: 10px;
}
.mb {
  margin-bottom: 20px;
}
.center {
  text-align: center;
  padding: 40px;
}
.metric-card {
  margin-bottom: 20px;
}
.metric-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0;
}
.metric-title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}
.metric-value {
  font-size: 28px;
  font-weight: bold;
  color: #409eff;
  margin: 10px 0;
}
.metric-description {
  font-size: 12px;
  color: #909399;
  margin-top: 5px;
}
.chart-wrapper {
  margin-top: 15px;
  height: 250px;
}
.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 20px;
  margin-bottom: 0;
}
.raw {
  color: #0b57d0;
  text-decoration: none;
}
</style>

