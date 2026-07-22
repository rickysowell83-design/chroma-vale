export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    // CORS headers — allow requests from chromavale.com and local dev
    const corsHeaders = {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type',
    };

    if (request.method === 'OPTIONS') {
      return new Response(null, { status: 204, headers: corsHeaders });
    }

    // POST /signup — store an email
    if (request.method === 'POST' && url.pathname === '/signup') {
      try {
        const { email } = await request.json();

        if (!email || !email.includes('@')) {
          return new Response(JSON.stringify({ error: 'Invalid email' }), {
            status: 400,
            headers: { ...corsHeaders, 'Content-Type': 'application/json' },
          });
        }

        // Normalize and store
        const key = email.toLowerCase().trim();
        const existing = await env.EMAILS.get(key);

        if (existing) {
          return new Response(JSON.stringify({ status: 'already_subscribed' }), {
            status: 200,
            headers: { ...corsHeaders, 'Content-Type': 'application/json' },
          });
        }

        await env.EMAILS.put(key, JSON.stringify({
          email: key,
          signed_up: new Date().toISOString(),
          source: 'chromavale.com',
        }));

        return new Response(JSON.stringify({ status: 'ok' }), {
          status: 201,
          headers: { ...corsHeaders, 'Content-Type': 'application/json' },
        });
      } catch (e) {
        return new Response(JSON.stringify({ error: 'Bad request' }), {
          status: 400,
          headers: { ...corsHeaders, 'Content-Type': 'application/json' },
        });
      }
    }

    // Everything else → 404
    return new Response('Not found', { status: 404, headers: corsHeaders });
  },
};
