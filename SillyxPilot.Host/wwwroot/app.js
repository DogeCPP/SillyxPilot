const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const linkify = (s) => esc(s).replace(/(https?:\/\/[^\s]+)/g, '<a href="$1" target="_blank" rel="noopener">$1</a>');

const state = { messages: [], alerts: [], controllers: [], config: null, unseenAlerts: 0, filters: new Set(['private', 'radio', 'broadcast', 'server']), map: null };
const GROUP_ORDER = ['Center', 'Approach/Departure', 'Tower', 'Ground', 'Ramp', 'Clearance Delivery', 'ATIS', 'Observers'];

function activateTab(name) {
  $$('.tab').forEach((t) => t.classList.toggle('active', t.dataset.tab === name));
  $$('.panel').forEach((p) => p.classList.toggle('active', p.dataset.panel === name));
  if (name === 'alerts') { state.unseenAlerts = 0; updateAlertBadge(); }
  if (name === 'settings') renderSettings();
  if (name === 'map') initMap();
}
$$('[data-tab]').forEach((el) => el.addEventListener('click', () => activateTab(el.dataset.tab)));

function messageLineHtml(m) {
  const ts = (m.tstamp || '').split('.')[0] || new Date(m.at || Date.now()).toLocaleTimeString();
  let cls = m.kind || 'info', badge = '';
  if (m.kind === 'private') {
    if (m.senderClass === 'supervisor') { cls = 'supervisor'; badge = '<span class="badge-sup">SUP</span>'; }
    else if (m.senderClass === 'atc') { cls = 'atc'; badge = '<span class="badge-atc">ATC</span>'; }
    else cls = 'private';
  }
  const freq = m.freq ? ` <span class="fq">on ${esc(m.freq)}</span>` : '';
  const who = m.from ? `<span class="who">${esc(m.from)}</span>${badge}${freq}: ` : '';
  return `<div class="line ${cls}"><span class="ts">[${esc(ts)}]</span> ${who}${linkify(m.message)}</div>`;
}
function passesFilter(m) {
  const k = m.kind === 'private' ? 'private' : m.kind;
  if (['private', 'radio', 'broadcast', 'server'].includes(k)) return state.filters.has(k);
  return true;
}
function renderLog() {
  const q = $('#search').value.trim().toLowerCase();
  const log = $('#log');
  const atBottom = log.scrollHeight - log.scrollTop - log.clientHeight < 40;
  const rows = state.messages.filter(passesFilter).filter((m) => !q || (m.from || '').toLowerCase().includes(q) || (m.message || '').toLowerCase().includes(q) || (m.kind || '').includes(q));
  log.innerHTML = rows.map(messageLineHtml).join('');
  if (atBottom) log.scrollTop = log.scrollHeight;
}
function addMessage(m) { state.messages.push(m); if (state.messages.length > 1000) state.messages.shift(); renderLog(); }
$('#search').addEventListener('input', renderLog);
$$('.kfilter').forEach((c) => c.addEventListener('change', () => { state.filters = new Set($$('.kfilter').filter((x) => x.checked).map((x) => x.value)); renderLog(); }));

function renderControllers() {
  const groups = {};
  for (const g of GROUP_ORDER) groups[g] = [];
  for (const c of state.controllers) (groups[c.group] || (groups[c.group] = [])).push(c);
  $('#controllers').innerHTML = GROUP_ORDER.map((g) => {
    const items = groups[g] || [];
    const rows = items.map((c) => {
      const isSup = c.rating === 'SUP' || c.rating === 'ADM';
      return `<div class="ctrl-item ${isSup ? 'sup' : ''}"><span class="cs">${esc(c.callsign)}${isSup ? ' •SUP' : ''}</span><span class="fq">${esc(c.freq || '')}</span></div>`;
    }).join('');
    return `<div class="ctrl-group ${items.length ? '' : 'empty'}"><div class="g-title">${g}${items.length ? ` (${items.length})` : ''}</div>${rows}</div>`;
  }).join('');
}

function renderAwareness(a) {
  if (!a) return;
  const n = a.supervisorCount ?? 0;
  $('#sup-count').textContent = n;
  const badge = $('#sup-badge');
  badge.classList.remove('warn', 'danger');
  if (n >= 3) badge.classList.add('danger'); else if (n >= 1) badge.classList.add('warn');
  if (a.error) { $('#awareness-meta').innerHTML = `<span style="color:var(--danger)">VATSIM feed error: ${esc(a.error)}</span>`; return; }
  const names = (a.supervisors || []).map((s) => s.callsign).join(', ');
  $('#awareness-meta').textContent = `${a.atcCount ?? 0} ATC · ${a.atisCount ?? 0} ATIS · ${a.pilotCount ?? 0} pilots online` + (names ? ` · supervisors: ${names}` : '');
}

