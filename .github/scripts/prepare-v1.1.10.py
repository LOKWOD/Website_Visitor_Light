from pathlib import Path


path = Path("firmware/src/main.cpp")
source = path.read_text(encoding="utf-8")


def replace_once(old: str, new: str, label: str) -> None:
    global source
    if old not in source:
        raise SystemExit(f"Could not find {label}.")
    source = source.replace(old, new, 1)


replace_once(
    'constexpr char kFirmwareVersion[] = "1.1.9";',
    'constexpr char kFirmwareVersion[] = "1.1.10";',
    "v1.1.9 firmware version marker",
)

replace_once(
    'String lastEventLabel = "None yet";\n',
    'String lastEventLabel = "None yet";\nString lastEventKind = "visit";\n',
    "latest event globals",
)

replace_once(
    '    const String label = event["label"] | "Website visitor";\n'
    '    const String path = event["path"] | "/";\n',
    '    const String label = event["label"] | "Website visitor";\n'
    '    const String kind = event["kind"] | "visit";\n'
    '    const String path = event["path"] | "/";\n',
    "Worker event label/path parsing",
)

replace_once(
    '    enqueueColor(red, green, blue, 3, label, path);\n'
    '    lastEventLabel = label;\n',
    '    const uint8_t pulses = kind == "affiliate_click" ? 6 : 3;\n'
    '    enqueueColor(red, green, blue, pulses, label, path);\n'
    '    lastEventLabel = label;\n'
    '    lastEventKind = kind;\n',
    "event flash and latest-event assignment",
)

old_card = '<section class="card"><h2>Latest visitor</h2><div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:12px"><button type="button" id="sound_toggle" onclick="toggleSound()">Enable cash-register sound</button><span class="muted" id="sound_state">Off &middot; appraisal sites only</span></div><div class="visitor-id" id="visitor_id">Waiting for a visitor</div><div class="muted" style="margin-top:10px">Approximate area</div><div class="visitor-location" id="visitor_location">Waiting for location data</div><div class="row"><span>Country</span><span class="value" id="visitor_country_name">--</span></div><div class="row"><span>Site</span><span class="value" id="visitor_site">--</span></div><div class="row"><span>Page</span><span class="value visitor-page" id="visitor_path">--</span></div><div class="row"><span>Title</span><span class="value visitor-page" id="visitor_title">--</span></div><div class="row"><span>When</span><span class="value" id="visitor_time">--</span></div><p class="muted" style="margin:12px 0 0">Location is approximate and derived from network geography. Full visitor IP addresses are not displayed on the dashboard.</p></section></div></main>'
new_card = '<section class="card"><h2>Latest event</h2><div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:12px"><button type="button" id="sound_toggle" onclick="toggleSound()">Enable cash-register sound</button><span class="muted" id="sound_state">Off &middot; affiliate clicks only</span></div><div class="visitor-id" id="visitor_id">Waiting for an event</div><div class="muted" style="margin-top:10px">Approximate area</div><div class="visitor-location" id="visitor_location">Waiting for location data</div><div class="row"><span>Country</span><span class="value" id="visitor_country_name">--</span></div><div class="row"><span>Type</span><span class="value" id="visitor_kind">--</span></div><div class="row"><span>Site</span><span class="value" id="visitor_site">--</span></div><div class="row"><span>Page</span><span class="value visitor-page" id="visitor_path">--</span></div><div class="row"><span>Title / link</span><span class="value visitor-page" id="visitor_title">--</span></div><div class="row"><span>When</span><span class="value" id="visitor_time">--</span></div><p class="muted" style="margin:12px 0 0">The sound plays only in this private dashboard after you enable it. Public website visitors never hear it.</p></section></div></main>'
replace_once(old_card, new_card, "latest event dashboard card")

replace_once(
    "const appraisalSite=(name)=>/syracuse appraiser|accurate/i.test(name||'');",
    "const affiliateEvent=(kind)=>kind==='affiliate_click';",
    "dashboard sound event selector",
)

replace_once(
    "function updateSoundUi(){const b=document.getElementById('sound_toggle');b.textContent=soundEnabled?'Mute cash-register sound':'Enable cash-register sound';text('sound_state',(soundEnabled?'On':'Off')+' · appraisal sites only')}",
    "function updateSoundUi(){const b=document.getElementById('sound_toggle');b.textContent=soundEnabled?'Mute cash-register sound':'Enable cash-register sound';text('sound_state',(soundEnabled?'On':'Off')+' · affiliate clicks only')}",
    "dashboard sound status text",
)

replace_once(
    "soundEnabled=!soundEnabled;localStorage.setItem('lokwodAppraisalSound',soundEnabled?'1':'0');updateSoundUi();",
    "soundEnabled=!soundEnabled;localStorage.setItem('lokwodAffiliateSound',soundEnabled?'1':'0');updateSoundUi();",
    "dashboard sound preference key",
)

replace_once(
    "text('visitor_country_name',countryName(s.visitor_country)||'--');text('visitor_site',s.visitor_site||'--');",
    "text('visitor_country_name',countryName(s.visitor_country)||'--');text('visitor_kind',affiliateEvent(s.visitor_kind)?'Affiliate click':'Website visit');text('visitor_site',s.visitor_site||'--');",
    "dashboard event type rendering",
)

replace_once(
    "if(statusInitialized&&ts&&ts!==lastSeenEventTs&&appraisalSite(s.visitor_site))cashRegister();",
    "if(statusInitialized&&ts&&ts!==lastSeenEventTs&&affiliateEvent(s.visitor_kind))cashRegister();",
    "dashboard affiliate sound trigger",
)

replace_once(
    '  document["visitor_site"] = lastEventLabel == "None yet" ? "" : lastEventLabel;\n',
    '  document["visitor_site"] = lastEventLabel == "None yet" ? "" : lastEventLabel;\n'
    '  document["visitor_kind"] = lastEventKind;\n',
    "dashboard status event kind",
)

required = [
    'constexpr char kFirmwareVersion[] = "1.1.10";',
    'String lastEventKind = "visit";',
    'kind == "affiliate_click" ? 6 : 3',
    'id="visitor_kind"',
    "affiliateEvent(s.visitor_kind)",
    "affiliate clicks only",
    "Public website visitors never hear it.",
]
for token in required:
    if token not in source:
        raise SystemExit(f"Missing required v1.1.10 token: {token}")

if "appraisalSite" in source or "lokwodAppraisalSound" in source:
    raise SystemExit("Legacy appraisal-only dashboard sound logic remains.")

path.write_text(source, encoding="utf-8")
print("Prepared v1.1.10 with private affiliate-click sound and six-pulse money flashes.")
