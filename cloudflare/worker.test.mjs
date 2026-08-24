import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import vm from "node:vm";

async function loadWorker() {
  let source = await readFile(new URL("./worker.js", import.meta.url), "utf8");
  source = source
    .replace('import { DurableObject } from "cloudflare:workers";', "")
    .replace("export class VisitorHub", "class VisitorHub")
    .replace("export default index_default;", "")
    .concat("\nglobalThis.__workerTestExports = { sanitizeIpAddress, sanitizeReferrer, browserBeaconSource, requestIdentity, VisitorHub, index_default };\n");

  class DurableObject {
    constructor(ctx, env) {
      this.ctx = ctx;
      this.env = env;
    }
  }

  const context = vm.createContext({
    Blob,
    console,
    crypto,
    DurableObject,
    Headers,
    Intl,
    Request,
    Response,
    TextDecoder,
    TextEncoder,
    URL,
  });
  vm.runInContext(source, context, { filename: "worker.js" });
  return context.__workerTestExports;
}

const worker = await loadWorker();

test("sanitizes visitor IP addresses", () => {
  assert.equal(worker.sanitizeIpAddress("203.0.113.25"), "203.0.113.25");
  assert.equal(worker.sanitizeIpAddress("2001:db8::42"), "2001:db8::42");
  assert.equal(worker.sanitizeIpAddress("203.0.113.25, spoofed"), "");
});

test("normalizes referrers and strips credentials and fragments", () => {
  assert.equal(
    worker.sanitizeReferrer("https://user:password@example.com/search?q=boat#private"),
    "https://example.com/search?q=boat",
  );
  assert.equal(worker.sanitizeReferrer("javascript:alert(1)"), "");
});

test("browser beacon sends the document referrer for visits and affiliate clicks", () => {
  const source = worker.browserBeaconSource();
  assert.equal(source.match(/referrer: document\.referrer \|\| ""/g)?.length, 2);
});

test("request identity retains the validated IP and hashes the private lookup key", async () => {
  const request = new Request("https://example.com/v1/hit", {
    headers: {
      "CF-Connecting-IP": "203.0.113.25",
      "User-Agent": "Visitor Test Browser",
    },
  });
  const identity = await worker.requestIdentity(request, { FINGERPRINT_SALT: "test-salt" });
  assert.equal(identity.ip, "203.0.113.25");
  assert.match(identity.ipKey, /^[0-9a-f]{64}$/);
  assert.equal(identity.ipKey.includes("203.0.113.25"), false);
});

test("history returns the newest 50 real events and excludes cloud tests", async () => {
  const events = Array.from({ length: 55 }, (_, index) => ({
    id: index + 1,
    ts: index + 1,
    kind: index % 8 === 0 ? "affiliate_click" : "visit",
    ip: `203.0.113.${index + 1}`,
    test: false,
  }));
  events.splice(30, 0, { id: 900, ts: 900, kind: "visit", test: true });

  const storage = {
    async get() {
      return {
        version: 1,
        seq: 900,
        ownerIpKey: "",
        events,
        dedupe: {},
        rate: {},
        stats: { day: "", total: 0, sites: {}, affiliateClicks: 0 },
      };
    },
    async put() {},
  };
  const hub = new worker.VisitorHub({ storage }, {});
  const response = await hub.fetch(new Request("https://visitor-hub/internal/history", {
    method: "POST",
    body: JSON.stringify({ limit: 50 }),
  }));
  const payload = await response.json();

  assert.equal(response.status, 200);
  assert.equal(payload.ok, true);
  assert.equal(payload.visitors.length, 50);
  assert.equal(payload.visitors.some((event) => event.test === true), false);
  assert.equal(payload.visitors[0].id, 55);
  assert.equal(payload.visitors.at(-1).id, 6);
});
