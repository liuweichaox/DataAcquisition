<template>
  <div>
    <nav class="nav">
      <router-link to="/edges">Edges</router-link>
      <a href="/metrics" target="_blank" rel="noreferrer">Central metrics</a>
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

