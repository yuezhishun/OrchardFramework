<script setup lang="ts">
const graphQlApiUrl = `${import.meta.env.BASE_URL}api/graphql`
</script>

<template>
  <section>
    <div class="panel-header">
      <h3>GraphQL / 调试入口</h3>
    </div>

    <article class="panel">
      <p>
        当前管理能力已进入 H5：配方管理、站点设置、本地化与 OpenId 管理均通过 Vue + Management API 完成。
      </p>
      <p class="subtle">如已启用 GraphQL 模块，可直接访问后端 GraphQL 端点进行查询联调。</p>
      <div class="action-row">
        <a class="inline-link" :href="graphQlApiUrl" target="_blank" rel="noreferrer">打开 GraphQL API</a>
        <RouterLink class="inline-link" to="/capabilities">查看能力矩阵</RouterLink>
        <RouterLink class="inline-link" to="/site-settings">打开站点设置</RouterLink>
        <RouterLink class="inline-link" to="/localization">打开本地化</RouterLink>
        <RouterLink class="inline-link" to="/openid">打开 OpenId 管理</RouterLink>
        <RouterLink class="inline-link" to="/recipes">打开配方管理</RouterLink>
        <RouterLink class="inline-link" to="/tenants">打开租户管理</RouterLink>
      </div>
    </article>

    <article class="panel">
      <h4>联调建议</h4>
      <p class="subtle">先通过「能力探测」确认模块启用，再进行 GraphQL 查询和 OpenId 流程联调。</p>
    </article>
  </section>
</template>
