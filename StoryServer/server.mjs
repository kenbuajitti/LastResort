import http from 'node:http';
import { pathToFileURL } from 'node:url';
import { StoryEngine, StoryError } from './story.mjs';
import { createGenerator } from './openai.mjs';
import { FileStore, limitedGenerator } from './storage.mjs';

// Local by default; production requires explicit origins and durable storage.
export function createServer(engine, { publicMode = false, allowedOrigins = [] } = {}) {
  let active = 0;
  const starts = [];
  return http.createServer(async (req, res) => {
    const send = (code, payload) => {
      res.writeHead(code, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
      res.end(JSON.stringify(payload));
    };
    if (!publicMode && !/^(127\.0\.0\.1|localhost)(:\d+)?$/i.test(req.headers.host || '')) return send(403, { error: 'Local connections only.' });
    const origin = req.headers.origin;
    if (origin) {
      if (!(publicMode ? allowedOrigins.includes(origin) : /^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/i.test(origin))) return send(403, { error: 'This game origin is not allowed.' });
      res.setHeader('Access-Control-Allow-Origin', origin);
      res.setHeader('Vary', 'Origin');
      res.setHeader('Access-Control-Allow-Methods', 'POST, GET, OPTIONS');
      res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    }
    if (req.method === 'OPTIONS') { res.writeHead(204); return res.end(); }
    if (req.url === '/health' && req.method === 'GET') return send(200, { status: 'ok', service: 'Last Resort', localOnly: !publicMode });
    if (req.url !== '/v1/story' || req.method !== 'POST') return send(404, { error: 'Unknown route.' });
    if (!(req.headers['content-type'] || '').startsWith('application/json')) return send(415, { error: 'JSON required.' });
    if (active >= 4) return send(429, { error: 'The story server is busy. Retry shortly.' });
    const now = Date.now();
    while (starts.length && starts[0] < now - 60000) starts.shift();
    if (starts.length >= 60) return send(429, { error: 'Too many requests. Wait a minute before retrying.' });
    starts.push(now);
    active++;
    try {
      let body = '';
      let bytes = 0;
      for await (const chunk of req) {
        bytes += chunk.length;
        if (bytes > 4096) throw new StoryError(413, 'Request is too large.');
        body += chunk.toString('utf8');
      }
      let input;
      try { input = JSON.parse(body); } catch { throw new StoryError(400, 'Invalid JSON.'); }
      send(200, await engine.advance(input));
    } catch (error) {
      send(error instanceof StoryError ? error.status : 500,
        { error: error instanceof StoryError ? error.message : 'The story service encountered an error. Please retry.' });
    } finally { active--; }
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const key = process.env.OPENAI_API_KEY;
  if (!key || key === 'replace_with_your_key') {
    console.error('Add your OpenAI API key to StoryServer/.env before starting. See README.md.');
    process.exitCode = 1;
  } else {
    const port = Number(process.env.PORT || 8787);
    if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('PORT must be between 1024 and 65535.');
    const publicMode = process.env.NODE_ENV === 'production';
    const allowedOrigins = (process.env.ALLOWED_ORIGINS || '').split(',').map(s => s.trim()).filter(Boolean);
    if (publicMode && (!allowedOrigins.length || !process.env.DATA_DIR)) throw new Error('Production requires ALLOWED_ORIGINS and DATA_DIR.');
    for (const origin of allowedOrigins) {
      if (new URL(origin).origin !== origin || !origin.startsWith('https://')) throw new Error('Use exact HTTPS origins, without paths or trailing slashes.');
    }
    const store = new FileStore(process.env.DATA_DIR || './data');
    const dailyLimit = Number(process.env.MAX_GENERATIONS_PER_DAY || 200);
    if (!Number.isInteger(dailyLimit) || dailyLimit < 1) throw new Error('MAX_GENERATIONS_PER_DAY must be a positive integer.');
    const generate = limitedGenerator(createGenerator({ apiKey: key, model: process.env.OPENAI_MODEL || 'gpt-4.1-mini' }), store, dailyLimit);
    const engine = new StoryEngine(generate, { store });
    const server = createServer(engine, { publicMode, allowedOrigins });
    server.requestTimeout = 110000;
    server.headersTimeout = 10000;
    server.on('error', error => {
      console.error(error.code === 'EADDRINUSE' ? 'Port is already in use. Close the other Last Resort server and try again.' : 'Could not start the local server.');
      process.exitCode = 1;
    });
    server.listen(port, publicMode ? '0.0.0.0' : '127.0.0.1', () => {
      console.log(`StoryIQ server listening on port ${port} (${publicMode ? 'production' : 'local'}).`);
      console.log('Keep this window open while testing in Unity. Press Ctrl+C to stop.');
    });
    process.on('SIGTERM', () => { server.close(() => process.exit(0)); setTimeout(() => process.exit(0), 100000).unref(); });
  }
}
