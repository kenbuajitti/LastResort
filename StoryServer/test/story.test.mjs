import test from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { StoryEngine, StoryError } from '../story.mjs';
import { createGenerator } from '../openai.mjs';
import { createServer } from '../server.mjs';

const id = () => randomUUID().replaceAll('-', '');
const first = () => ({ sessionId: id(), requestId: id(), expectedTurn: 0, choiceId: '' });
const next = (reply, index = 0) => ({ sessionId: reply.sessionId, requestId: id(), expectedTurn: reply.completed, choiceId: reply.choices[index].id });
const scene = context => ({ title: `Fixture turn ${context.completed}`, narrative: 'Test fixture narrative, not a live story.',
  choices: context.isEnding ? [] : ['Talk to Felix', 'Talk to Penny', 'Find Rosa', 'Inspect the ledger', 'Follow Tamsin'],
  epilogue: context.isEnding ? 'Test fixture ending.' : '' });
const rejectsStatus = (promise, status) => assert.rejects(promise, error => error instanceof StoryError && error.status === status);

test('complete game: opening plus 20 choices, fixed mystery, exact ending and private state', async () => {
  const contexts = [];
  const engine = new StoryEngine(async context => { contexts.push(context); return scene(context); });
  let response = await engine.advance(first());
  assert.equal(response.completed, 0);
  for (let i = 1; i <= 20; i++) {
    const request = next(response, i % 5);
    response = await engine.advance(request);
    assert.equal(response.completed, i);
    assert.equal(response.day, Math.min(5, Math.floor(i / 4) + 1));
    assert.equal(response.choices.length, i === 20 ? 0 : 5);
    assert.equal(response.isEnding, i === 20);
    assert.equal(contexts[i].history.length, i);
    assert.equal(contexts[i].acceptedChoice.id, request.choiceId);
    assert.deepEqual(contexts[i].mystery, contexts[0].mystery);
    assert.ok(!Object.hasOwn(response, 'mystery'));
  }
  assert.equal(contexts.length, 21);
  assert.ok(response.epilogue);
  await rejectsStatus(engine.advance({ sessionId: response.sessionId, requestId: id(), expectedTurn: 20, choiceId: 't20_c1' }), 409);
  assert.equal(contexts.length, 21);
});

test('concurrent duplicates and replay of a lost response invoke generation only once', async () => {
  let release;
  let calls = 0;
  const engine = new StoryEngine(async c => { calls++; await new Promise(r => { release = r; }); return scene(c); });
  const request = first();
  const a = engine.advance(request);
  const b = engine.advance(request);
  await rejectsStatus(engine.advance({ ...request, requestId: id() }), 409);
  release();
  const [x, y] = await Promise.all([a, b]);
  assert.deepEqual(x, y);
  x.title = 'Mutation outside server';
  const replay = await engine.advance(request);
  assert.equal(replay.title, y.title);
  assert.equal(calls, 1);
  await rejectsStatus(engine.advance({ ...request, choiceId: 'different' }), 409);
});

test('invalid and stale choices never call the generator', async () => {
  let calls = 0;
  const engine = new StoryEngine(async c => { calls++; return scene(c); });
  const opening = await engine.advance(first());
  await rejectsStatus(engine.advance({ ...next(opening), choiceId: 'invented' }), 400);
  const second = await engine.advance(next(opening));
  await rejectsStatus(engine.advance(next(opening, 1)), 409);
  await rejectsStatus(engine.advance({ ...next(second), expectedTurn: 10 }), 409);
  assert.equal(calls, 2);
});

test('generation failures and malformed scenes do not commit a decision; same request can retry', async () => {
  let bad = false;
  let exception = false;
  const engine = new StoryEngine(async c => {
    if (exception) throw new StoryError(504, 'Simulated timeout');
    const output = scene(c);
    if (bad) output.choices = ['Duplicate', 'Duplicate', 'Three', 'Four', 'Five'];
    return output;
  });
  const opening = await engine.advance(first());
  const request = next(opening);
  exception = true;
  await rejectsStatus(engine.advance(request), 504);
  exception = false; bad = true;
  await rejectsStatus(engine.advance(request), 502);
  assert.equal(engine.sessions.get(opening.sessionId).current.completed, 0);
  assert.equal(engine.sessions.get(opening.sessionId).history.length, 1);
  bad = false;
  const reply = await engine.advance(request);
  assert.equal(reply.completed, 1);
  assert.equal(engine.sessions.get(opening.sessionId).history.length, 2);
  assert.deepEqual(await engine.advance(request), reply);
});

