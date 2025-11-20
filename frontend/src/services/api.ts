import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Add token to requests
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Auth
export const authApi = {
  checkInit: () => api.get('/auth/check-init'),
  initAdmin: (data: { username: string; email: string; password: string }) =>
    api.post('/auth/init', data),
  login: (data: { username: string; password: string }) =>
    api.post('/auth/login', data),
  getMe: () => api.get('/auth/me'),
};

// Servers
export const serversApi = {
  getAll: () => api.get('/servers'),
  getById: (id: number) => api.get(`/servers/${id}`),
  create: (data: any) => api.post('/servers', data),
  update: (id: number, data: any) => api.put(`/servers/${id}`, data),
  delete: (id: number) => api.delete(`/servers/${id}`),
  testConnection: (id: number) => api.post(`/servers/${id}/test-connection`),
  start: (id: number) => api.post(`/servers/${id}/start`),
  stop: (id: number) => api.post(`/servers/${id}/stop`),
  getStatus: (id: number) => api.get(`/servers/${id}/status`),
};

// Databases
export const databasesApi = {
  getAll: (serverId: number) => api.get(`/servers/${serverId}/databases`),
  create: (serverId: number, data: { name: string; charset?: string; collation?: string }) =>
    api.post(`/servers/${serverId}/databases`, data),
  delete: (serverId: number, dbName: string) =>
    api.delete(`/servers/${serverId}/databases/${dbName}`),
  getTables: (serverId: number, dbName: string) =>
    api.get(`/servers/${serverId}/databases/${dbName}/tables`),
  getTableSchema: (serverId: number, dbName: string, tableName: string) =>
    api.get(`/servers/${serverId}/databases/${dbName}/tables/${tableName}/schema`),
  executeQuery: (serverId: number, dbName: string, query: string) =>
    api.post(`/servers/${serverId}/databases/${dbName}/query`, { query }),
};

export default api;
