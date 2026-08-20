from pathlib import Path
import math
import re

path = Path("firmware/src/main.cpp")
source = path.read_text(encoding="utf-8")


def replace_once(old: str, new: str, label: str) -> None:
    global source
    if old not in source:
        raise SystemExit(f"Could not find {label}.")
    source = source.replace(old, new, 1)


security_route = r'''  webServer.on("/api/security", HTTP_POST, []() {
    if (!requireDashboardAuthorization()) return;
    const String newDashboardPassword =
        webServer.hasArg("dashboard_password") ? webServer.arg("dashboard_password") : "";
    const String newSetupPassword =
        webServer.hasArg("setup_password") ? webServer.arg("setup_password") : "";
    const String newOtaPassword =
        webServer.hasArg("ota_password") ? webServer.arg("ota_password") : "";

    String message;
    if (!saveLocalPasswords(newDashboardPassword, newSetupPassword, newOtaPassword, message)) {
      String errorPage =
          "<!doctype html><meta name=viewport content='width=device-width,initial-scale=1'>"
          "<title>Password settings</title><body style='font-family:Arial;padding:24px'>"
          "<h2>Password settings were not changed</h2><p>";
      errorPage += htmlEscape(message);
      errorPage += "</p><p><a href='/'>Return to dashboard</a></p></body>";
      webServer.send(400, "text/html; charset=utf-8", errorPage);
      return;
    }

    webServer.send(200, "text/html; charset=utf-8",
                   "<!doctype html><meta name=viewport content='width=device-width,initial-scale=1'>"
                   "<title>Passwords saved</title><body style='font-family:Arial;padding:24px'>"
                   "<h2>Passwords saved</h2><p>They are stored on the ESP32 and will survive future firmware updates.</p>"
                   "<p>The Visitor Light is restarting now. If you changed the dashboard password, sign in again as <strong>admin</strong> using the new password.</p></body>");
    delay(900);
    ESP.restart();
  });
'''

if 'webServer.on("/api/security", HTTP_POST' not in source:
    marker = '  webServer.on("/api/check-update", HTTP_POST, []() {'
    if marker not in source:
        raise SystemExit("Could not find the dashboard update-check route marker.")
    source = source.replace(marker, security_route + marker, 1)

replace_once(
    'constexpr char kFirmwareVersion[] = "1.1.8";',
    'constexpr char kFirmwareVersion[] = "1.1.9";',
    "v1.1.8 firmware version marker",
)

old_select = '<form method="post" action="/api/cloud-test"><label for="site">Cloud test color</label><select id="site" name="site"><option value="nautical-dream">Nautical Dream &mdash; aqua</option><option value="lokwod">LOKWOD &mdash; blue</option><option value="life-in-the-simulation">Life in the Simulation &mdash; green</option><option value="beautiful-mens-club">Beautiful Men\'s Club &mdash; purple</option><option value="mr-adventure-dad">Mr Adventure Dad &mdash; orange</option><option value="syracuse-appraiser">Syracuse Appraiser &mdash; amber</option><option value="accurate-re-appraisals">Accurate RE Appraisals &mdash; white</option></select><button class="secondary" type="submit">Run cloud test</button></form>'
new_select = '<form method="post" action="/api/cloud-test"><label for="site">Cloud test color</label><select id="site" name="site"><option value="nautical-dream">Nautical Dream &mdash; aqua</option><option value="lokwod">LOKWOD &mdash; blue</option><option value="life-in-the-simulation">Life in the Simulation &mdash; green</option><option value="beautiful-mens-club">Beautiful Men\'s Club &mdash; purple</option><option value="mr-adventure-dad">Mr Adventure Dad &mdash; orange</option><option value="syracuse-appraiser">Syracuse Appraiser &mdash; amber</option><option value="accurate-re-appraisals">Accurate RE Appraisals (.com) &mdash; white</option><option value="accurate-re-appraisals-org">Accurate RE Appraisals (.org) &mdash; red</option><option value="dish-gal">Dish Gal &mdash; hot pink</option><option value="blappos">Blappos &mdash; teal</option><option value="big-bud-man">Big Bud Man &mdash; lime</option><option value="the-crypto-appraiser">The Crypto Appraiser &mdash; indigo</option></select><button class="secondary" type="submit">Run cloud test</button></form>'
replace_once(old_select, new_select, "dashboard cloud-test site menu")

