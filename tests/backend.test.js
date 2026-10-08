import test from 'node:test';
import assert from 'node:assert/strict';
import {validateCase,onRequestPost} from '../functions/api/cases.js';
const valid={product:'MossGlow',adText:'Planet-friendly shampoo.',isSus:true,tells:[{phrase:'Planet-friendly',type:'vague'}],verdictText:'No measured evidence.'};
test('invalid evidence and verdicts are rejected',()=>{
  assert.equal(validateCase(valid),true);
  assert.equal(validateCase({...valid,isSus:false}),false);
  assert.equal(validateCase({...valid,tells:[{phrase:'absent',type:'vague'}]}),false);
  assert.equal(validateCase({...valid,tells:[{phrase:'Planet-friendly',type:'unknown'}]}),false);
  assert.equal(validateCase({...valid,isSus:false,tells:[]}),true);
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
