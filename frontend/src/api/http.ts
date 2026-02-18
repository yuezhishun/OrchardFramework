import axios from 'axios'

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? `${import.meta.env.BASE_URL}api`

export const http = axios.create({
  baseURL: baseUrl,
  timeout: 10000,
})

http.interceptors.request.use((config) => {
  const token = localStorage.getItem('orchard_auth_token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})
