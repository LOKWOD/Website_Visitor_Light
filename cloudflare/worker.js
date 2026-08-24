var __defProp = Object.defineProperty;

var __name = (target, value) => __defProp(target, "name", { value, configurable: true });

// src/index.js

import { DurableObject } from "cloudflare:workers";

// src/core.js

var SITE_CONFIG = Object.freeze({

  "nautical-dream": Object.freeze({

    label: "Nautical Dream",

    color: Object.freeze([0, 185, 255]),

    domains: Object.freeze(["nauticaldream.com"])

  }),

  lokwod: Object.freeze({

    label: "LOKWOD",

    color: Object.freeze([0, 82, 255]),

    domains: Object.freeze(["lokwod.com"])

  }),

  "life-in-the-simulation": Object.freeze({

    label: "Life in the Simulation",

    color: Object.freeze([0, 235, 95]),

    domains: Object.freeze(["lifeinthesimulation.com"])

  }),

  "beautiful-mens-club": Object.freeze({

    label: "Beautiful Men's Club",

    color: Object.freeze([180, 35, 255]),

    domains: Object.freeze(["beautifulmensclub.com"])

  }),

  "mr-adventure-dad": Object.freeze({

    label: "Mr Adventure Dad",

    color: Object.freeze([255, 96, 0]),

    domains: Object.freeze(["mradventuredad.com"])

  }),

  "syracuse-appraiser": Object.freeze({

    label: "Syracuse Appraiser",

    color: Object.freeze([255, 174, 0]),

    domains: Object.freeze(["syracuseappraiser.com"])

  }),

  "accurate-re-appraisals": Object.freeze({

    label: "Accurate RE Appraisals (.com)",

    color: Object.freeze([235, 235, 235]),

    domains: Object.freeze(["accuratereappraisals.com"])

  }),

  "accurate-re-appraisals-org": Object.freeze({

    label: "Accurate RE Appraisals (.org)",

    color: Object.freeze([255, 0, 0]),

    domains: Object.freeze(["accuratereappraisals.org"])

  }),

  "dish-gal": Object.freeze({

    label: "Dish Gal",

    color: Object.freeze([255, 0, 140]),

    domains: Object.freeze(["dishgal.com"])

  }),

  blappos: Object.freeze({

    label: "Blappos",

    color: Object.freeze([0, 255, 180]),

    domains: Object.freeze(["blappos.com"])

  }),

  "big-bud-man": Object.freeze({

    label: "Big Bud Man",

    color: Object.freeze([170, 255, 0]),

    domains: Object.freeze(["bigbudman.com"])

  }),

  "the-crypto-appraiser": Object.freeze({

    label: "The Crypto Appraiser",

    color: Object.freeze([75, 0, 255]),

    domains: Object.freeze(["thecryptoappraiser.com"])

  })

});

function getSiteConfig(siteId) {

  if (typeof siteId !== "string") return null;

  return SITE_CONFIG[siteId.trim().toLowerCase()] ?? null;

}

__name(getSiteConfig, "getSiteConfig");

function sanitizeText(value, maxLength) {

  if (typeof value !== "string") return "";

  return value.replace(/[\u0000-\u001F\u007F]/g, " ").replace(/\s+/g, " ").trim().slice(0, maxLength);

}

__name(sanitizeText, "sanitizeText");

function sanitizeIpAddress(value) {

  const ip = sanitizeText(String(value ?? ""), 64);

  return /^[0-9a-f:.]+$/i.test(ip) ? ip : "";

}

__name(sanitizeIpAddress, "sanitizeIpAddress");

function sanitizeReferrer(value) {

  const raw = sanitizeText(value, 500);

  if (!raw) return "";

  try {

    const parsed = new URL(raw);

    if (parsed.protocol !== "https:" && parsed.protocol !== "http:") return "";

    parsed.username = "";

    parsed.password = "";

    parsed.hash = "";

    return parsed.toString().slice(0, 500);

  } catch {

    return "";

  }

}

__name(sanitizeReferrer, "sanitizeReferrer");

function normalizeOrigin(origin) {

  if (typeof origin !== "string" || origin.length > 300) return null;

  try {

    const parsed = new URL(origin);

    if (parsed.protocol !== "https:" && parsed.protocol !== "http:") return null;

    return parsed;

  } catch {

    return null;

  }

}

__name(normalizeOrigin, "normalizeOrigin");

