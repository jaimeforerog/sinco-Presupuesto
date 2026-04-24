namespace SincoPresupuesto.Api.Endpoints;

/// <summary>
/// HTML + JS inline del visor. Embebido en el ensamblado para no requerir
/// <c>wwwroot/</c> (simplifica el Dockerfile). Ver
/// <c>slices/_obs-visor-eventos/README.md</c> decisión #7.
///
/// Arquitectura de la UI: una sola página con navegación por hash
/// (<c>#/tenants/{id}/streams</c>, etc.). Sin dependencias externas.
/// </summary>
internal static class DiagIndexHtml
{
    public const string Content = """
<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="UTF-8">
  <title>Visor de Eventos — Sinco Presupuesto</title>
  <style>
    :root {
      --fg: #1a1a1a; --muted: #666; --bg: #fafafa; --card: #fff;
      --border: #e1e1e1; --accent: #0b6bcb; --code: #f4f4f4;
    }
    * { box-sizing: border-box; }
    body {
      font-family: -apple-system, Segoe UI, system-ui, sans-serif;
      margin: 0; padding: 0; color: var(--fg); background: var(--bg);
    }
    header {
      padding: 16px 24px; background: var(--card); border-bottom: 1px solid var(--border);
      display: flex; align-items: baseline; gap: 16px; flex-wrap: wrap;
    }
    h1 { font-size: 18px; margin: 0; }
    .breadcrumb { font-size: 13px; color: var(--muted); }
    .breadcrumb a { color: var(--accent); text-decoration: none; }
    .breadcrumb a:hover { text-decoration: underline; }
    main { padding: 24px; max-width: 1200px; margin: 0 auto; }
    section { background: var(--card); border: 1px solid var(--border); border-radius: 6px; padding: 16px; margin-bottom: 16px; }
    h2 { font-size: 15px; margin: 0 0 12px; color: var(--muted); text-transform: uppercase; letter-spacing: .5px; }
    table { width: 100%; border-collapse: collapse; font-size: 13px; }
    th, td { padding: 8px 10px; text-align: left; border-bottom: 1px solid var(--border); }
    th { background: var(--code); font-weight: 600; font-size: 12px; color: var(--muted); text-transform: uppercase; }
    tr:hover { background: var(--bg); cursor: pointer; }
    tr.clickable td { cursor: pointer; }
    a { color: var(--accent); }
    code { background: var(--code); padding: 2px 6px; border-radius: 3px; font-size: 12px; font-family: 'SF Mono', Consolas, monospace; }
    pre { background: var(--code); padding: 12px; border-radius: 4px; overflow-x: auto; font-size: 12px; margin: 0; }
    .empty { color: var(--muted); font-style: italic; padding: 24px 0; text-align: center; }
    .badge { display: inline-block; padding: 2px 8px; border-radius: 10px; background: var(--accent); color: white; font-size: 11px; font-weight: 600; }
    .row-links { display: flex; gap: 12px; flex-wrap: wrap; margin-top: 8px; }
  </style>
</head>
<body>
  <header>
    <h1>🔍 Visor de Eventos</h1>
    <nav class="breadcrumb" id="breadcrumb"></nav>
  </header>
  <main id="app"></main>

  <script>
    const app = document.getElementById('app');
    const breadcrumb = document.getElementById('breadcrumb');

    // ─── Router mínimo basado en hash ─────────────────────────────────
    const routes = [
      { pattern: /^$/, handler: renderTenants },
      { pattern: /^\/tenants\/([^/]+)$/, handler: renderTenantDashboard },
      { pattern: /^\/tenants\/([^/]+)\/streams$/, handler: renderStreams },
      { pattern: /^\/tenants\/([^/]+)\/streams\/([^/]+)\/events$/, handler: renderEvents },
      { pattern: /^\/tenants\/([^/]+)\/projections\/presupuestos$/, handler: renderPresupuestos },
      { pattern: /^\/tenants\/([^/]+)\/projections\/configuracion$/, handler: renderConfiguracion },
    ];

    function route() {
      const path = location.hash.replace(/^#/, '');
      for (const r of routes) {
        const m = path.match(r.pattern);
        if (m) {
          updateBreadcrumb(path, m);
          r.handler(...m.slice(1));
          return;
        }
      }
      location.hash = '';
    }

    function updateBreadcrumb(path, match) {
      const parts = [`<a href="#">🏠 Tenants</a>`];
      if (match[1]) parts.push(`<a href="#/tenants/${encodeURIComponent(match[1])}">📁 ${escape(match[1])}</a>`);
      if (path.includes('/streams')) parts.push(`<a href="#/tenants/${encodeURIComponent(match[1])}/streams">📚 streams</a>`);
      if (match[2]) parts.push(`<span>🧾 ${escape(match[2])}</span>`);
      if (path.includes('/projections/presupuestos')) parts.push(`<span>📊 presupuestos</span>`);
      if (path.includes('/projections/configuracion')) parts.push(`<span>⚙️ configuración</span>`);
      breadcrumb.innerHTML = parts.join(' › ');
    }

    // ─── Vistas ───────────────────────────────────────────────────────
    async function renderTenants() {
      app.innerHTML = '<section><h2>Cargando tenants…</h2></section>';
      const tenants = await fetchJson('/diag/tenants');
      if (!tenants.length) {
        app.innerHTML = '<section><h2>Tenants</h2><div class="empty">Aún no hay tenants con eventos.</div></section>';
        return;
      }
      app.innerHTML = `
        <section>
          <h2>Tenants (${tenants.length})</h2>
          <table>
            <thead><tr><th>tenantId</th></tr></thead>
            <tbody>
              ${tenants.map(t => `<tr class="clickable" onclick="location.hash='#/tenants/${encodeURIComponent(t)}'"><td><code>${escape(t)}</code></td></tr>`).join('')}
            </tbody>
          </table>
        </section>`;
    }

    function renderTenantDashboard(tenantId) {
      const t = encodeURIComponent(tenantId);
      app.innerHTML = `
        <section>
          <h2>Tenant ${escape(tenantId)}</h2>
          <div class="row-links">
            <a href="#/tenants/${t}/streams">📚 Streams (event store)</a>
            <a href="#/tenants/${t}/projections/presupuestos">📊 Presupuestos (proyección)</a>
            <a href="#/tenants/${t}/projections/configuracion">⚙️ Configuración (proyección)</a>
          </div>
        </section>`;
    }

    async function renderStreams(tenantId) {
      const t = encodeURIComponent(tenantId);
      app.innerHTML = `<section><h2>Streams de ${escape(tenantId)}</h2><div>Cargando…</div></section>`;
      const streams = await fetchJson(`/diag/tenants/${t}/streams`);
      if (!streams.length) {
        app.innerHTML = `<section><h2>Streams de ${escape(tenantId)}</h2><div class="empty">Sin streams.</div></section>`;
        return;
      }
      app.innerHTML = `
        <section>
          <h2>Streams de ${escape(tenantId)} (${streams.length})</h2>
          <table>
            <thead><tr><th>streamId</th><th>tipo</th><th>versión</th><th>creado</th><th>actualizado</th></tr></thead>
            <tbody>
              ${streams.map(s => `
                <tr class="clickable" onclick="location.hash='#/tenants/${t}/streams/${s.streamId}/events'">
                  <td><code>${s.streamId}</code></td>
                  <td>${escape(s.aggregateType || '—')}</td>
                  <td><span class="badge">v${s.version}</span></td>
                  <td>${formatDate(s.createdAt)}</td>
                  <td>${formatDate(s.updatedAt)}</td>
                </tr>`).join('')}
            </tbody>
          </table>
        </section>`;
    }

    async function renderEvents(tenantId, streamId) {
      const t = encodeURIComponent(tenantId);
      app.innerHTML = `<section><h2>Eventos de ${escape(streamId)}</h2><div>Cargando…</div></section>`;
      const events = await fetchJson(`/diag/tenants/${t}/streams/${streamId}/events`);
      if (!events.length) {
        app.innerHTML = `<section><h2>Eventos</h2><div class="empty">Stream sin eventos.</div></section>`;
        return;
      }
      app.innerHTML = `
        <section>
          <h2>Eventos de ${escape(streamId)} (${events.length})</h2>
          ${events.map(e => `
            <div style="margin-bottom: 16px; padding-bottom: 16px; border-bottom: 1px solid var(--border);">
              <div style="display: flex; gap: 12px; align-items: baseline; margin-bottom: 6px;">
                <span class="badge">v${e.version}</span>
                <strong>${escape(e.eventType)}</strong>
                <span style="color: var(--muted); font-size: 12px;">${formatDate(e.timestamp)}</span>
                <span style="color: var(--muted); font-size: 11px;">seq ${e.sequence}</span>
              </div>
              <pre>${escape(JSON.stringify(e.data, null, 2))}</pre>
            </div>`).join('')}
        </section>`;
    }

    async function renderPresupuestos(tenantId) {
      const t = encodeURIComponent(tenantId);
      app.innerHTML = `<section><h2>Presupuestos de ${escape(tenantId)}</h2><div>Cargando…</div></section>`;
      const presupuestos = await fetchJson(`/diag/tenants/${t}/projections/presupuestos`);
      if (!presupuestos.length) {
        app.innerHTML = `<section><h2>Presupuestos</h2><div class="empty">Sin presupuestos.</div></section>`;
        return;
      }
      app.innerHTML = `
        <section>
          <h2>Presupuestos de ${escape(tenantId)} (${presupuestos.length})</h2>
          ${presupuestos.map(p => `
            <div style="margin-bottom: 16px; padding-bottom: 16px; border-bottom: 1px solid var(--border);">
              <div style="margin-bottom: 6px;">
                <strong>${escape(p.codigo)}</strong> — ${escape(p.nombre)}
                <span class="badge" style="background: ${p.estado === 0 ? '#6c757d' : '#28a745'};">${estadoNombre(p.estado)}</span>
              </div>
              <pre>${escape(JSON.stringify(p, null, 2))}</pre>
            </div>`).join('')}
        </section>`;
    }

    async function renderConfiguracion(tenantId) {
      const t = encodeURIComponent(tenantId);
      app.innerHTML = `<section><h2>Configuración de ${escape(tenantId)}</h2><div>Cargando…</div></section>`;
      const resp = await fetch(`/diag/tenants/${t}/projections/configuracion`);
      if (resp.status === 404) {
        app.innerHTML = `<section><h2>Configuración</h2><div class="empty">Tenant sin configuración registrada.</div></section>`;
        return;
      }
      const doc = await resp.json();
      app.innerHTML = `
        <section>
          <h2>Configuración de ${escape(tenantId)}</h2>
          <pre>${escape(JSON.stringify(doc, null, 2))}</pre>
        </section>`;
    }

    // ─── Utilidades ──────────────────────────────────────────────────
    async function fetchJson(url) {
      const r = await fetch(url);
      if (!r.ok) throw new Error(`${r.status} ${r.statusText}`);
      return r.json();
    }

    function escape(s) {
      if (s === null || s === undefined) return '';
      return String(s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function formatDate(iso) {
      if (!iso) return '—';
      return new Date(iso).toLocaleString();
    }

    function estadoNombre(n) {
      return ['Borrador', 'Aprobado', 'Activo', 'Cerrado'][n] || '?';
    }

    window.addEventListener('hashchange', route);
    route();
  </script>
</body>
</html>
""";
}
