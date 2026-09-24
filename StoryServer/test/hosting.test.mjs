import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { StoryEngine } from '../story.mjs';
import { FileStore, limitedGenerator } from '../storage.mjs';
import { createServer } from '../server.mjs';
const scene = async () => ({ title:'Arrival', narrative:'An island.', choices:['A','B','C','D','E'], epilogue:'' });
const request = { sessionId:'a'.repeat(32), requestId:'b'.repeat(32), expectedTurn:0, choiceId:'' };
function disk(t) { const dir=mkdtempSync(join(tmpdir(),'story-')); t.after(()=>rmSync(dir,{recursive:true,force:true})); return new FileStore(dir); }
test('restart preserves mystery, history and retry cache without another paid call', async t => {
  const store=disk(t); const first=new StoryEngine(scene,{store});
  const reply=await first.advance(request);
  const restored=new StoryEngine(async()=>{throw Error('must not generate');},{store});
  assert.deepEqual(await restored.advance(request),reply);
  assert.deepEqual(restored.sessions.get(request.sessionId).mystery,first.sessions.get(request.sessionId).mystery);
});
test('failed disk commit does not advance accepted state', async () => {
  let broken=false;
  const engine=new StoryEngine(scene,{store:{read:()=>null,write:()=>{if(broken)throw Error('disk full');}}});
  const first=await engine.advance(request); broken=true;
  await assert.rejects(engine.advance({...request,requestId:'c'.repeat(32),choiceId:first.choices[0].id}),/disk full/);
  assert.equal(engine.sessions.get(request.sessionId).current.completed,0);
});
test('daily cap counts failed calls, survives restart and resets on UTC day', async t => {
  const store=disk(t); let day=new Date('2026-09-24T00:00:00Z');
  const limited=limitedGenerator(async()=>{throw Error('provider failed');},store,1,()=>day);
  await assert.rejects(limited({}),/provider failed/);
  const restarted=limitedGenerator(scene,store,1,()=>day);
  await assert.rejects(restarted({}),e=>e.status===429);
  day=new Date('2026-09-25T00:00:00Z'); assert.equal((await restarted({})).title,'Arrival');
});
test('public HTTP supports exact game origin and preflight; rejects other origins', async t => {
  const server=createServer(new StoryEngine(scene),{publicMode:true,allowedOrigins:['https://html-classic.itch.zone']});
  await new Promise(r=>server.listen(0,'127.0.0.1',r)); t.after(()=>{server.closeAllConnections();server.close();});
  const url=`http://127.0.0.1:${server.address().port}`;
  const pre=await fetch(url+'/v1/story',{method:'OPTIONS',headers:{Origin:'https://html-classic.itch.zone'}});
  assert.equal(pre.status,204); assert.equal(pre.headers.get('access-control-allow-origin'),'https://html-classic.itch.zone');
  assert.equal((await fetch(url+'/v1/story',{method:'POST',headers:{Origin:'https://html-classic.itch.zone','Content-Type':'application/json'},body:JSON.stringify(request)})).status,200);
  assert.equal((await fetch(url+'/health',{headers:{Origin:'https://evil.example'}})).status,403);
  assert.equal((await (await fetch(url+'/health')).json()).localOnly,false);
});