function isAllowedSiteOrigin(siteId, origin) {

  const config = getSiteConfig(siteId);

  const parsed = normalizeOrigin(origin);

  if (!config || !parsed) return false;

  const host = parsed.hostname.toLowerCase().replace(/\.$/, "");

  if (host === "localhost" || host === "127.0.0.1") return true;

  if (host === "lokwod.github.io") return true;

  return config.domains.some((domain) => host === domain || host === `www.${domain}`);

}

__name(isAllowedSiteOrigin, "isAllowedSiteOrigin");

function isLikelyBot(userAgent, cfProperties = null) {

  const ua = String(userAgent ?? "").toLowerCase();

  if (!ua || ua.length > 1e3) return true;

  const botManagement = cfProperties?.botManagement;

  if (botManagement?.verifiedBot === true) return true;

  if (typeof botManagement?.score === "number" && botManagement.score < 20) return true;

  return /(?:bot\b|crawler|spider|slurp|bingpreview|facebookexternalhit|headless|lighthouse|pagespeed|pingdom|uptimerobot|statuscake|curl\/|wget\/|python-requests|go-http-client|httpclient|axios\/)/i.test(

    ua

  );

}

__name(isLikelyBot, "isLikelyBot");

function clampInteger(value, minimum, maximum, fallback) {

  const parsed = Number.parseInt(String(value), 10);

  if (!Number.isFinite(parsed)) return fallback;

  return Math.min(maximum, Math.max(minimum, parsed));

}

__name(clampInteger, "clampInteger");

function browserBeaconSource() {

  return String.raw`(() => {

  "use strict";

  const script = document.currentScript;

  const site = script && script.dataset ? script.dataset.site : "";

  if (!script || !site || navigator.webdriver) return;

  try {

    if (window.top !== window.self) return;

  } catch {

    return;

  }

  try {

    if (localStorage.getItem("lokwodVisitorBeaconDisabled") === "1") return;

  } catch {

    // Storage can be unavailable in strict privacy modes; notification still works.

  }

  const endpoint = new URL("/v1/hit", script.src).href;

  const sendPayload = (details) => {

    const payload = JSON.stringify({ site, ...details });

    const body = new Blob([payload], { type: "text/plain;charset=UTF-8" });

    if (navigator.sendBeacon && navigator.sendBeacon(endpoint, body)) return;

    fetch(endpoint, {

      method: "POST",

      mode: "no-cors",

      keepalive: true,

      headers: { "Content-Type": "text/plain;charset=UTF-8" },

      body: payload,

    }).catch(() => {});

  };

  let visitSent = false;

  const sendVisit = () => {

    if (visitSent || document.visibilityState === "prerender") return;

    visitSent = true;

    sendPayload({

      kind: "visit",

      path: location.pathname || "/",

      title: document.title || "",

      referrer: document.referrer || "",

    });

  };

  document.addEventListener("click", (event) => {

    const target = event.target instanceof Element ? event.target : null;

    const link = target ? target.closest('a[data-affiliate-active="true"]') : null;

    if (!link) return;

    sendPayload({

      kind: "affiliate_click",

      path: location.pathname || "/",

      title: (link.textContent || document.title || "Affiliate link").trim().slice(0, 140),

      referrer: document.referrer || "",

    });

  }, { capture: true });

  if (document.readyState === "complete") {

    setTimeout(sendVisit, 250);

  } else {

    addEventListener("load", () => setTimeout(sendVisit, 250), { once: true });

  }

})();`;

}

__name(browserBeaconSource, "browserBeaconSource");

function calendarDayInTimeZone(timestamp, timeZone = "America/New_York") {

  const instant = new Date(Number.isFinite(timestamp) ? timestamp : Date.now());

  const parts = new Intl.DateTimeFormat("en-US", {

    timeZone,

    year: "numeric",

    month: "2-digit",

    day: "2-digit"

  }).formatToParts(instant);

  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]));

  return `${values.year}-${values.month}-${values.day}`;

}

__name(calendarDayInTimeZone, "calendarDayInTimeZone");

function selectPendingEvents(events, after, batchSize, latestSequence, bootstrap = false) {

  const safeEvents = Array.isArray(events) ? events : [];

  const safeAfter = Number.isSafeInteger(after) && after >= 0 ? after : 0;

  const safeBatch = clampInteger(batchSize, 1, 32, 12);

  const safeLatest = Number.isSafeInteger(latestSequence) && latestSequence >= 0 ? latestSequence : 0;

  if (bootstrap) return { events: [], cursor: safeLatest };

  const pending = safeEvents.filter((event) => Number.isSafeInteger(event?.id) && event.id > safeAfter).sort((left, right) => left.id - right.id).slice(0, safeBatch);

  return {

    events: pending,

    cursor: pending.length > 0 ? pending[pending.length - 1].id : safeLatest

  };

}

