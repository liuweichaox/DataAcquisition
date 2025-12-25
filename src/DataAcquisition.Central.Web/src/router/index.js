import { createRouter, createWebHistory } from "vue-router";
import EdgesView from "../views/EdgesView.vue";
import MetricsView from "../views/MetricsView.vue";
import LogsView from "../views/LogsView.vue";

const routes = [
  { path: "/", redirect: "/edges" },
  { path: "/edges", name: "edges", component: EdgesView },
  { path: "/metrics", name: "metrics", component: MetricsView },
  { path: "/logs", name: "logs", component: LogsView },
];

export default createRouter({
  history: createWebHistory(),
  routes,
});

