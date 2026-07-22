export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    const corsHeaders = {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type',
    };

    if (request.method === 'OPTIONS') {
      return new Response(null, { status: 204, headers: corsHeaders });
    }

    // GET /list?key=SECRET — admin: list all signups
    if (request.method === 'GET' && url.pathname === '/list') {
      const key = url.searchParams.get('key');
      const adminKey = env.ADMIN_KEY || 'chroma-vale-admin-2026';

      if (key !== adminKey) {
        return new Response(JSON.stringify({ error: 'Unauthorized' }), {
          status: 401,
          headers: { ...corsHeaders, 'Content-Type': 'application/json' },
        });
      }

      try {
        const list = await env.EMAILS.list();
        const results = [];
        for (const k of list.keys) {
          const raw = await env.EMAILS.get(k.name);
          if (raw) results.push(JSON.parse(raw));
        }
        results.sort((a, b) => new Date(b.signed_up) - new Date(a.signed_up));
        return new Response(JSON.stringify({ count: results.length, emails: results }), {
          status: 200,
          headers: { ...corsHeaders, 'Content-Type': 'application/json' },
        });
      } catch (e) {
        return new Response(JSON.stringify({ error: 'Failed to list' }), {
          status: 500,
          headers: { ...corsHeaders, 'Content-Type': 'application/json' },
        });
      }
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

        const emailKey = email.toLowerCase().trim();
        const existing = await env.EMAILS.get(emailKey);

        if (existing) {
          return new Response(JSON.stringify({ status: 'already_subscribed' }), {
            status: 200,
            headers: { ...corsHeaders, 'Content-Type': 'application/json' },
          });
        }

        await env.EMAILS.put(emailKey, JSON.stringify({
          email: emailKey,
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