__name(selectPendingEvents, "selectPendingEvents");

// src/index.js

var SERVICE_VERSION = "1.3.0";

var STATE_KEY = "hub-state-v1";

var HUB_NAME = "primary";

var textEncoder = new TextEncoder();

var FIRMWARE_REPOSITORY = "LOKWOD/Website_Visitor_Light";

var FIRMWARE_ASSET_NAME = "LOKWOD_Visitor_Light.bin";

var FIRMWARE_USER_AGENT = "LOKWOD-Visitor-Light-Update-Proxy/1.1";

var PINNED_FIRMWARE_VERSION = "1.1.5";

var PINNED_FIRMWARE_ASSET_PATH = "/LOKWOD_Visitor_Light.bin";

var PINNED_FIRMWARE_SHA256 = "06cc64b0265debffedd01b1a1eafea0bbbac6879f6c17a4e3cbf58974b6a2afa";

function normalizeFirmwareVersion(value) {

  const text = String(value ?? "").trim().replace(/^v/i, "").split(/[+-]/, 1)[0];

  return /^\d+\.\d+\.\d+$/.test(text) ? text : "";

}

__name(normalizeFirmwareVersion, "normalizeFirmwareVersion");

function releaseTagIsSafe(value) {

  return /^v\d+\.\d+\.\d+$/.test(String(value ?? ""));

}

__name(releaseTagIsSafe, "releaseTagIsSafe");

function compareFirmwareVersions(left, right) {

  const parse = /* @__PURE__ */ __name((value) => normalizeFirmwareVersion(value).split(".").map((part) => Number.parseInt(part, 10)), "parse");

  const a = parse(left);

  const b = parse(right);

  if (a.length !== 3 || b.length !== 3 || a.some(Number.isNaN) || b.some(Number.isNaN)) return 0;

  for (let index = 0; index < 3; index += 1) {

    if (a[index] !== b[index]) return a[index] > b[index] ? 1 : -1;

  }

  return 0;

}

__name(compareFirmwareVersions, "compareFirmwareVersions");

function pinnedFirmwareManifest(requestUrl) {

  const downloadUrl = new URL("/v1/firmware/download", requestUrl.origin);

  downloadUrl.searchParams.set("tag", `v${PINNED_FIRMWARE_VERSION}`);

  return {

    ok: true,

    version: PINNED_FIRMWARE_VERSION,

    asset: FIRMWARE_ASSET_NAME,

    size: 1094624,

    sha256: PINNED_FIRMWARE_SHA256,

    source: "cloudflare-static-asset",

    downloadUrl: downloadUrl.toString()

  };

}

__name(pinnedFirmwareManifest, "pinnedFirmwareManifest");

async function fetchGitHubRelease(apiUrl) {

  return fetch(apiUrl, {

    headers: {

      Accept: "application/vnd.github+json",

      "User-Agent": FIRMWARE_USER_AGENT,

      "X-GitHub-Api-Version": "2022-11-28"

    },

    cf: { cacheEverything: true, cacheTtl: 300 }

  });

}

__name(fetchGitHubRelease, "fetchGitHubRelease");

function findFirmwareAsset(release) {

  const assets = Array.isArray(release?.assets) ? release.assets : [];

  return assets.find((asset) => asset?.name === FIRMWARE_ASSET_NAME) ?? null;

}

__name(findFirmwareAsset, "findFirmwareAsset");

async function handleFirmwareLatest(request, env, requestUrl) {

  if (!isAuthorized(request, env)) return jsonResponse({ ok: false, error: "unauthorized" }, 401);

  const pinned = pinnedFirmwareManifest(requestUrl);

  try {

    const apiUrl = `https://api.github.com/repos/${FIRMWARE_REPOSITORY}/releases/latest`;

    const github = await fetchGitHubRelease(apiUrl);

    if (!github.ok) {

      console.warn("Firmware release lookup unavailable; serving pinned v1.1.5 manifest", github.status);

      return jsonResponse(pinned);

    }

    const release = await github.json();

    const version = normalizeFirmwareVersion(release?.tag_name);

    const asset = findFirmwareAsset(release);

    if (!version || !asset?.browser_download_url || compareFirmwareVersions(version, PINNED_FIRMWARE_VERSION) <= 0) {

      return jsonResponse(pinned);

    }

    const downloadUrl = new URL("/v1/firmware/download", requestUrl.origin);

    downloadUrl.searchParams.set("tag", `v${version}`);

    return jsonResponse({

      ok: true,

      version,

      asset: FIRMWARE_ASSET_NAME,

      size: Number.isFinite(asset.size) ? asset.size : 0,

      source: "github-release-via-cloudflare",

      downloadUrl: downloadUrl.toString()

    });

  } catch (error) {

    console.warn("Firmware release lookup threw; serving pinned v1.1.5 manifest", error);

    return jsonResponse(pinned);

  }

}

