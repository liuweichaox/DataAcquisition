import { createApp } from "vue";
import App from "./App.vue";
import router from "./router";
import ElementPlus from "element-plus";
import "element-plus/dist/index.css";
import "./styles/global.css";
import * as ElementPlusIconsVue from "@element-plus/icons-vue";
import zhCn from "element-plus/dist/locale/zh-cn.mjs";

const app = createApp(App);

// 注册所有图标
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component);
}

app.use(router);
app.use(ElementPlus, {
  locale: zhCn,
});

app.mount("#app");
