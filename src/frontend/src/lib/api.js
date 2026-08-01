export function getApiBaseUrl() {
  // Provided by docker-entrypoint.sh at container runtime.
  const v = window.__VITE_API_URL__;
  if (typeof v !== 'string') return '';
  return v.trim();
}

export function toApiUrl(apiBaseUrl, path) {
  if (!apiBaseUrl) return path;
  const trimmed = apiBaseUrl.endsWith('/') ? apiBaseUrl.slice(0, -1) : apiBaseUrl;
  const p = path.startsWith('/') ? path : `/${path}`;
  return `${trimmed}${p}`;
}

export async function readProblemMessage(response) {
  try {
    const data = await response.json();
    if (data?.detail) return String(data.detail);
    if (data?.title) return String(data.title);
    return `Request failed with status ${response.status}`;
  } catch {
    return `Request failed with status ${response.status}`;
  }
}