__name(handleFirmwareLatest, "handleFirmwareLatest");

async function handleFirmwareDownload(request, env, url) {

  if (!isAuthorized(request, env)) return jsonResponse({ ok: false, error: "unauthorized" }, 401);

  const tag = url.searchParams.get("tag") || "";

  if (!releaseTagIsSafe(tag)) return jsonResponse({ ok: false, error: "invalid_tag" }, 400);

  if (tag === `v${PINNED_FIRMWARE_VERSION}`) {

    if (!env.FIRMWARE_ASSETS) {

      return jsonResponse({ ok: false, error: "firmware_asset_binding_missing" }, 503);

    }

    const assetUrl = new URL(PINNED_FIRMWARE_ASSET_PATH, "https://firmware-assets.invalid");

    const assetResponse = await env.FIRMWARE_ASSETS.fetch(assetUrl);

    if (!assetResponse.ok || !assetResponse.body) {

      console.error("Pinned firmware asset unavailable", assetResponse.status);

      return jsonResponse({ ok: false, error: "pinned_asset_unavailable" }, 503);

    }

    const headers2 = new Headers(assetResponse.headers);

    headers2.set("Content-Type", "application/octet-stream");

    headers2.set("Cache-Control", "private, no-store");

    headers2.set("X-Content-Type-Options", "nosniff");

    headers2.set("X-LOKWOD-Firmware-Version", PINNED_FIRMWARE_VERSION);

    headers2.set("X-LOKWOD-Firmware-SHA256", PINNED_FIRMWARE_SHA256);

    headers2.set("X-LOKWOD-Firmware-Source", "cloudflare-static-asset");

    return new Response(assetResponse.body, { status: 200, headers: headers2 });

  }

  const apiUrl = `https://api.github.com/repos/${FIRMWARE_REPOSITORY}/releases/tags/${encodeURIComponent(tag)}`;

  const github = await fetchGitHubRelease(apiUrl);

  if (github.status === 404) return jsonResponse({ ok: false, error: "release_not_found" }, 404);

  if (!github.ok) {

    console.error("Firmware tagged release lookup failed", github.status, await github.text());

    return jsonResponse({ ok: false, error: "release_lookup_failed" }, 502);

  }

  const release = await github.json();

  const version = normalizeFirmwareVersion(release?.tag_name);

  const asset = findFirmwareAsset(release);

  if (!version || `v${version}` !== tag || !asset?.browser_download_url) {

    return jsonResponse({ ok: false, error: "invalid_release" }, 502);

  }

  const binary = await fetch(asset.browser_download_url, {

    headers: {

      Accept: "application/octet-stream",

      "User-Agent": FIRMWARE_USER_AGENT

    },

    redirect: "follow"

  });

  if (!binary.ok || !binary.body) {

    console.error("Firmware asset fetch failed", binary.status);

    return jsonResponse({ ok: false, error: "asset_fetch_failed" }, 502);

  }

  const headers = new Headers();

  headers.set("Content-Type", "application/octet-stream");

  headers.set("Cache-Control", "private, no-store");

  headers.set("X-Content-Type-Options", "nosniff");

  headers.set("X-LOKWOD-Firmware-Version", version);

  headers.set("X-LOKWOD-Firmware-Source", "github-release-via-cloudflare");

  const contentLength = binary.headers.get("Content-Length");

  if (contentLength) headers.set("Content-Length", contentLength);

  const etag = binary.headers.get("ETag");

  if (etag) headers.set("ETag", etag);

  return new Response(binary.body, { status: 200, headers });

}

__name(handleFirmwareDownload, "handleFirmwareDownload");

function securityHeaders() {

  return {

    "Cache-Control": "no-store",

    "Content-Security-Policy": "default-src 'none'; frame-ancestors 'none'",

    "X-Content-Type-Options": "nosniff"

  };

}

__name(securityHeaders, "securityHeaders");

function jsonResponse(value, status = 200, extraHeaders = {}) {

  return new Response(JSON.stringify(value), {

    status,

    headers: {

      "Content-Type": "application/json; charset=utf-8",

      ...securityHeaders(),

      ...extraHeaders

    }

  });

}

