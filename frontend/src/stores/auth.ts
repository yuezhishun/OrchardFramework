import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { authApi } from '../api/service'

const TOKEN_KEY = 'orchard_auth_token'
const PROFILE_KEY = 'orchard_auth_profile'

interface Profile {
  userId: number
  tenantId: number
  username: string
  roles: string[]
  permissions: string[]
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string>('')
  const profile = ref<Profile | null>(null)

  const isAuthenticated = computed(() => Boolean(token.value))
  const isPlatformAdmin = computed(() => profile.value?.roles.includes('PlatformAdmin') ?? false)

  const initialize = () => {
    const savedToken = localStorage.getItem(TOKEN_KEY)
    const savedProfile = localStorage.getItem(PROFILE_KEY)

    token.value = savedToken ?? ''
    profile.value = savedProfile ? (JSON.parse(savedProfile) as Profile) : null
  }

  const login = async (username: string, password: string) => {
    const response = await authApi.login(username, password)

    token.value = response.token
    profile.value = {
      userId: response.userId,
      tenantId: response.tenantId,
      username: response.username,
      roles: response.roles,
      permissions: response.permissions,
    }

    localStorage.setItem(TOKEN_KEY, response.token)
    localStorage.setItem(PROFILE_KEY, JSON.stringify(profile.value))
  }

  const logout = () => {
    token.value = ''
    profile.value = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(PROFILE_KEY)
  }

  return {
    token,
    profile,
    isAuthenticated,
    isPlatformAdmin,
    initialize,
    login,
    logout,
  }
})
