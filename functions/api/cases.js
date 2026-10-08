const TYPES = ['vague', 'fake_label', 'no_proof', 'tiny_truth', 'wrong_comparison'];
export function validateCase(value) {
  if (!value || typeof value.product !== 'string' || !value.product.trim() || typeof value.adText !== 'string' || value.adText.length > 650 || typeof value.verdictText !== 'string' || typeof value.isSus !== 'boolean' || !Array.isArray(value.tells) || value.tells.length > 3) return false;
  if (value.isSus !== (value.tells.length > 0)) return false;
  return value.tells.every(t => t && typeof t.phrase === 'string' && t.phrase.length > 0 && value.adText.includes(t.phrase) && TYPES.includes(t.type)) && new Set(value.tells.map(t => t.phrase)).size === value.tells.length;
}
async function boundedJson(response, limit) {
  if (!response.ok) throw new Error('Provider request failed');
  const reader = response.body.getReader(); let total = 0; const chunks = [];
  try {
    while (true) { const {done, value} = await reader.read(); if (done) break; total += value.length; if (total > limit) throw new Error('Provider response too large'); chunks.push(value); }
  } finally { await reader.cancel(); }
  const bytes = new Uint8Array(total); let offset = 0; for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
  return JSON.parse(new TextDecoder().decode(bytes));
}
async function call(env, route, body, limit = 65536) {
  const base = env.ONEENDPOINT_BASE_URL || 'https://1endpoint.dev/api/v1';
  if (!base.startsWith('https://')) throw new Error('HTTPS provider required');
  const result = await fetch(base.replace(/\/$/, '') + route, {method:'POST', headers:{'Content-Type':'application/json', Authorization:'Bearer ' + env.ONEENDPOINT_API_KEY}, body:JSON.stringify(body), signal:AbortSignal.timeout(90000)});
  return boundedJson(result, limit);
}
async function image(env, prompt) {
  const result = await call(env, '/images/generations', {model:env.IMAGE_MODEL || 'gpt-image-2', prompt, n:1, size:'1024x1024', output_format:'png', response_format:'b64_json'}, 12000000);
  const data = result.data?.[0]?.b64_json;
  if (typeof data !== 'string' || data.length > 11000000 || !/^[A-Za-z0-9+/=\r\n]+$/.test(data)) throw new Error('Invalid image response');
  return 'data:image/png;base64,' + data;
}
const json = (value,status=200) => Response.json(value,{status,headers:{'Cache-Control':'no-store'}});
export async function onRequestPost(context) {
  const {request,env} = context;
  if (env.FACTORY) return env.FACTORY.fetch(request);
  if (!env.ONEENDPOINT_API_KEY || !env.TEXT_MODEL) return json({error:'Factory not configured',ads:[]},503);
  if (request.headers.get('Origin') && request.headers.get('Origin') !== new URL(request.url).origin) return json({error:'Origin rejected'},403);
  // A Cloudflare rate-limiter binding is required before enabling paid generation publicly.
  if (!env.CASE_RATE_LIMITER) return json({error:'Factory rate limiter not configured',ads:[]},503);
  const rate = await env.CASE_RATE_LIMITER.limit({key:request.headers.get('CF-Connecting-IP') || 'unknown'});
  if (!rate.success) return json({error:'Please try again later',ads:[]},429);
  let input;
  try { const raw=await request.text(); if(raw.length>512)return json({error:'Request too large'},413); input=JSON.parse(raw); } catch { return json({error:'Invalid request'},400); }
  const difficulty=Number(input.difficulty ?? 1), count=Number(input.count ?? 1);
  if (!Number.isInteger(difficulty) || difficulty<1 || difficulty>5 || !Number.isInteger(count) || count<1 || count>3) return json({error:'Invalid difficulty or count'},400);
  try {
    const result = await call(env,'/chat/completions',{model:env.TEXT_MODEL,temperature:0.8,response_format:{type:'json_object'},messages:[{role:'system',content:'Create fictional greenwashing detective cases. Return JSON {ads:[...]}. Each case has product, category (drinks/cosmetics/fashion/snacks/tech), adText (under 70 words), isSus boolean, tells [{phrase,type,explanation}], verdictText (under 30 words), difficulty. Only fictional brands. No real brands, politics or medical promises. SUS has 1-3 exact non-overlapping phrases in adText, with type vague/fake_label/no_proof/tiny_truth/wrong_comparison. LEGIT has empty tells and a narrowly scoped measured claim, comparison baseline and independent verification supplied as fictional evidence. Explain a claim is supported only within its stated scope. Include both LEGIT and SUS where batch size permits.'},{role:'user',content:'Generate '+count+' cases at difficulty '+difficulty+'.'}]});
    const raw = result.choices?.[0]?.message?.content;
    const cases = JSON.parse(raw).ads;
    if (!Array.isArray(cases) || cases.length !== count || !cases.every(validateCase)) throw new Error('Invalid cases');
    // Sequential cases bound peak image memory; each case gets two matching illustrations.
    const ads=[];
    for (const c of cases) {
      c.id=crypto.randomUUID(); c.difficulty=difficulty;
      const visual='Fictional product '+c.product+' in category '+c.category+'. The case copy is: '+c.adText+'. '; 
      [c.imageUrl,c.adImageUrl]=await Promise.all([
        image(env,visual+'Create a premium clean product photograph on a simple background. No written text, no environmental certification symbols, no real brand logos.'),
        image(env,visual+'Create a matching advertisement visual with the same product design and an editorial composition. Leave open space for game-rendered text. No written words, no real logos, no extra sustainability claims or badges.')
      ]);
      ads.push(c);
    }
    return json({ads});
  } catch { console.warn(JSON.stringify({event:'case_generation_failed'})); return json({error:'Factory unavailable; use local archive',ads:[]},502); }
}
