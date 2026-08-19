from pathlib import Path

path = Path('firmware/src/main.cpp')
source = path.read_text(encoding='utf-8')

old_version = 'constexpr char kFirmwareVersion[] = "1.1.4";'
new_version = 'constexpr char kFirmwareVersion[] = "1.1.7";'
if old_version not in source:
    raise SystemExit('Expected v1.1.4 source after compatibility preparation.')
source = source.replace(old_version, new_version, 1)

old_colors = '<section class="card"><h2>Site colors</h2><div class="legend"><div><i class="dot" style="background:rgb(0,185,255)"></i>Nautical Dream</div><div><i class="dot" style="background:rgb(0,82,255)"></i>LOKWOD</div><div><i class="dot" style="background:rgb(0,235,95)"></i>Life Simulation</div><div><i class="dot" style="background:rgb(180,35,255)"></i>Beautiful Men\'s Club</div><div><i class="dot" style="background:rgb(255,96,0)"></i>Mr Adventure Dad</div><div><i class="dot" style="background:rgb(255,174,0)"></i>Syracuse Appraiser</div><div><i class="dot" style="background:rgb(235,235,235);border:1px solid #aaa"></i>Accurate Appraisals</div></div></section>'
new_colors = '<section class="card"><h2>Site colors</h2><div class="legend"><div><i class="dot" style="background:rgb(0,185,255)"></i>Nautical Dream</div><div><i class="dot" style="background:rgb(0,82,255)"></i>LOKWOD</div><div><i class="dot" style="background:rgb(0,235,95)"></i>Life Simulation</div><div><i class="dot" style="background:rgb(180,35,255)"></i>Beautiful Men\'s Club</div><div><i class="dot" style="background:rgb(255,96,0)"></i>Mr Adventure Dad</div><div><i class="dot" style="background:rgb(255,174,0)"></i>Syracuse Appraiser</div><div><i class="dot" style="background:rgb(235,235,235);border:1px solid #aaa"></i>Accurate Appraisals</div><div><i class="dot" style="background:transparent;border:1px dashed #9aa7b7"></i>Dish Gal</div></div></section>'
if old_colors in source:
    source = source.replace(old_colors, new_colors, 1)
elif 'Dish Gal' not in source:
    raise SystemExit('Could not find Site colors block.')

latest_api = 'constexpr char kLatestReleaseApi[] = "https://api.github.com/repos/LOKWOD/Website_Visitor_Light/releases/latest";\n'
if latest_api in source:
    source = source.replace(latest_api, '', 1)

function_start = source.index('bool fetchLatestFirmwareRelease(String &version, String &downloadUrl) {')
function_end = source.index('\nbool installFirmwareUpdate(', function_start)
worker_manifest_function = '''bool fetchLatestFirmwareRelease(String &version, String &downloadUrl) {
  autoUpdateStatus = "Checking Cloudflare for firmware updates...";

  if (workerBaseUrl.length() == 0 || deviceToken.length() < 32) {
    autoUpdateStatus = "Update service unavailable until Worker configuration is ready.";
    return false;
  }

  LokwodSecureClient secureClient;
  HTTPClient http;
  if (!beginSecureRequest(secureClient, http, workerBaseUrl + "/v1/firmware/latest")) {
    autoUpdateStatus = "Update check could not initialize Worker HTTPS.";
    return false;
  }
  http.addHeader("Cache-Control", "no-cache");

  const int status = http.GET();
  if (status == HTTP_CODE_NOT_FOUND) {
    http.end();
    autoUpdateStatus = "No published firmware release yet.";
    return true;
  }
  if (status != HTTP_CODE_OK) {
    autoUpdateStatus = "Firmware update service failed with HTTP " + String(status) + ".";
    http.end();
    return false;
  }

  const String payload = http.getString();
  http.end();

  JsonDocument document;
  const DeserializationError error = deserializeJson(document, payload);
  if (error) {
    autoUpdateStatus = String("Firmware manifest JSON error: ") + error.c_str();
    return false;
  }
  if (!document["ok"].as<bool>()) {
    autoUpdateStatus = "Firmware update service returned an unsuccessful manifest.";
    return false;
  }

  version = normalizedVersion(String(document["version"] | ""));
  downloadUrl = String(document["downloadUrl"] | "");
  int major = 0, minor = 0, patch = 0;
  if (!parseVersion(version, major, minor, patch)) {
    autoUpdateStatus = "Update service returned an invalid firmware version.";
    return false;
  }
  if (!downloadUrl.startsWith(workerBaseUrl + "/v1/firmware/")) {
    autoUpdateStatus = "Update service returned an invalid firmware download URL.";
    return false;
  }
  return true;
}
'''
source = source[:function_start] + worker_manifest_function + source[function_end:]

old_callback = '''        request->setUserAgent(String("LOKWOD-Visitor-Light/") + AppConfig::kFirmwareVersion);\n        request->addHeader("Accept", "application/octet-stream");'''
new_callback = '''        request->setUserAgent(String("LOKWOD-Visitor-Light/") + AppConfig::kFirmwareVersion);\n        request->addHeader("Authorization", "Bearer " + deviceToken);\n        request->addHeader("X-LOKWOD-Device", "esp32-s3-visitor-light");\n        request->addHeader("Accept", "application/octet-stream");'''
if old_callback in source:
    source = source.replace(old_callback, new_callback, 1)
