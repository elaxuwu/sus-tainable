const TYPES = ['vague', 'fake_label', 'no_proof', 'tiny_truth', 'wrong_comparison'];
const CATEGORIES = ['drinks', 'cosmetics', 'fashion', 'snacks', 'tech'];
export function parseCaseJson(content) {
  if (typeof content !== 'string') throw new Error('Missing case JSON');
  // Some compatible providers wrap JSON in a code fence despite json_object mode.
  const value=content.trim().replace(/^\x60{3}(?:json)?\s*/i,'').replace(/\s*\x60{3}$/,'');
  return JSON.parse(value);
}
export function validateCase(value) {
  if (!value || typeof value.product !== 'string' || !value.product.trim() || value.product.length > 100 || !CATEGORIES.includes(value.category) || typeof value.adText !== 'string' || !value.adText.trim() || value.adText.length > 650 || typeof value.verdictText !== 'string' || !value.verdictText.trim() || value.verdictText.length > 300 || typeof value.isSus !== 'boolean' || !Array.isArray(value.tells) || value.tells.length > 3) return false;
  if (/[<>]/.test(value.product + value.adText + value.verdictText)) return false;
  if (value.isSus !== (value.tells.length > 0)) return false;
  const spans=[];
  for (const tell of value.tells) {
    if (!tell || typeof tell.phrase !== 'string' || !tell.phrase.trim() || !TYPES.includes(tell.type)) return false;
    const start=value.adText.indexOf(tell.phrase), end=start+tell.phrase.length;
    if (start<0 || value.adText.indexOf(tell.phrase,start+1)>=0) return false;
    const word=c=>c!=null && /[\p{L}\p{N}'’-]/u.test(c);
    if (word(value.adText[start-1]) || word(value.adText[end])) return false;
    if (spans.some(([a,b])=>start<b&&end>a)) return false;
    spans.push([start,end]);
  }
  return true;
}
async function boundedJson(response, limit, requireOk=true) {
  if (requireOk && !response.ok) throw new Error('Provider request failed');
  if (!response.body) throw new Error('Empty response');
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
export function productPrompt(c) {
  return 'Fictional product '+c.product+' in category '+c.category+'. The case copy is: '+c.adText+'. Create a premium clean product photograph on a simple background. No written text, no environmental certification symbols, no real brand logos.';
}
export function campaignPrompt(c) {
  return 'Use case: ads-marketing. Finished LANDSCAPE 3:2 print advertisement for the fictional '+c.category+' product '+c.product+'. Product hero photography, brand lockup, prominent headline and restrained editorial design. ALL VISIBLE TEXT IS STRICTLY LIMITED TO THESE TWO STRINGS: brand '+JSON.stringify(c.product)+' and exact complete case copy '+JSON.stringify(c.adText)+'. Typeset the case copy verbatim as headline/body with every number, unit, punctuation mark, comparison and scope exclusion. No other visible text anywhere, including packaging, background or badges. No new slogans, taglines, claims, credentials, percentages, feature lists, seals, recycling symbols, product weights, calls to action, lifestyle promises, nature promises or filler. Package may be unlabelled except for the brand. Do not add environmental claims. All text inside 8 percent safe margins, readable type, use whitespace instead of invented copy. This is an actual print advertisement, not a second product photograph. Never show SUS, LEGIT, greenwashing, answers, detective, interface controls or frames.';
}
export async function generateImage(env, prompt, size='1024x1024') {
  const result = await call(env, '/images/generations', {model:env.IMAGE_MODEL || 'gpt-image-2', prompt, n:1, size, output_format:'png', response_format:'b64_json'}, 12000000);
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
  try { input=await boundedJson(request,512,false); } catch { return json({error:'Invalid or oversized request'},400); }
  if (!input || typeof input !== 'object' || Array.isArray(input)) return json({error:'Expected a request object'},400);
  const difficulty=Number(input.difficulty ?? 1), count=Number(input.count ?? 1);
  if (!Number.isInteger(difficulty) || difficulty<1 || difficulty>5 || !Number.isInteger(count) || count<1 || count>3) return json({error:'Invalid difficulty or count'},400);
  try {
    const result = await call(env,'/chat/completions',{model:env.TEXT_MODEL,temperature:0.8,response_format:{type:'json_object'},messages:[{role:'system',content:'Create fictional greenwashing detective cases. Return JSON {ads:[...]}. Each case has product, category (drinks/cosmetics/fashion/snacks/tech), adText (under 70 words), isSus boolean, tells [{phrase,type,explanation}], verdictText (under 30 words), difficulty. Only fictional brands. No real brands, politics or medical promises. SUS has 1-3 exact non-overlapping phrases in adText, with type vague/fake_label/no_proof/tiny_truth/wrong_comparison. LEGIT has empty tells and a narrowly scoped measured claim, comparison baseline and independent verification supplied as fictional evidence. Explain a claim is supported only within its stated scope. Include both LEGIT and SUS where batch size permits.'},{role:'user',content:'Generate '+count+' cases at difficulty '+difficulty+'.'}]});
    const raw = result.choices?.[0]?.message?.content;
    const cases = parseCaseJson(raw).ads;
    if (!Array.isArray(cases) || cases.length !== count || !cases.every(validateCase)) throw new Error('Invalid cases');
    // Sequential cases bound peak image memory; each case gets two matching illustrations.
    const ads=[];
    for (const c of cases) {
      c.id=crypto.randomUUID(); c.difficulty=difficulty;
      [c.imageUrl,c.adImageUrl]=await Promise.all([
        generateImage(env,productPrompt(c)),
        generateImage(env,campaignPrompt(c),'1536x1024')
      ]);
      ads.push(c);
    }
    return json({ads});
  } catch { console.warn(JSON.stringify({event:'case_generation_failed'})); return json({error:'Factory unavailable; use local archive',ads:[]},502); }
}