__name(jsonResponse, "jsonResponse");

function corsHeaders(origin) {

  return {

    "Access-Control-Allow-Origin": origin,

    "Access-Control-Allow-Methods": "POST, OPTIONS",

    "Access-Control-Allow-Headers": "Content-Type",

    "Access-Control-Max-Age": "86400",

    Vary: "Origin"

  };

}

__name(corsHeaders, "corsHeaders");

async function readSmallJson(request, maximumBytes = 4096) {

  const declared = Number.parseInt(request.headers.get("Content-Length") ?? "0", 10);

  if (Number.isFinite(declared) && declared > maximumBytes) {

    throw new RangeError("Request body is too large");

  }

  const text = await request.text();

  if (text.length > maximumBytes) throw new RangeError("Request body is too large");

  if (!text.trim()) return {};

  return JSON.parse(text);

}

__name(readSmallJson, "readSmallJson");

function constantTimeEqual(left, right) {

  const a = String(left ?? "");

  const b = String(right ?? "");

  const length = Math.max(a.length, b.length);

  let difference = a.length ^ b.length;

  for (let index = 0; index < length; index += 1) {

    difference |= (a.charCodeAt(index) || 0) ^ (b.charCodeAt(index) || 0);

  }

  return difference === 0;

}

__name(constantTimeEqual, "constantTimeEqual");

function isAuthorized(request, env) {

  const authorization = request.headers.get("Authorization") ?? "";

  const match = /^Bearer\s+(.+)$/i.exec(authorization);

  return Boolean(match && env.DEVICE_TOKEN && constantTimeEqual(match[1], env.DEVICE_TOKEN));

}

__name(isAuthorized, "isAuthorized");

async function sha256Hex(value) {

  const digest = await crypto.subtle.digest("SHA-256", textEncoder.encode(value));

  return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, "0")).join("");

}

__name(sha256Hex, "sha256Hex");

async function requestIdentity(request, env) {

  const ip = sanitizeIpAddress(request.headers.get("CF-Connecting-IP")) || "unknown";

  const userAgent = request.headers.get("User-Agent") || "unknown";

  const salt = env.FINGERPRINT_SALT || "missing-salt";

  return {

    ip,

    ipKey: await sha256Hex(`${salt}|ip|${ip}`),

    userAgent,

    visitorSeed: `${salt}|visitor|${ip}|${userAgent}`

  };

}

__name(requestIdentity, "requestIdentity");

function getHub(env) {

  return env.VISITOR_HUB.getByName(HUB_NAME);

}

__name(getHub, "getHub");

async function forwardHubResponse(response) {

  const headers = new Headers(response.headers);

  headers.set("Cache-Control", "no-store");

  headers.set("X-Content-Type-Options", "nosniff");

  return new Response(response.body, { status: response.status, headers });

}

__name(forwardHubResponse, "forwardHubResponse");

function initialState() {

  return {

    version: 1,

    seq: 0,

    ownerIpKey: "",

    events: [],

    dedupe: {},

    rate: {},

    stats: { day: "", total: 0, sites: {}, affiliateClicks: 0 }

  };

}

__name(initialState, "initialState");

function normalizeState(value) {

  const state = value && typeof value === "object" ? value : initialState();

  state.version = 1;

  state.seq = Number.isSafeInteger(state.seq) && state.seq >= 0 ? state.seq : 0;

  state.ownerIpKey = typeof state.ownerIpKey === "string" ? state.ownerIpKey : "";

  state.events = Array.isArray(state.events) ? state.events : [];

  state.dedupe = state.dedupe && typeof state.dedupe === "object" ? state.dedupe : {};

  state.rate = state.rate && typeof state.rate === "object" ? state.rate : {};

  state.stats = state.stats && typeof state.stats === "object" ? state.stats : { day: "", total: 0, sites: {}, affiliateClicks: 0 };

  state.stats.day = typeof state.stats.day === "string" ? state.stats.day : "";

  state.stats.total = Number.isSafeInteger(state.stats.total) ? state.stats.total : 0;

  state.stats.affiliateClicks = Number.isSafeInteger(state.stats.affiliateClicks) ? state.stats.affiliateClicks : 0;

  state.stats.sites = state.stats.sites && typeof state.stats.sites === "object" ? state.stats.sites : {};

  return state;

}

__name(normalizeState, "normalizeState");

function resetDailyStatsIfNeeded(state, now) {

  const day = calendarDayInTimeZone(now);

  if (state.stats.day !== day) state.stats = { day, total: 0, sites: {}, affiliateClicks: 0 };

}

