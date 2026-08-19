# Production Worker heartbeat integration

The live Visitor Light Worker is `lokwod-analytics`. Its existing visitor-event and firmware routes must remain intact.

Do **not** replace the live Worker with `heartbeat-extension.js`. Add the heartbeat routes to the existing Worker.

## Required binding

Create a Workers KV namespace for device health and bind it to the Worker as:

`DEVICE_STATUS`

The binding contains only sanitized health data and no credentials.

## Routes to add

Import or paste the helpers from `heartbeat-extension.js` into the production Worker.

In the existing request router, add:

```js
if (url.pathname === '/v1/heartbeat' && request.method === 'POST') {
  // IMPORTANT: run the exact same device Bearer-token authorization used by
  // /v1/events and the firmware routes BEFORE this call.
  return recordHeartbeat(request, env);
}

if (url.pathname === '/v1/heartbeat/status' && request.method === 'GET') {
  return getHeartbeatStatus(env);
}
```

`POST /v1/heartbeat` must remain device-authenticated.

`GET /v1/heartbeat/status` is intentionally safe to expose without the device token because the helper returns only:

- online/offline state
- last-seen timestamp and age
- firmware version
- uptime
- Wi-Fi RSSI
- age of the last successful visitor poll
- accepted visitor count
- free heap

It does not return the device token, passwords, Wi-Fi SSID, local IP, public IP, or visitor IP data.

## Online definition

The firmware posts once per minute. The status helper reports the device online while the most recently stored heartbeat is no more than three minutes old.

## Deployment safety

Before deployment:

1. Preserve every existing `lokwod-analytics` route.
2. Preserve the existing device authorization logic and secrets.
3. Add the `DEVICE_STATUS` KV binding.
4. Add only the two heartbeat route branches.
5. Verify `/v1/events` still returns normally before considering the deployment complete.
6. Verify `/v1/heartbeat/status` changes to `online: true` after a v1.1.8 device checks in.
