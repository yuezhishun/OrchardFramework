<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()

const menus = [
  { to: '/', label: '总览', exact: true },
  { to: '/tenants', label: '租户管理' },
  { to: '/features', label: '功能管理' },
  { to: '/feature-profiles', label: '功能模板' },
  { to: '/users', label: '用户管理' },
  { to: '/roles', label: '角色权限' },
  { to: '/permissions', label: '权限目录' },
  { to: '/site-settings', label: '站点设置' },
  { to: '/localization', label: '本地化' },
  { to: '/openid', label: 'OpenId' },
  { to: '/recipes', label: '配方管理' },
  { to: '/capabilities', label: '能力探测' },
  { to: '/admin-links', label: '管理入口' },
  { to: '/graphiql', label: 'GraphQL' },
]

const pageTitle = computed(() => {
  const matched = menus.find((item) => item.to === route.path)
  return matched?.label ?? '控制台'
})
</script>

<template>
  <div class="layout">
    <aside class="sidebar">
      <h1 class="brand">Orchard SaaS</h1>
      <RouterLink
        v-for="menu in menus"
        :key="menu.to"
        :to="menu.to"
        class="menu-item"
        :class="{ active: menu.exact ? route.path === menu.to : route.path.startsWith(menu.to) }"
      >
        {{ menu.label }}
      </RouterLink>
    </aside>

    <div class="content-shell">
      <header class="topbar">
        <div>
          <h2>{{ pageTitle }}</h2>
          <p class="subtle">Iteration H5: 增加 Recipes 管理与 OpenId 统计增强</p>
        </div>
      </header>

      <main class="content">
        <RouterView />
      </main>
    </div>
  </div>
</template>