elif 'request->addHeader("Authorization", "Bearer " + deviceToken);' not in source:
    raise SystemExit('Firmware download authorization callback was not found.')

old_maintenance = 'Automatic GitHub firmware updates check after boot and every six hours.'
new_maintenance = 'Automatic firmware updates check through the connected Cloudflare Worker after boot and every six hours.'
if old_maintenance in source:
    source = source.replace(old_maintenance, new_maintenance, 1)

old_latest = '<section class="card"><h2>Latest visitor</h2><div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:12px"><button type="button" id="sound_toggle" onclick="toggleSound()">Enable cash-register sound</button><span class="muted" id="sound_state">Off &middot; appraisal sites only</span></div><div class="visitor-id" id="visitor_id">Waiting for a visitor</div><div class="visitor-location" id="visitor_location">--</div><div class="row"><span>Site</span><span class="value" id="visitor_site">--</span></div><div class="row"><span>Page</span><span class="value visitor-page" id="visitor_path">--</span></div><div class="row"><span>Title</span><span class="value visitor-page" id="visitor_title">--</span></div><div class="row"><span>When</span><span class="value" id="visitor_time">--</span></div><p class="muted" style="margin:12px 0 0">Visitor IDs are anonymous and location is approximate. Raw visitor IP addresses are not stored or shown.</p></section></div></main>'
new_latest = '<section class="card"><h2>Latest visitor</h2><div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:12px"><button type="button" id="sound_toggle" onclick="toggleSound()">Enable cash-register sound</button><span class="muted" id="sound_state">Off &middot; appraisal sites only</span></div><div class="visitor-id" id="visitor_id">Waiting for a visitor</div><div class="muted" style="margin-top:10px">Approximate area</div><div class="visitor-location" id="visitor_location">Waiting for location data</div><div class="row"><span>Country</span><span class="value" id="visitor_country_name">--</span></div><div class="row"><span>Site</span><span class="value" id="visitor_site">--</span></div><div class="row"><span>Page</span><span class="value visitor-page" id="visitor_path">--</span></div><div class="row"><span>Title</span><span class="value visitor-page" id="visitor_title">--</span></div><div class="row"><span>When</span><span class="value" id="visitor_time">--</span></div><p class="muted" style="margin:12px 0 0">Location is approximate and derived from network geography. Full visitor IP addresses are not displayed on the dashboard.</p></section></div></main>'
if old_latest in source:
    source = source.replace(old_latest, new_latest, 1)
elif 'id="visitor_country_name"' not in source:
    raise SystemExit('Latest visitor card was not found.')

old_js = "const appraisalSite=(name)=>/syracuse appraiser|accurate/i.test(name||'');"
new_js = """const appraisalSite=(name)=>/syracuse appraiser|accurate/i.test(name||'');
const countryName=(code)=>{
  const c=String(code||'').toUpperCase();
  if(!c)return '';
  try{
    if(typeof Intl!=='undefined'&&Intl.DisplayNames){
      return new Intl.DisplayNames([navigator.language||'en'],{type:'region'}).of(c)||c;
    }
  }catch(e){}
  return c;
};
const visitorArea=(s)=>{
  const parts=[s.visitor_city,s.visitor_region].filter(Boolean);
  const country=countryName(s.visitor_country);
  if(country)parts.push(country);
  return parts.join(', ')||'Location unavailable';
};"""
if old_js in source:
    source = source.replace(old_js, new_js, 1)
else:
    malformed_start = source.find("const appraisalSite=(name)=>/syracuse appraiser|accurate/i.test(name||'');\\\\nconst countryName=")
    if malformed_start >= 0:
        malformed_end = source.find('function cashRegister()', malformed_start)
        if malformed_end < 0:
            raise SystemExit('Could not locate end of malformed dashboard helper block.')
        source = source[:malformed_start] + new_js + '\n' + source[malformed_end:]
    elif 'const countryName=(code)=>{' not in source:
        raise SystemExit('Dashboard JavaScript marker was not found.')

old_refresh = "text('visitor_id',s.visitor_id?('Visitor '+s.visitor_id):'Waiting for a visitor');const loc=[s.visitor_city,s.visitor_region,s.visitor_country].filter(Boolean).join(', ');text('visitor_location',loc||'Location unavailable');text('visitor_site',s.visitor_site||'--');"
new_refresh = "text('visitor_id',s.visitor_id?('Visitor '+s.visitor_id):'Waiting for a visitor');text('visitor_location',visitorArea(s));text('visitor_country_name',countryName(s.visitor_country)||'--');text('visitor_site',s.visitor_site||'--');"
if old_refresh in source:
    source = source.replace(old_refresh, new_refresh, 1)
elif new_refresh not in source:
    raise SystemExit('Latest visitor refresh code was not found.')

if '\\\\nconst countryName' in source:
    raise SystemExit('Literal backslash-n remains in dashboard JavaScript.')
if 'api.github.com/repos/LOKWOD/Website_Visitor_Light/releases/latest' in source:
    raise SystemExit('Direct GitHub updater unexpectedly remains in firmware source.')

path.write_text(source, encoding='utf-8')
print('Prepared v1.1.7 dashboard recovery firmware.')