function alertHtml(al) {
  const cat = al.category || 'private';
  const title = cat === 'supervisor' ? `Supervisor: ${al.from}` : cat === 'atc' ? `ATC: ${al.from}` : `${al.from}`;
  const time = new Date(al.at || Date.now()).toLocaleString();
  const chans = (al.channels || []).map((c) => `<span class="chip ok">${esc(c)}</span>`).join('') || '<span class="chip">none</span>';
  const errs = (al.errors || []).length ? `<div class="a-channels" style="color:var(--danger)">${esc(al.errors.join('; '))}</div>` : '';
  return `<div class="alert-item ${cat}"><div class="a-head"><span class="a-title">${esc(title)}</span><span class="a-time">${esc(time)}</span></div><div class="a-msg">${linkify(al.message || '')}</div><div class="a-channels">sent via ${chans}</div>${errs}</div>`;
}
function renderAlerts() { $('#alerts').innerHTML = state.alerts.slice().reverse().map(alertHtml).join('') || '<div class="empty-note">No alerts yet. When a supervisor or ATC messages you, it shows up here so you can respond before things escalate.</div>'; }
function updateAlertBadge() { const b = $('#alert-badge'); if (state.unseenAlerts > 0) { b.style.display = ''; b.textContent = state.unseenAlerts; } else b.style.display = 'none'; }
function onAlert(al) {
  state.alerts.push(al); renderAlerts();
  if (!$('.tab[data-tab=alerts]').classList.contains('active')) { state.unseenAlerts++; updateAlertBadge(); }
  if (al.sound) playAlertSound(al.category);
  if (al.desktop) notify(al);
}

function playAlertSound(category) {
  const el = $('#alert-sound');
  el.src = category === 'supervisor' ? '/sounds/NewMessage.wav' : '/sounds/PrivateMessage.wav';
  el.currentTime = 0; el.play().catch(() => {});
  if (category === 'supervisor') setTimeout(() => { el.currentTime = 0; el.play().catch(() => {}); }, 700);
}
function notify(al) {
  if (!('Notification' in window) || Notification.permission !== 'granted') return;
  const title = al.category === 'supervisor' ? 'A supervisor is contacting you' : al.category === 'atc' ? 'ATC message' : 'Private message';
  new Notification(title, { body: `${al.from}: ${al.message || ''}`, requireInteraction: al.category === 'supervisor' });
}
$('#notif-btn').addEventListener('click', async () => {
  if (!('Notification' in window)) return;
  const p = await Notification.requestPermission();
  $('#notif-btn').textContent = p === 'granted' ? 'Notifications on' : 'Notifications blocked';
});
if ('Notification' in window && Notification.permission === 'granted') $('#notif-btn').textContent = 'Notifications on';

function toast(al) {
  const t = document.createElement('div');
  t.className = 'toast' + (al.category === 'atc' ? ' atc' : '');
  const title = al.category === 'supervisor' ? 'A supervisor is contacting you' : al.category === 'atc' ? 'ATC message' : 'Private message';
  t.innerHTML = `<div class="t-title">${title}</div><div>${esc(al.from)}: ${esc(al.message || '')}</div>`;
  $('#toasts').appendChild(t);
  setTimeout(() => t.remove(), al.category === 'supervisor' ? 12000 : 6000);
}