__name(resetDailyStatsIfNeeded, "resetDailyStatsIfNeeded");

function pruneStateMaps(state, now, dedupeMs) {

  const dedupeCutoff = now - Math.max(dedupeMs * 12, 36e5);

  for (const [key, timestamp] of Object.entries(state.dedupe)) {

    if (!Number.isFinite(timestamp) || timestamp < dedupeCutoff) delete state.dedupe[key];

  }

  const rateCutoff = now - 12e4;

  for (const [key, bucket] of Object.entries(state.rate)) {

    if (!bucket || !Number.isFinite(bucket.started) || bucket.started < rateCutoff) delete state.rate[key];

  }

}

__name(pruneStateMaps, "pruneStateMaps");

function appendEvent(state, event, now, retention) {

  state.seq += 1;

  const stored = { ...event, id: state.seq, ts: now };

  state.events.push(stored);

  if (state.events.length > retention) state.events.splice(0, state.events.length - retention);

  resetDailyStatsIfNeeded(state, now);

  if (stored.kind === "affiliate_click") {

    state.stats.affiliateClicks += 1;

  } else {

    state.stats.total += 1;

    state.stats.sites[stored.site] = (state.stats.sites[stored.site] || 0) + 1;

  }

  return stored;

}

__name(appendEvent, "appendEvent");

export class VisitorHub extends DurableObject {

  static {

    __name(this, "VisitorHub");

  }

  constructor(ctx, env) {

    super(ctx, env);

    this.ctx = ctx;

    this.env = env;

  }

  async loadState() {

    return normalizeState(await this.ctx.storage.get(STATE_KEY));

  }

  async saveState(state) {

    await this.ctx.storage.put(STATE_KEY, state);

  }

  async fetch(request) {

    const url = new URL(request.url);

    try {

      if (request.method === "POST" && url.pathname === "/internal/record") {

        return await this.record(await readSmallJson(request, 8192));

      }

      if (request.method === "POST" && url.pathname === "/internal/poll") {

        return await this.poll(await readSmallJson(request));

      }

      if (request.method === "POST" && url.pathname === "/internal/history") {

        return await this.history(await readSmallJson(request));

      }

      if (request.method === "POST" && url.pathname === "/internal/test") {

        return await this.test(await readSmallJson(request));

      }

      if (request.method === "POST" && url.pathname === "/internal/status") {

        return await this.status();

      }

      if (request.method === "POST" && url.pathname === "/internal/reset") {

        return await this.reset();

      }

      return jsonResponse({ ok: false, error: "not_found" }, 404);

    } catch (error) {

      console.error("VisitorHub error", error);

      return jsonResponse({ ok: false, error: "hub_error" }, 500);

    }

  }

  async record(payload) {

    const now = Date.now();

    const state = await this.loadState();

    const retention = clampInteger(this.env.EVENT_RETENTION, 64, 256, 128);

    pruneStateMaps(state, now, 0);

    const ipKey = typeof payload.ipKey === "string" ? payload.ipKey : "";

    if (!ipKey || !payload.event) {

      return jsonResponse({ ok: false, error: "invalid_record" }, 400);

    }

    let bucket = state.rate[ipKey];

    if (!bucket || now - bucket.started >= 6e4) bucket = { started: now, count: 0 };

    bucket.count += 1;

    state.rate[ipKey] = bucket;

    if (bucket.count > 30) {

      await this.saveState(state);

      return jsonResponse({ ok: true, ignored: "rate_limit" });

    }

    const event = appendEvent(state, payload.event, now, retention);

    await this.saveState(state);

    return jsonResponse({ ok: true, eventId: event.id });

  }

  async poll(payload) {

    const state = await this.loadState();

    const previousStatsDay = state.stats.day;

    resetDailyStatsIfNeeded(state, Date.now());

    let changed = state.stats.day !== previousStatsDay;

    if (typeof payload.ownerIpKey === "string" && payload.ownerIpKey && payload.ownerIpKey !== state.ownerIpKey) {

      state.ownerIpKey = payload.ownerIpKey;

      changed = true;

    }

    const after = Number.isSafeInteger(payload.after) && payload.after >= 0 ? payload.after : 0;

    const batchSize = clampInteger(this.env.MAX_POLL_BATCH, 1, 32, 12);

    const selection = selectPendingEvents(state.events, after, batchSize, state.seq, Boolean(payload.bootstrap));

    if (changed) await this.saveState(state);

    return jsonResponse({

      ok: true,

      cursor: selection.cursor,

      events: selection.events,

      stats: state.stats,

      retained: state.events.length

    });

  }