old_colors = '<section class="card"><h2>Site colors</h2><div class="legend"><div><i class="dot" style="background:rgb(0,185,255)"></i>Nautical Dream</div><div><i class="dot" style="background:rgb(0,82,255)"></i>LOKWOD</div><div><i class="dot" style="background:rgb(0,235,95)"></i>Life Simulation</div><div><i class="dot" style="background:rgb(180,35,255)"></i>Beautiful Men\'s Club</div><div><i class="dot" style="background:rgb(255,96,0)"></i>Mr Adventure Dad</div><div><i class="dot" style="background:rgb(255,174,0)"></i>Syracuse Appraiser</div><div><i class="dot" style="background:rgb(235,235,235);border:1px solid #aaa"></i>Accurate Appraisals</div><div><i class="dot" style="background:transparent;border:1px dashed #9aa7b7"></i>Dish Gal</div></div></section>'
new_colors = '<section class="card"><h2>Site colors</h2><div class="legend"><div><i class="dot" style="background:rgb(0,185,255)"></i>Nautical Dream</div><div><i class="dot" style="background:rgb(0,82,255)"></i>LOKWOD</div><div><i class="dot" style="background:rgb(0,235,95)"></i>Life in the Simulation</div><div><i class="dot" style="background:rgb(180,35,255)"></i>Beautiful Men\'s Club</div><div><i class="dot" style="background:rgb(255,96,0)"></i>Mr Adventure Dad</div><div><i class="dot" style="background:rgb(255,174,0)"></i>Syracuse Appraiser</div><div><i class="dot" style="background:rgb(235,235,235);border:1px solid #aaa"></i>Accurate RE (.com)</div><div><i class="dot" style="background:rgb(255,0,0)"></i>Accurate RE (.org)</div><div><i class="dot" style="background:rgb(255,0,140)"></i>Dish Gal</div><div><i class="dot" style="background:rgb(0,255,180)"></i>Blappos</div><div><i class="dot" style="background:rgb(170,255,0)"></i>Big Bud Man</div><div><i class="dot" style="background:rgb(75,0,255)"></i>The Crypto Appraiser</div></div></section>'
replace_once(old_colors, new_colors, "v1.1.7 Site colors legend")

required_security_tokens = [
    "String dashboardSessionToken;",
    "bool saveLocalPasswords(",
    "String loginPage(",
    "bool dashboardSessionIsValid()",
    'webServer.on("/login", HTTP_POST',
    'webServer.on("/logout", HTTP_GET',
    'webServer.on("/api/security", HTTP_POST',
]
for token in required_security_tokens:
    if token not in source:
        raise SystemExit(f"Missing required saveable-password/login token: {token}")

required_site_ids = [
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
]
for site_id in required_site_ids:
    option = f'<option value="{site_id}">'
    if option not in source:
        raise SystemExit(f"Missing cloud-test option: {site_id}")

legend_start = source.index('<section class="card"><h2>Site colors</h2>')
legend_end = source.index("</section>", legend_start)
legend = source[legend_start:legend_end]
colors = [
    tuple(map(int, match))
    for match in re.findall(r"background:rgb\((\d+),(\d+),(\d+)\)", legend)
]
if len(colors) != 12 or len(set(colors)) != 12:
    raise SystemExit(f"Expected 12 unique dashboard RGB colors; found {len(colors)} colors and {len(set(colors))} unique values.")

minimum_distance = min(
    math.dist(colors[left], colors[right])
    for left in range(len(colors))
    for right in range(left + 1, len(colors))
)
if minimum_distance < 70:
    raise SystemExit(f"Dashboard colors are too close; minimum RGB distance is {minimum_distance:.1f}.")

required_v119_tokens = [
    'constexpr char kFirmwareVersion[] = "1.1.9";',
    'id="heartbeat"',
    "heartbeat_last_success_age_ms",
    "Dish Gal &mdash; hot pink",
    "The Crypto Appraiser &mdash; indigo",
]
for token in required_v119_tokens:
    if token not in source:
        raise SystemExit(f"Missing required v1.1.9 token: {token}")

path.write_text(source, encoding="utf-8")
print(f"Prepared v1.1.9 with 12 distinct dashboard site colors (minimum RGB distance {minimum_distance:.1f}).")

