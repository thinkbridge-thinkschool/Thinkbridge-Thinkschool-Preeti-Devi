// Day 17 — Managed Identity proxy.
//
// This service runs as its own Azure Container App with a system-assigned
// Managed Identity (no client secret, no certificate — nothing stored
// anywhere). DefaultAzureCredential picks that identity up automatically at
// runtime via the platform-injected IDENTITY_ENDPOINT/IDENTITY_HEADER. This
// process is the ONLY place in the whole system that ever holds an
// Azure-AD-issued access token: it is acquired here, attached to the
// outbound request to the real Week-1 API, and never returned to the
// caller — the browser only ever sees the API's JSON response, exactly as
// if it had called the API directly.
const http = require('http');
const { DefaultAzureCredential } = require('@azure/identity');

const PORT = process.env.PORT || 8080;
const API_BASE_URL = process.env.WEEK1_API_BASE_URL;
const TOKEN_SCOPE = process.env.MI_TOKEN_SCOPE;
const ALLOWED_ORIGIN = process.env.ALLOWED_ORIGIN || '';

const credential = new DefaultAzureCredential();

function setCors(res) {
  if (ALLOWED_ORIGIN) {
    res.setHeader('Access-Control-Allow-Origin', ALLOWED_ORIGIN);
    res.setHeader('Vary', 'Origin');
  }
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, DELETE, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let data = '';
    req.on('data', (chunk) => (data += chunk));
    req.on('end', () => resolve(data));
    req.on('error', reject);
  });
}

const server = http.createServer(async (req, res) => {
  setCors(res);

  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    res.end();
    return;
  }

  const url = new URL(req.url, `http://localhost:${PORT}`);

  if (url.pathname === '/health') {
    res.writeHead(200, { 'Content-Type': 'text/plain' });
    res.end('Healthy');
    return;
  }

  const deleteMatch = url.pathname.match(/^\/proxy\/quotes\/(\d+)$/);
  if (!deleteMatch && url.pathname !== '/proxy/quotes') {
    res.writeHead(404, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ title: 'Not Found' }));
    return;
  }

  try {
    const tokenResponse = await credential.getToken(TOKEN_SCOPE);

    const upstreamUrl = deleteMatch
      ? new URL(`${API_BASE_URL}/api/quotes/${deleteMatch[1]}`)
      : new URL(`${API_BASE_URL}/api/quotes`);
    if (req.method === 'GET') {
      for (const [key, value] of url.searchParams.entries()) {
        upstreamUrl.searchParams.set(key, value);
      }
    }

    const init = {
      method: req.method,
      headers: {
        Authorization: `Bearer ${tokenResponse.token}`,
        'Content-Type': 'application/json',
      },
    };

    if (req.method === 'POST') {
      init.body = await readBody(req);
    }

    const upstream = await fetch(upstreamUrl.toString(), init);
    const text = await upstream.text();

    console.log(
      `MI proxy -> ${req.method} ${upstreamUrl.pathname} -> ${upstream.status}`
    );

    res.writeHead(upstream.status, { 'Content-Type': 'application/json' });
    res.end(text);
  } catch (err) {
    console.error('MI proxy failed to reach the real Week-1 API:', err.message);
    res.writeHead(502, { 'Content-Type': 'application/json' });
    res.end(
      JSON.stringify({
        title: 'Bad Gateway',
        detail: 'The Managed Identity proxy could not reach the Week-1 API.',
      })
    );
  }
});

server.listen(PORT, () => {
  console.log(`MI proxy listening on port ${PORT}`);
});