const mapState = { map: null, layer: null, selfMarker: null, initialized: false };
function initMap() {
  if (mapState.initialized) { setTimeout(() => mapState.map.invalidateSize(), 50); return; }
  mapState.initialized = true;
  mapState.map = L.map('map', { worldCopyJump: true, preferCanvas: false }).setView([20, 20], 3);
  L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
    maxZoom: 12, subdomains: 'abcd',
    attribution: '© OpenStreetMap contributors, © CARTO',
  }).addTo(mapState.map);
  mapState.layer = L.layerGroup().addTo(mapState.map);
  if (state.lastMap) renderMap(state.lastMap);
}
function planeIcon(heading, self, label) {
  const size = self ? 28 : 16;
  const cls = self ? 'ac-plane self' : 'ac-plane';
  const labelHtml = label ? `<div class="plane-label">${esc(label)}</div>` : '';
  const html = `<div class="${cls}" style="width:${size}px;height:${size}px;transform:rotate(${heading || 0}deg)"></div>${labelHtml}`;
  return L.divIcon({ className: 'aircraft-icon', html, iconSize: [size, size], iconAnchor: [size / 2, size / 2] });
}
function renderMap(map) {
  state.lastMap = map;
  if (!mapState.initialized) return;
  mapState.layer.clearLayers();
  const self = map.self;
  for (const p of map.traffic || []) {
    if (p.lat == null) continue;
    L.marker([p.lat, p.lon], { icon: planeIcon(p.heading, false), interactive: true })
      .bindTooltip(`${p.callsign}${p.aircraft ? ' · ' + p.aircraft : ''}${p.dep && p.arr ? `<br>${p.dep} → ${p.arr}` : ''}<br>${Math.round(p.altitude)} ft · ${Math.round(p.groundspeed)} kt`)
      .addTo(mapState.layer);
  }
  if (self && self.lat != null) {
    L.marker([self.lat, self.lon], { icon: planeIcon(self.heading, true, self.callsign), zIndexOffset: 1000 }).addTo(mapState.layer);
    if (!mapState.centered) { mapState.map.setView([self.lat, self.lon], 6); mapState.centered = true; }
  }
  renderMapSide(map);
}
function renderMapSide(map) {
  const self = map.self;
  if (self && self.lat != null) {
    $('#self-body').innerHTML = `
      <div class="row"><span class="k">Callsign</span><span class="v">${esc(self.callsign)}</span></div>
      <div class="row"><span class="k">Route</span><span class="v">${esc(self.dep || '?')} → ${esc(self.arr || '?')}</span></div>
      <div class="row"><span class="k">Altitude</span><span class="v">${Math.round(self.altitude)} ft</span></div>
      <div class="row"><span class="k">Ground speed</span><span class="v">${Math.round(self.groundspeed)} kt</span></div>
      <div class="row"><span class="k">Heading</span><span class="v">${Math.round(self.heading)}°</span></div>`;
  } else {
    $('#self-body').textContent = 'Not flying right now. Start a flight in xPilot and your aircraft will show up here.';
  }
  const pred = map.prediction;
  if (pred) {
    const w = pred.watching || [];
    $('#watching-body').innerHTML = w.length
      ? w.map((c) => `<div class="row"><span class="v">${esc(c.callsign)}</span><span class="k">${esc(c.position)} · ${c.distanceNm} nm</span></div>`).join('')
      : (pred.currentFir && pred.currentFir.online ? `<div class="row"><span class="v">${esc(pred.currentFir.controller)}</span><span class="k">${esc(pred.currentFir.name)} Center</span></div>` : '<span class="k">No controllers near you right now.</span>');
    const up = pred.upcoming || [];
    let html = '';
    if (pred.currentFir) html += `<div class="pred"><div class="pred-h"><span class="name">${esc(pred.currentFir.name)}</span><span class="eta">now</span></div><div class="status ${pred.currentFir.online ? 'on' : 'off'}">${pred.currentFir.online ? 'staffed by ' + esc(pred.currentFir.controller) : 'not staffed'}</div></div>`;
    html += up.map((u) => `<div class="pred"><div class="pred-h"><span class="name">${esc(u.name)}</span><span class="eta">${u.etaMin != null ? 'in about ' + u.etaMin + ' min' : u.distanceNm + ' nm'}</span></div><div class="status ${u.online ? 'on' : 'off'}">${u.online ? 'staffed by ' + esc(u.controller) : 'not staffed yet'}</div></div>`).join('');
    $('#predict-body').innerHTML = html || '<span class="k">No airspace prediction available.</span>';
  } else {
    $('#watching-body').innerHTML = '<span class="k">Waiting for your position.</span>';
    $('#predict-body').innerHTML = '<span class="k">Prediction appears once you\'re airborne with a filed route.</span>';
  }
}

