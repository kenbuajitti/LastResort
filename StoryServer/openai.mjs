import { StoryError, instructions } from './story.mjs';

export function makeSchema(ending) {
  return {
    type: 'object', additionalProperties: false,
    properties: {
      title: { type: 'string' }, narrative: { type: 'string' },
      choices: { type: 'array', items: { type: 'string' }, minItems: ending ? 0 : 5, maxItems: ending ? 0 : 5 },
      epilogue: { type: 'string' }
    },
    required: ['title', 'narrative', 'choices', 'epilogue']
  };
}

export function createGenerator({ apiKey, model = 'gpt-4.1-mini', fetchFn = fetch, timeoutMs = 90000 }) {
  return async context => {
    if (!apiKey || apiKey === 'replace_with_your_key')
      throw new StoryError(503, 'Add an OpenAI API key to StoryServer/.env, then restart the server.');
    let response;
    try {
      response = await fetchFn('https://api.openai.com/v1/responses', {
        method: 'POST',
        headers: { Authorization: `Bearer ${apiKey}`, 'Content-Type': 'application/json' },
        signal: AbortSignal.timeout(timeoutMs),
        body: JSON.stringify({
          model, store: false, instructions, input: JSON.stringify(context),
          max_output_tokens: context.isEnding ? 2400 : 1800,
          text: { format: { type: 'json_schema', name: context.isEnding ? 'last_resort_ending' : 'last_resort_turn',
            strict: true, schema: makeSchema(context.isEnding) } }
        })
      });
    } catch {
      throw new StoryError(504, 'The storyteller could not be reached in time. Your choice has not advanced; retry.');
    }
    if (!response.ok) {
      const message = response.status === 401 ? 'The server API key was rejected. Check StoryServer/.env and restart.'
        : response.status === 429 ? 'The API has reached a rate or credit limit. Check your API account, then retry.'
        : response.status === 400 || response.status === 404 ? 'Check the server model setting and API model access.'
        : 'The storyteller is temporarily unavailable. Please retry.';
      throw new StoryError(502, message);
    }
    let result;
    try { result = await response.json(); } catch { throw new StoryError(502, 'The storyteller returned an unreadable response. Retry.'); }
    if (!result || result.status !== 'completed') throw new StoryError(502, 'The storyteller did not finish the scene. Please retry.');
    if (!Array.isArray(result.output)) throw new StoryError(502, 'The storyteller returned an unreadable scene. Please retry.');
    const content = result.output.filter(item => item && item.type === 'message').flatMap(item => Array.isArray(item.content) ? item.content : []);
    if (content.some(item => item.type === 'refusal')) throw new StoryError(502, 'The storyteller could not continue this scene. Retry or begin a new stay.');
    const raw = content.filter(item => item.type === 'output_text').map(item => item.text).join('');
    try { return JSON.parse(raw); }
    catch { throw new StoryError(502, 'The storyteller returned an unreadable scene. Please retry.'); }
  };
}
