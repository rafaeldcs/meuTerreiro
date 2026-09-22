const api = process.env.INTERNAL_API_ORIGIN || 'http://127.0.0.1:5080';
const config = {
  output: 'standalone', poweredByHeader: false,
  async rewrites() { return [{ source: '/api/:path*', destination: `${api}/api/:path*` }]; },
  async headers() {
    return [
      { source: '/:path*', headers: [
        { key: 'X-Content-Type-Options', value: 'nosniff' },
        { key: 'X-Frame-Options', value: 'DENY' },
        { key: 'Referrer-Policy', value: 'same-origin' },
        { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' }
      ] },
      { source: '/sw.js', headers: [{ key: 'Cache-Control', value: 'no-cache, no-store, must-revalidate' }, { key: 'Service-Worker-Allowed', value: '/' }] },
      { source: '/push-policy.js', headers: [{ key: 'Cache-Control', value: 'no-cache' }] }
    ];
  }
};
export default config;