function renderNetworkStatus(net) {
  const dot = $('#net-dot'), status = $('#net-status');
  if (!net || !net.enabled) { dot.className = 'dot off'; status.textContent = 'Network offline'; return; }
  if (net.canReceive) { dot.className = 'dot ' + (net.connected ? 'on' : 'off'); status.textContent = net.connected ? `Network · ${net.activeCount ?? 0} active` : `Network connecting…`; }
  else { dot.className = 'dot on'; status.textContent = 'Network (send only)'; }
  $('#net-count').textContent = net.activeCount ?? 0;
  $('#net-users').textContent = (net.activeUsers && net.activeUsers.length) ? '· ' + net.activeUsers.join(', ') : '';
}
function showNetworkRoom(handle) {
  $('#net-gate').style.display = 'none';
  $('#net-room').style.display = 'flex';
  $('#net-handle').textContent = handle;
}
function addNetworkChat(c) {
  const log = $('#net-log');
  const atBottom = log.scrollHeight - log.scrollTop - log.clientHeight < 40;
  const div = document.createElement('div');
  div.className = 'net-msg' + (c.self ? ' self' : '');
  const time = new Date(c.at || Date.now()).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  div.innerHTML = `<span class="ts">${time}</span> <span class="from">${esc(c.from)}</span>: ${linkify(c.text)}`;
  log.appendChild(div);
  if (atBottom) log.scrollTop = log.scrollHeight;
}
async function saveUsername() {
  const name = $('#username-input').value.trim();
  const r = await fetch('/api/network/username', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username: name }) });
  const j = await r.json();
  if (!j.ok) { $('#username-error').textContent = j.reason; return; }
  if (state.config) state.config.network.handle = j.username;
  showNetworkRoom(j.username);
}
$('#username-save').addEventListener('click', saveUsername);
$('#username-input').addEventListener('keydown', (e) => { if (e.key === 'Enter') saveUsername(); });
$('#change-username').addEventListener('click', () => { $('#net-room').style.display = 'none'; $('#net-gate').style.display = 'flex'; $('#username-input').focus(); });
function sendNetwork() {
  const inp = $('#net-text'); const text = inp.value.trim();
  if (!text) return;
  fetch('/api/network/send', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text }) }).catch(() => {});
  inp.value = '';
}
$('#net-send').addEventListener('click', sendNetwork);
$('#net-text').addEventListener('keydown', (e) => { if (e.key === 'Enter') sendNetwork(); });