test('bad final epilogue is rejected without completing turn 20', async () => {
  let bad = true;
  const engine = new StoryEngine(async c => ({ ...scene(c), epilogue: c.isEnding && bad ? '' : scene(c).epilogue }));
  let reply = await engine.advance(first());
  for (let i = 1; i < 20; i++) reply = await engine.advance(next(reply));
  const finalRequest = next(reply);
  await rejectsStatus(engine.advance(finalRequest), 502);
  assert.equal(engine.sessions.get(reply.sessionId).current.completed, 19);
  bad = false;
  assert.equal((await engine.advance(finalRequest)).isEnding, true);
});

test('server restart/expiry does not invent replacement history', async () => {
  let clock = 100;
  const engine = new StoryEngine(async c => scene(c), { now: () => clock, ttlMs: 100, maxSessions: 1 });
  const reply = await engine.advance(first());
  await rejectsStatus(engine.advance(first()), 429);
  clock = 300;
  await rejectsStatus(engine.advance(next(reply)), 410);
  assert.equal((await engine.advance(first())).completed, 0);
});

test('REST provider uses strict schema, full context and parses output message items', async () => {
  let sent;
  const generator = createGenerator({ apiKey: 'test-key-only', fetchFn: async (url, options) => {
    assert.equal(url, 'https://api.openai.com/v1/responses');
    sent = JSON.parse(options.body);
    return { ok: true, json: async () => ({ status: 'completed', output: [
      { type: 'reasoning', summary: [] },
      { type: 'message', content: [{ type: 'output_text', text: JSON.stringify(scene({ completed: 0, isEnding: false })) }] }
    ] }) };
  } });
  const context = { completed: 0, isEnding: false, mystery: { culprit: 'test' }, history: [] };
  const result = await generator(context);
  assert.equal(result.choices.length, 5);
  assert.equal(sent.store, false);
  assert.equal(sent.text.format.strict, true);
  assert.equal(sent.text.format.schema.properties.choices.minItems, 5);
  assert.deepEqual(JSON.parse(sent.input), context);
});

test('REST provider handles refusal, truncation, bad JSON, API errors, no key and transport failure', async () => {
  for (const response of [
    { ok: false, status: 401 }, { ok: false, status: 429 }, { ok: false, status: 500 },
    { ok: true, json: async () => ({ status: 'incomplete' }) },
    { ok: true, json: async () => ({ status: 'completed', output: [{ type: 'message', content: [{ type: 'refusal' }] }] }) },
    { ok: true, json: async () => ({ status: 'completed', output: [] }) },
    { ok: true, json: async () => null },
    { ok: true, json: async () => ({ status: 'completed', output: {} }) },
    { ok: true, json: async () => { throw new Error('bad'); } }
  ]) {
    const generate = createGenerator({ apiKey: 'test-key-only', fetchFn: async () => response });
    await rejectsStatus(generate({ isEnding: false }), 502);
  }
  await rejectsStatus(createGenerator({ apiKey: '' })({}), 503);
  await rejectsStatus(createGenerator({ apiKey: 'test-key-only', fetchFn: async () => { throw new Error('offline'); } })({}), 504);
});

test('HTTP integration: Unity-shaped requests, replay, local origin boundary and malformed body', async t => {
  let calls = 0;
  const engine = new StoryEngine(async c => { calls++; return scene(c); });
  const server = createServer(engine);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => { server.closeAllConnections(); server.close(); });
  const url = `http://127.0.0.1:${server.address().port}`;
  const post = (payload, headers = {}) => fetch(url + '/v1/story', {
    method: 'POST', headers: { 'Content-Type': 'application/json', ...headers }, body: typeof payload === 'string' ? payload : JSON.stringify(payload)
  });
  assert.equal((await fetch(url + '/health')).status, 200);
  const req = first();
  const response = await post(req);
  assert.equal(response.status, 200);
  const firstReply = await response.json();
  assert.deepEqual(await (await post(req)).json(), firstReply);
  const nextResponse = await post(next(firstReply));
  assert.equal((await nextResponse.json()).completed, 1);
  assert.equal(calls, 2);
  assert.equal((await post(first(), { Origin: 'https://untrusted.example' })).status, 403);
  assert.equal((await post('{invalid')).status, 400);
  assert.equal((await post('x'.repeat(5000))).status, 413);
  assert.equal((await post({ sessionId: 'bad' })).status, 400);
  assert.equal((await post({ ...first(), sessionId: [id()] })).status, 400);
});
