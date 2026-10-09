import test from 'node:test';
import assert from 'node:assert/strict';
import {validateCase,onRequestPost,parseCaseJson,campaignPrompt,productPrompt} from '../functions/api/cases.js';
const valid={product:'MossGlow',category:'cosmetics',adText:'Planet-friendly shampoo.',isSus:true,tells:[{phrase:'Planet-friendly',type:'vague'}],verdictText:'No measured evidence.'};
test('provider fenced JSON is accepted without accepting arbitrary prose',()=>{
  assert.deepEqual(parseCaseJson('\x60\x60\x60json\n{"ads":[]}\n\x60\x60\x60'),{ads:[]});
  assert.throws(()=>parseCaseJson('Here is the result: {"ads":[]}'));
});
test('campaign prompt requests a landscape advert with exact copy and no invented claims',()=>{
  const prompt=campaignPrompt(valid);
  assert.match(prompt,/LANDSCAPE 3:2/);assert.ok(prompt.includes(JSON.stringify(valid.adText)));
  assert.match(prompt,/actual print advertisement/);assert.match(prompt,/No other visible text anywhere/);
  assert.match(productPrompt(valid),/premium clean product photograph/);
});
test('invalid evidence and verdicts are rejected',()=>{
  assert.equal(validateCase(valid),true);
  assert.equal(validateCase({...valid,isSus:false}),false);
  assert.equal(validateCase({...valid,tells:[{phrase:'absent',type:'vague'}]}),false);
  assert.equal(validateCase({...valid,tells:[{phrase:'Planet-friendly',type:'unknown'}]}),false);
  assert.equal(validateCase({...valid,isSus:false,tells:[]}),true);
});
test('empty content, substring evidence and overlapping tells are rejected',()=>{
  assert.equal(validateCase({...valid,adText:'',isSus:false,tells:[]}),false);
  assert.equal(validateCase({...valid,verdictText:''}),false);
  assert.equal(validateCase({...valid,category:'unknown'}),false);
  assert.equal(validateCase({...valid,tells:[{phrase:'friendly',type:'vague'}]}),false);
  assert.equal(validateCase({...valid,tells:[{phrase:'Planet-friendly',type:'vague'},{phrase:'Planet-friendly shampoo',type:'no_proof'}]}),false);
});
test('null/array/oversized request bodies return errors rather than throwing',async()=>{
  const env={ONEENDPOINT_API_KEY:'test',TEXT_MODEL:'test',CASE_RATE_LIMITER:{limit:async()=>({success:true})}};
  for (const body of ['null','[]','"text"','{'+ ' '.repeat(600)+'}']) {
    const res=await onRequestPost({request:new Request('https://game.test/api/cases',{method:'POST',body}),env});
    assert.equal(res.status,400);
  }
});
test('one text case and two image responses become a playable case',async()=>{
  const original=globalThis.fetch;const routes=[];
  globalThis.fetch=async(url,options)=>{
    routes.push(new URL(url).pathname);
    const body=JSON.parse(options.body);
    if(url.endsWith('/chat/completions'))return Response.json({choices:[{message:{content:JSON.stringify({ads:[valid]})}}]});
    assert.equal(body.n,1);assert.equal(body.response_format,'b64_json');
    assert.ok(['1024x1024','1536x1024'].includes(body.size));
    return Response.json({data:[{b64_json:'iVBORw0KGgo='}]});
  };
  try {
    const res=await onRequestPost({request:new Request('https://game.test/api/cases',{method:'POST',body:'{"difficulty":2,"count":1}'}),env:{ONEENDPOINT_API_KEY:'test',TEXT_MODEL:'test',CASE_RATE_LIMITER:{limit:async()=>({success:true})}}});
    assert.equal(res.status,200);const {ads}=await res.json();
    assert.equal(ads.length,1);assert.equal(ads[0].difficulty,2);assert.ok(ads[0].id);
    assert.ok(ads[0].imageUrl.startsWith('data:image/png;base64,'));assert.ok(ads[0].adImageUrl.startsWith('data:image/png;base64,'));assert.equal(routes.length,3);
  } finally {globalThis.fetch=original;}
});
test('unconfigured public factory fails closed',async()=>{
  const response=await onRequestPost({request:new Request('https://game.test/api/cases',{method:'POST',body:'{}'}),env:{}});
  assert.equal(response.status,503);
});
test('cross-origin paid generation rejected',async()=>{
  const response=await onRequestPost({request:new Request('https://game.test/api/cases',{method:'POST',headers:{Origin:'https://elsewhere.test'},body:'{}'}),env:{ONEENDPOINT_API_KEY:'test',TEXT_MODEL:'test'}});
  assert.equal(response.status,403);
});
test('Pages delegates through its private service binding',async()=>{
  let called=false;
  const response=await onRequestPost({request:new Request('https://game.test/api/cases',{method:'POST'}),env:{FACTORY:{fetch:async()=>{called=true;return Response.json({ads:[]});}}}});
  assert.equal(called,true);assert.equal(response.status,200);
});
test('rate limit blocks provider calls',async()=>{
  const response=await onRequestPost({request:new Request('https://game.test/api/cases',{method:'POST',body:'{}'}),env:{ONEENDPOINT_API_KEY:'test',TEXT_MODEL:'test',CASE_RATE_LIMITER:{limit:async()=>({success:false})}}});
  assert.equal(response.status,429);
});