async function renderSettings() {
  if (!state.config) { state.config = await (await fetch('/api/config')).json(); }
  const c = state.config;
  const rule = (cat) => ['discord', 'email', 'desktop', 'sound'].map((ch) => `<input type="checkbox" data-cfg="alerts.${cat}.${ch}" ${c.alerts?.[cat]?.[ch] ? 'checked' : ''} style="justify-self:center" />`).join('');
  $('#settings').innerHTML = `
    <h3>Alerts</h3>
    <p class="section-hint">Choose how you want to be told when someone messages you. Supervisor messages are the ones you really don't want to miss.</p>
    <div class="rule-grid">
      <div></div><div class="h">Discord</div><div class="h">Email</div><div class="h">Desktop</div><div class="h">Sound</div>
      <div class="r-name">Supervisor message</div>${rule('supervisorPanic')}
      <div class="r-name">ATC private message</div>${rule('atcPrivateMessage')}
      <div class="r-name">Any private message</div>${rule('anyPrivateMessage')}
    </div>
    <div class="field"><label>Don't repeat within (sec)</label><input type="number" data-cfg="alerts.dedupeSeconds" value="${esc(c.alerts?.dedupeSeconds ?? 120)}" /></div>

    <h3>Discord alerts</h3>
    <p class="section-hint">Paste a Discord webhook URL to get a ping in your channel (great for phone push).</p>
    <div class="field"><label>Webhook URL</label><input type="text" data-cfg="discord.webhookUrl" value="${esc(c.discord?.webhookUrl || '')}" placeholder="https://discord.com/api/webhooks/…" /></div>

    <h3>Email alerts</h3>
    <p class="section-hint">SillyPilot sends the email for you. Just tick the box and tell it where to send.</p>
    <div class="field check"><input type="checkbox" data-cfg="email.enabled" ${c.email?.enabled ? 'checked' : ''} /> <label style="width:auto">Email me when it matters</label></div>
    <div class="field"><label>Your email address</label><input type="text" data-cfg="email.address" value="${esc(c.email?.address || '')}" placeholder="you@example.com" /></div>

    <h3>SillyPilot Network</h3>
    <p class="section-hint">Chat with other people running SillyPilot. Your messages go through a shared Discord channel.</p>
    <div class="field check"><input type="checkbox" data-cfg="network.enabled" ${c.network?.enabled ? 'checked' : ''} /> <label style="width:auto">Join the network</label></div>
    <div class="field"><label>Username</label><input type="text" data-cfg="network.handle" value="${esc(c.network?.handle || '')}" placeholder="the name others see" /></div>

    <h3>General</h3>
    <div class="field check"><input type="checkbox" data-cfg="openBrowserOnStart" ${c.openBrowserOnStart ? 'checked' : ''} /> <label style="width:auto">Automatically open SillyxPilot after xPilot starts</label></div>
    <div class="field"><label>VATSIM refresh (sec)</label><input type="number" data-cfg="vatsim.pollSeconds" value="${esc(c.vatsim?.pollSeconds ?? 60)}" /></div>
    <div class="field"><label>Map traffic range (nm)</label><input type="number" data-cfg="map.trafficRadiusNm" value="${esc(c.map?.trafficRadiusNm ?? 400)}" /></div>
    <div class="field"><label>Dashboard port</label><input type="number" data-cfg="port" value="${esc(c.port ?? 3000)}" /> <span class="hint" style="margin:0">restart to apply</span></div>

    <div class="btn-row">
      <button class="btn" id="save-settings">Save</button>
      <button class="btn ghost" id="test-alert">Send a test alert</button>
      <span class="save-status" id="save-status"></span>
    </div>`;
  $('#save-settings').addEventListener('click', saveSettings);
  $('#test-alert').addEventListener('click', testAlert);
}
function collectSettings() {
  const patch = {};
  $$('[data-cfg]').forEach((el) => {
    const path = el.dataset.cfg.split('.');
    let val = el.type === 'checkbox' ? el.checked : el.type === 'number' ? Number(el.value) : el.value;
    let o = patch;
    for (let i = 0; i < path.length - 1; i++) o = o[path[i]] = o[path[i]] || {};
    o[path[path.length - 1]] = val;
  });
  return patch;
}
async function saveSettings() {
  const j = await (await fetch('/api/config', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(collectSettings()) })).json();
  state.config = j.config;
  $('#save-status').textContent = 'Saved';
  setTimeout(() => ($('#save-status').textContent = ''), 2500);
}
async function testAlert() {
  await saveSettings();
  $('#save-status').textContent = 'sending test…';
  const j = await (await fetch('/api/test-alert', { method: 'POST' })).json();
  const parts = Object.entries(j.results || {}).map(([k, v]) => `${k}: ${v}`);
  $('#save-status').textContent = parts.length ? parts.join(' · ') : 'nothing configured to test';
}

const notes = $('#notes');
notes.value = localStorage.getItem('silly-notes') || '';
notes.addEventListener('input', () => localStorage.setItem('silly-notes', notes.value));

let es;
function connectEvents() {
  es = new EventSource('/events');
  es.onopen = () => { $('#st-ws').textContent = 'connected'; };
  es.onerror = () => { $('#st-ws').textContent = 'reconnecting…';  };
  es.onmessage = (e) => {
    let msg; try { msg = JSON.parse(e.data); } catch { return; }
    switch (msg.type) {
      case 'snapshot':
        state.messages = msg.messages || []; state.alerts = msg.alerts || []; state.controllers = msg.controllers || [];
        renderLog(); renderAlerts(); renderControllers(); renderAwareness(msg.awareness); renderNetworkStatus(msg.network);
        state.lastMap = msg.map; if (mapState.initialized) renderMap(msg.map);
        applyStatus(msg); ensureUsernameGate(msg);
        break;
      case 'message': addMessage(msg.message); break;
      case 'alert': onAlert(msg.alert); toast(msg.alert); break;
      case 'awareness': renderAwareness(msg.awareness); break;
      case 'controllers': state.controllers = msg.controllers || []; renderControllers(); break;
      case 'map': renderMap(msg.map); break;
      case 'network': renderNetworkStatus(msg.network); break;
      case 'networkChat': addNetworkChat(msg.chat); break;
      case 'status': applyStatus(msg); break;
    }
  };
}
function applyStatus(m) {
  if (m.callsign) $('#st-callsign').textContent = m.callsign;
  if (m.cid) $('#st-cid').textContent = m.cid;
  if (m.connectedToVatsim != null) $('#st-connected').textContent = m.connectedToVatsim ? 'connected' : 'not connected yet';
}
async function ensureUsernameGate() {
  if (!state.config) state.config = await (await fetch('/api/config')).json();
  const handle = state.config?.network?.handle;
  if (handle) showNetworkRoom(handle);
}
connectEvents();
