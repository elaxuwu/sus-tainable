import {onRequestPost} from '../functions/api/cases.js';
export default {
  async fetch(request,env) {
    if (request.method !== 'POST' || new URL(request.url).pathname !== '/api/cases') return Response.json({error:'Not found'},{status:404});
    return onRequestPost({request,env});
  }
};
