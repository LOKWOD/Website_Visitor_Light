import { readFileSync } from "node:fs";

const workerSource = readFileSync(new URL("./worker.js", import.meta.url), "utf8");
const configStart = workerSource.indexOf("var SITE_CONFIG = Object.freeze({");
const configEnd = workerSource.indexOf("\n\n});", configStart);
if (configStart < 0 || configEnd < 0) {
  throw new Error("SITE_CONFIG block was not found in cloudflare/worker.js");
}

const configSource = workerSource.slice(configStart, configEnd);
const entryPattern = /(?:^|\n)\s{2}(?:"([^"]+)"|([a-z][a-z0-9-]*)): Object\.freeze\(\{[\s\S]*?color: Object\.freeze\(\[(\d+),\s*(\d+),\s*(\d+)\]\)[\s\S]*?domains: Object\.freeze\(\[("[^"]+"(?:,\s*"[^"]+")*)\]\)[\s\S]*?\n\s{2}\}\)(?:,|$)/g;

const entries = [];
for (const match of configSource.matchAll(entryPattern)) {
  entries.push({
    site: match[1] || match[2],
    color: [Number(match[3]), Number(match[4]), Number(match[5])],
  });
}

const expectedSites = new Set([
  "nautical-dream",
  "lokwod",
  "life-in-the-simulation",
  "beautiful-mens-club",
  "mr-adventure-dad",
  "syracuse-appraiser",
  "accurate-re-appraisals",
  "accurate-re-appraisals-org",
  "dish-gal",
  "blappos",
  "big-bud-man",
  "the-crypto-appraiser",
]);

const actualSites = new Set(entries.map((entry) => entry.site));
const missingSites = [...expectedSites].filter((site) => !actualSites.has(site));
const unexpectedSites = [...actualSites].filter((site) => !expectedSites.has(site));
if (missingSites.length || unexpectedSites.length) {
  throw new Error(`Site map mismatch. Missing: ${missingSites.join(", ") || "none"}; unexpected: ${unexpectedSites.join(", ") || "none"}`);
}

const exactColors = new Map();
for (const entry of entries) {
  const key = entry.color.join(",");
  if (exactColors.has(key)) {
    throw new Error(`Duplicate RGB color ${key}: ${exactColors.get(key)} and ${entry.site}`);
  }
  exactColors.set(key, entry.site);
}

const minimumDistance = 70;
for (let leftIndex = 0; leftIndex < entries.length; leftIndex += 1) {
  for (let rightIndex = leftIndex + 1; rightIndex < entries.length; rightIndex += 1) {
    const left = entries[leftIndex];
    const right = entries[rightIndex];
    const distance = Math.hypot(
      left.color[0] - right.color[0],
      left.color[1] - right.color[1],
      left.color[2] - right.color[2],
    );
    if (distance < minimumDistance) {
      throw new Error(`Colors too close (${distance.toFixed(1)}): ${left.site} and ${right.site}`);
    }
  }
}

console.log(`Validated ${entries.length} distinct site colors; minimum RGB distance is at least ${minimumDistance}.`);

