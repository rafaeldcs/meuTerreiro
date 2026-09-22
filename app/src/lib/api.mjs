export async function api(path, options = {}) {
  const response = await fetch(`/api${path}`, {
    credentials: 'same-origin', cache: 'no-store', ...options,
    headers: { ...(typeof FormData !== 'undefined' && options.body instanceof FormData ? {} : {'Content-Type': 'application/json'}), 'X-Terreiro-Client': 'app', ...options.headers }
  });
  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    if (response.status === 401 && typeof window !== 'undefined') window.dispatchEvent(new Event('session-expired'));
    const failure = new Error(error.message || (response.status === 429 ? 'Muitas tentativas. Aguarde antes de tentar novamente.' : 'Não foi possível concluir esta ação.'));
    failure.code = error.code; failure.status = response.status;
    throw failure;
  }
  return response.status === 204 ? null : response.json();
}
export const post = (path, body = {}) => api(path, { method: 'POST', body: JSON.stringify(body) });
