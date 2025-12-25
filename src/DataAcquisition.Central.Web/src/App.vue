<template>
  <div>
    <nav class="nav">
      <router-link to="/edges">Edges</router-link>
      <router-link to="/metrics">Edge Metrics</router-link>
      <router-link to="/logs">Edge Logs</router-link>
      <a href="/metrics" target="_blank" rel="noreferrer">Central /metrics</a>
    </nav>
    <main class="main">
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.nav {
  display: flex;
  gap: 12px;
  padding: 12px 16px;
  border-bottom: 1px solid #eee;
  flex-wrap: wrap;
}
.main {
  padding: 0;
}
a,
.nav :deep(a) {
  color: #0b57d0;
  text-decoration: none;
}
</style>