  async history(payload) {

    const state = await this.loadState();

    const limit = clampInteger(payload.limit, 1, 50, 50);

    const visitors = state.events.filter((event) => event?.test !== true).slice(-limit).reverse();

    return jsonResponse({

      ok: true,

      visitors,

      count: visitors.length,

      retained: state.events.length

    });

  }

  async test(payload) {

    const now = Date.now();

    const state = await this.loadState();

    const siteId = typeof payload.site === "string" ? payload.site : "lokwod";

    const config = getSiteConfig(siteId) || getSiteConfig("lokwod");

    const retention = clampInteger(this.env.EVENT_RETENTION, 64, 256, 128);

    const event = appendEvent(

      state,

      {

        site: getSiteConfig(siteId) ? siteId : "lokwod",

        label: config.label,

        color: [...config.color],

        path: "/system-test",

        title: "Visitor light cloud test",

        city: "",

        region: "",

        country: "",

        test: true

      },

      now,

      retention

    );

    await this.saveState(state);

    return jsonResponse({ ok: true, eventId: event.id });

  }

  async status() {

    const state = await this.loadState();

    const previousStatsDay = state.stats.day;

    resetDailyStatsIfNeeded(state, Date.now());

    if (state.stats.day !== previousStatsDay) await this.saveState(state);

    return jsonResponse({

      ok: true,

      cursor: state.seq,

      ownerRegistered: Boolean(state.ownerIpKey),

      retained: state.events.length,

      latest: state.events.at(-1) || null,

      stats: state.stats

    });

  }

  async reset() {

    const state = await this.loadState();

    const fresh = initialState();

    fresh.ownerIpKey = state.ownerIpKey;

    await this.saveState(fresh);

    return jsonResponse({ ok: true, cursor: 0 });

  }

};

async function handleHit(request, env) {

  const origin = request.headers.get("Origin") || "";

  let payload;

  try {

    payload = await readSmallJson(request);

  } catch (error) {

    const status = error instanceof RangeError ? 413 : 400;

    return jsonResponse({ ok: false, error: "invalid_body" }, status);

  }

  const siteId = sanitizeText(payload.site, 64).toLowerCase();

  const site = getSiteConfig(siteId);

  const kind = payload.kind === "affiliate_click" ? "affiliate_click" : "visit";

  if (!site || !isAllowedSiteOrigin(siteId, origin)) {

    return jsonResponse({ ok: false, error: "origin_not_allowed" }, 403);

  }

  const cf = request.cf || null;

  const userAgent = request.headers.get("User-Agent") || "";

  if (isLikelyBot(userAgent, cf)) {

    return new Response(null, { status: 204, headers: corsHeaders(origin) });

  }

  const identity = await requestIdentity(request, env);

  const visitorKey = await sha256Hex(`${identity.visitorSeed}|${siteId}`);

  const visitorId = visitorKey.slice(0, 10).toUpperCase();

  const isAffiliateClick = kind === "affiliate_click";

  const event = {

    site: siteId,

    kind,

    label: isAffiliateClick ? `Affiliate click · ${site.label}` : site.label,

    color: isAffiliateClick ? [255, 215, 0] : [...site.color],

    path: sanitizeText(payload.path || "/", 180) || "/",

    title: sanitizeText(payload.title || "", 140),

    referrer: sanitizeReferrer(payload.referrer || ""),

    ip: identity.ip,

    visitorId,

    city: sanitizeText(cf?.city || "", 80),

    region: sanitizeText(cf?.region || cf?.regionCode || "", 80),

    country: sanitizeText(cf?.country || "", 3).toUpperCase(),

    test: false

  };

  const hubResponse = await getHub(env).fetch("https://visitor-hub/internal/record", {

    method: "POST",

    headers: { "Content-Type": "application/json" },

    body: JSON.stringify({ ipKey: identity.ipKey, visitorKey, event })

  });

  if (!hubResponse.ok) {

    console.error("Visitor record failed", hubResponse.status, await hubResponse.text());

    return jsonResponse({ ok: false, error: "record_failed" }, 502, corsHeaders(origin));

  }

  return new Response(null, { status: 204, headers: corsHeaders(origin) });

}

__name(handleHit, "handleHit");

