// LOKWOD Website Visitor Light heartbeat helpers.
//
// Integrate these helpers into the EXISTING production lokwod-analytics Worker.
// Do not replace the production Worker wholesale with this file.
//
// Required KV binding: DEVICE_STATUS
// POST /v1/heartbeat must use the same device Bearer-token authorization as
// the existing /v1/events and firmware routes before calling recordHeartbeat().
// GET /v1/heartbeat/status may be public because it returns only sanitized
// device-health data (no device token, public IP, local IP, or Wi-Fi SSID).

const HEARTBEAT_KEY = 'visitor-light:esp32-s3';
const ONLINE_WINDOW_MS = 3 * 60 * 1000;
const STATUS_TTL_SECONDS = 24 * 60 * 60;

function json(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'cache-control': 'no-store',
    },
  });
}

function finiteInteger(value, fallback = 0) {
  const number = Number(value);
  return Number.isFinite(number) ? Math.trunc(number) : fallback;
}

export async function recordHeartbeat(request, env) {
  if (!env.DEVICE_STATUS) {
    return json({ ok: false, error: 'DEVICE_STATUS KV binding is not configured' }, 503);
  }

  let body;
  try {
    body = await request.json();
  } catch {
    return json({ ok: false, error: 'Invalid JSON body' }, 400);
  }

  const firmware = String(body?.firmware || '').slice(0, 32);
  const device = String(body?.device || 'esp32-s3-visitor-light').slice(0, 64);
  const now = Date.now();

  const status = {
    device,
    firmware,
    lastSeen: now,
    uptimeMs: Math.max(0, finiteInteger(body?.uptime_ms)),
    rssi: finiteInteger(body?.rssi),
    lastPollAgeMs: Math.max(0, finiteInteger(body?.last_poll_age_ms)),
    todayVisits: Math.max(0, finiteInteger(body?.today_visits)),
    freeHeap: Math.max(0, finiteInteger(body?.free_heap)),
  };

  await env.DEVICE_STATUS.put(HEARTBEAT_KEY, JSON.stringify(status), {
    expirationTtl: STATUS_TTL_SECONDS,
  });

  return json({ ok: true, receivedAt: now });
}

export async function getHeartbeatStatus(env) {
  if (!env.DEVICE_STATUS) {
    return json({ ok: false, error: 'DEVICE_STATUS KV binding is not configured' }, 503);
  }

  const status = await env.DEVICE_STATUS.get(HEARTBEAT_KEY, 'json');
  if (!status || !status.lastSeen) {
    return json({
      ok: true,
      online: false,
      lastSeen: null,
      ageMs: null,
      device: 'esp32-s3-visitor-light',
      firmware: null,
    });
  }

  const ageMs = Math.max(0, Date.now() - Number(status.lastSeen));
  return json({
    ok: true,
    online: ageMs <= ONLINE_WINDOW_MS,
    ageMs,
    device: status.device,
    firmware: status.firmware,
    lastSeen: status.lastSeen,
    uptimeMs: status.uptimeMs,
    rssi: status.rssi,
    lastPollAgeMs: status.lastPollAgeMs,
    todayVisits: status.todayVisits,
    freeHeap: status.freeHeap,
  });
}
