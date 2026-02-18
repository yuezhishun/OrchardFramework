import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  {
    path: '/',
    component: () => import('../components/AppShell.vue'),
    children: [
      {
        path: '',
        name: 'dashboard',
        component: () => import('../pages/DashboardPage.vue'),
      },
      {
        path: 'features',
        name: 'features',
        component: () => import('../pages/FeaturesPage.vue'),
      },
      {
        path: 'tenants',
        name: 'tenants',
        component: () => import('../pages/TenantsPage.vue'),
      },
      {
        path: 'feature-profiles',
        name: 'feature-profiles',
        component: () => import('../pages/FeatureProfilesPage.vue'),
      },
      {
        path: 'users',
        name: 'users',
        component: () => import('../pages/UsersPage.vue'),
      },
      {
        path: 'roles',
        name: 'roles',
        component: () => import('../pages/RolesPage.vue'),
      },
      {
        path: 'permissions',
        name: 'permissions',
        component: () => import('../pages/PermissionsPage.vue'),
      },
      {
        path: 'site-settings',
        name: 'site-settings',
        component: () => import('../pages/SiteSettingsPage.vue'),
      },
      {
        path: 'localization',
        name: 'localization',
        component: () => import('../pages/LocalizationPage.vue'),
      },
      {
        path: 'openid',
        name: 'openid',
        component: () => import('../pages/OpenIdPage.vue'),
      },
      {
        path: 'recipes',
        name: 'recipes',
        component: () => import('../pages/RecipesPage.vue'),
      },
      {
        path: 'capabilities',
        name: 'capabilities',
        component: () => import('../pages/CapabilitiesPage.vue'),
      },
      {
        path: 'admin-links',
        name: 'admin-links',
        component: () => import('../pages/AdminLinksPage.vue'),
      },
      {
        path: 'graphiql',
        name: 'graphiql',
        component: () => import('../pages/GraphiqlPage.vue'),
      },
    ],
  },
]

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})
