// Moto.Marketplace.Web/src/api/client.ts
const BASE = 'https://marketplace.moto-editor.dev/api/v1';

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const token = localStorage.getItem('moto_token');
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  });
  if (!res.ok) throw new Error(`API ${res.status}`);
  return res.json();
}

export const login = (u: string, p: string) =>
  apiFetch<{ token: string }>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username: u, password: p }),
  });

export const getCatalog = (q?: string) =>
  apiFetch<any[]>(`/plugins${q ? `?q=${encodeURIComponent(q)}` : ''}`);

export const submitPlugin = (payload: any) =>
  apiFetch('/plugins/submit', {
    method: 'POST',
    body: JSON.stringify(payload),
  });