async function handleEvents(request, env, url) {

  if (!isAuthorized(request, env)) return jsonResponse({ ok: false, error: "unauthorized" }, 401);

  const identity = await requestIdentity(request, env);

  const after = Math.max(0, Number.parseInt(url.searchParams.get("after") || "0", 10) || 0);

  const bootstrap = url.searchParams.get("bootstrap") === "1";

  const response = await getHub(env).fetch("https://visitor-hub/internal/poll", {

    method: "POST",

    headers: { "Content-Type": "application/json" },

    body: JSON.stringify({ after, bootstrap, ownerIpKey: identity.ipKey })

  });

  return forwardHubResponse(response);

}

__name(handleEvents, "handleEvents");

async function handleHistory(request, env, url) {

  if (!isAuthorized(request, env)) return jsonResponse({ ok: false, error: "unauthorized" }, 401);

  const limit = clampInteger(url.searchParams.get("limit"), 1, 50, 50);

  const response = await getHub(env).fetch("https://visitor-hub/internal/history", {

    method: "POST",

    headers: { "Content-Type": "application/json" },

    body: JSON.stringify({ limit })

  });

  return forwardHubResponse(response);

}

__name(handleHistory, "handleHistory");

async function handleTest(request, env) {

  if (!isAuthorized(request, env)) return jsonResponse({ ok: false, error: "unauthorized" }, 401);

  let payload = {};

  try {

    payload = await readSmallJson(request);

  } catch {

    return jsonResponse({ ok: false, error: "invalid_body" }, 400);

  }

  const siteId = sanitizeText(payload.site || "lokwod", 64).toLowerCase();

  const response = await getHub(env).fetch("https://visitor-hub/internal/test", {

    method: "POST",

    headers: { "Content-Type": "application/json" },

    body: JSON.stringify({ site: getSiteConfig(siteId) ? siteId : "lokwod" })

  });

  return forwardHubResponse(response);

}

__name(handleTest, "handleTest");

async function handleStatus(request, env) {

  if (!isAuthorized(request, env)) return jsonResponse({ ok: false, error: "unauthorized" }, 401);

  return forwardHubResponse(

    await getHub(env).fetch("https://visitor-hub/internal/status", { method: "POST" })

  );

}

__name(handleStatus, "handleStatus");

async function handleReset(request, env) {

  if (!isAuthorized(request, env)) return jsonResponse({ ok: false, error: "unauthorized" }, 401);

  return forwardHubResponse(

    await getHub(env).fetch("https://visitor-hub/internal/reset", { method: "POST" })

  );

}

__name(handleReset, "handleReset");

var index_default = {

  async fetch(request, env) {

    const url = new URL(request.url);

    if (request.method === "OPTIONS" && url.pathname === "/v1/hit") {

      const origin = request.headers.get("Origin") || "";

      return new Response(null, { status: 204, headers: corsHeaders(origin) });

    }

    if (request.method === "GET" && url.pathname === "/beacon.js") {

      return new Response(browserBeaconSource(), {

        headers: {

          "Content-Type": "application/javascript; charset=utf-8",

          "Cache-Control": "public, max-age=300, stale-while-revalidate=86400",

          "Access-Control-Allow-Origin": "*",

          "X-Content-Type-Options": "nosniff"

        }

      });

    }

    if (request.method === "POST" && url.pathname === "/v1/hit") return handleHit(request, env);

    if (request.method === "GET" && url.pathname === "/v1/events") return handleEvents(request, env, url);

    if (request.method === "GET" && url.pathname === "/v1/history") return handleHistory(request, env, url);

    if (request.method === "POST" && url.pathname === "/v1/test") return handleTest(request, env);

    if (request.method === "GET" && url.pathname === "/v1/status") return handleStatus(request, env);

    if (request.method === "POST" && url.pathname === "/v1/reset") return handleReset(request, env);

    if (request.method === "GET" && url.pathname === "/v1/firmware/latest") return handleFirmwareLatest(request, env, url);

    if (request.method === "GET" && url.pathname === "/v1/firmware/download") return handleFirmwareDownload(request, env, url);

    if (request.method === "GET" && (url.pathname === "/" || url.pathname === "/health")) {

      return jsonResponse({

        ok: true,

        service: "LOKWOD Website Visitor Light",

        version: SERVICE_VERSION,

        updatePath: "cloudflare-proxy",

        pinnedFirmware: PINNED_FIRMWARE_VERSION,

        pinnedFirmwareSha256: PINNED_FIRMWARE_SHA256,

        time: (/* @__PURE__ */ new Date()).toISOString()

      });

    }

    return jsonResponse({ ok: false, error: "not_found" }, 404);

  }

};

export default index_default;

//# sourceMappingURL=index.js.map
