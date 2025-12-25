import { createRouter, createWebHistory } from "vue-router";
import EdgesView from "../views/EdgesView.vue";

const routes = [
  { path: "/", redirect: "/edges" },
  { path: "/edges", name: "edges", component: EdgesView },
];

export default createRouter({
  history: createWebHistory(),
  routes,
});

