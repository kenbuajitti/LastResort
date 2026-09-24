import { mkdirSync, readFileSync, writeFileSync, renameSync } from 'node:fs';
import { join } from 'node:path';
import { StoryError } from './story.mjs';

// One process on one persistent disk. Do not scale to multiple instances.
export class FileStore {
  constructor(directory) { this.directory = directory; mkdirSync(directory, { recursive: true }); }
  read(name) {
    try { return JSON.parse(readFileSync(join(this.directory, name + '.json'), 'utf8')); }
    catch (error) { if (error.code === 'ENOENT') return null; throw error; }
  }
  write(name, value) {
    const file = join(this.directory, name + '.json');
    writeFileSync(file + '.tmp', JSON.stringify(value), { mode: 0o600, flush: true });
    renameSync(file + '.tmp', file);
  }
}

export function limitedGenerator(generate, store, limit, now = () => new Date()) {
  let budget = store.read('budget') || { day: '', count: 0 };
  return async context => {
    const day = now().toISOString().slice(0, 10);
    if (budget.day !== day) budget = { day, count: 0 };
    if (budget.count >= limit) throw new StoryError(429, 'Today’s story allowance has been reached. Please return tomorrow (UTC).');
    // Reserve before calling the provider, including failures; survives restarts.
    const next = { day, count: budget.count + 1 };
    store.write('budget', next);
    budget = next;
    return generate(context);
  };
}
