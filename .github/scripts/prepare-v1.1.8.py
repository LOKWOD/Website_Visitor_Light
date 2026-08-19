from pathlib import Path

path = Path('firmware/src/main.cpp')
source = path.read_text(encoding='utf-8')


def replace_once(old: str, new: str, label: str) -> None:
    global source
    if old not in source:
        raise SystemExit(f'Could not find {label}.')
    source = source.replace(old, new, 1)


replace_once(
    'constexpr char kFirmwareVersion[] = "1.1.7";',
    'constexpr char kFirmwareVersion[] = "1.1.8";',
    'v1.1.7 firmware version marker',
)

replace_once(
    'constexpr uint32_t kPollIntervalMs = 3000;\n',
    'constexpr uint32_t kPollIntervalMs = 3000;\n'
    'constexpr uint32_t kHeartbeatIntervalMs = 60UL * 1000UL;\n',
    'poll interval constant',
)

replace_once(
    'uint32_t lastSuccessfulPollMs = 0;\n',
    'uint32_t lastSuccessfulPollMs = 0;\n'
    'uint32_t lastHeartbeatAttemptMs = 0;\n'
    'uint32_t lastHeartbeatSuccessMs = 0;\n'
    'int lastHeartbeatHttpStatus = 0;\n'
    'String heartbeatStatus = "Waiting for first heartbeat.";\n',
    'poll status globals',
)

heartbeat_function = r'''
void sendHeartbeat() {
  lastHeartbeatAttemptMs = millis();

  if (WiFi.status() != WL_CONNECTED) {
    heartbeatStatus = "Heartbeat waiting for Wi-Fi.";
    return;
  }
  if (!systemTimeIsValid()) {
    heartbeatStatus = "Heartbeat waiting for network time.";
    return;
  }
  if (workerBaseUrl.length() == 0 || deviceToken.length() < 32) {
    heartbeatStatus = "Heartbeat waiting for Worker configuration.";
    return;
  }

  LokwodSecureClient secureClient;
  HTTPClient http;
  if (!beginSecureRequest(secureClient, http, workerBaseUrl + "/v1/heartbeat")) {
    heartbeatStatus = "Heartbeat HTTPS request could not initialize.";
    return;
  }
  http.addHeader("Content-Type", "application/json");
  http.addHeader("Cache-Control", "no-cache");

  JsonDocument document;
  document["device"] = "esp32-s3-visitor-light";
  document["firmware"] = AppConfig::kFirmwareVersion;
  document["uptime_ms"] = static_cast<uint64_t>(millis());
  document["rssi"] = WiFi.RSSI();
  document["last_poll_age_ms"] = workerEverConnected ? millis() - lastSuccessfulPollMs : 0;
  document["today_visits"] = todayAcceptedVisits;
  document["free_heap"] = ESP.getFreeHeap();

  String body;
  serializeJson(document, body);
  lastHeartbeatHttpStatus = http.POST(body);

  if (lastHeartbeatHttpStatus >= 200 && lastHeartbeatHttpStatus < 300) {
    lastHeartbeatSuccessMs = millis();
    heartbeatStatus = "Heartbeat acknowledged by Worker.";
  } else if (lastHeartbeatHttpStatus == HTTP_CODE_NOT_FOUND) {
    heartbeatStatus = "Worker heartbeat endpoint not installed yet (HTTP 404).";
  } else {
    heartbeatStatus = "Heartbeat failed with HTTP " + String(lastHeartbeatHttpStatus) + ".";
  }
  http.end();
}
'''

marker = '\nbool triggerCloudTest(const String &siteId) {'
if marker not in source:
    raise SystemExit('Could not find cloud-test function marker.')
source = source.replace(marker, '\n' + heartbeat_function + marker, 1)

replace_once(
    '<div class="row"><span>Worker</span><span class="value" id="worker">--</span></div><div class="row"><span>Last event</span>',
    '<div class="row"><span>Worker</span><span class="value" id="worker">--</span></div><div class="row"><span>Heartbeat</span><span class="value" id="heartbeat">--</span></div><div class="row"><span>Last event</span>',
    'dashboard Worker row',
)

replace_once(
    "text('worker',s.worker_connected?'Connected':'Not connected');text('last',",
    "text('worker',s.worker_connected?'Connected':'Not connected');const hbAge=Number(s.heartbeat_last_success_age_ms||0);text('heartbeat',s.heartbeat_ok?('OK · '+Math.round(hbAge/1000)+'s ago'):(s.heartbeat_status||'Waiting'));text('last',",
    'dashboard refresh Worker status',
)

replace_once(
    '  document["last_poll_age_ms"] = workerEverConnected ? millis() - lastSuccessfulPollMs : 0;\n',
    '  document["last_poll_age_ms"] = workerEverConnected ? millis() - lastSuccessfulPollMs : 0;\n'
    '  document["heartbeat_ok"] = lastHeartbeatSuccessMs != 0 &&\n'
    '      (millis() - lastHeartbeatSuccessMs < AppConfig::kHeartbeatIntervalMs * 3UL);\n'
    '  document["heartbeat_last_success_age_ms"] = lastHeartbeatSuccessMs == 0 ? 0 : millis() - lastHeartbeatSuccessMs;\n'
    '  document["heartbeat_http_status"] = lastHeartbeatHttpStatus;\n'
    '  document["heartbeat_status"] = heartbeatStatus;\n',
    'status API poll-age field',
)

replace_once(
    '  lastPollAttemptMs = millis() - AppConfig::kPollIntervalMs;\n'
    '  nextAutoUpdateCheckMs = millis() + AppConfig::kAutoUpdateFirstCheckMs;\n',
    '  lastPollAttemptMs = millis() - AppConfig::kPollIntervalMs;\n'
    '  lastHeartbeatAttemptMs = millis();\n'
    '  nextAutoUpdateCheckMs = millis() + AppConfig::kAutoUpdateFirstCheckMs;\n',
    'setup poll/update initialization',
)

replace_once(
    '  serviceTime();\n'
    '  serviceAutomaticUpdate();\n',
    '  serviceTime();\n'
    '  if (!otaInProgress && WiFi.status() == WL_CONNECTED &&\n'
    '      millis() - lastHeartbeatAttemptMs >= AppConfig::kHeartbeatIntervalMs) {\n'
    '    sendHeartbeat();\n'
    '  }\n'
    '  serviceAutomaticUpdate();\n',
    'main-loop service ordering',
)

required = [
    'constexpr char kFirmwareVersion[] = "1.1.8";',
    'kHeartbeatIntervalMs',
    'void sendHeartbeat()',
    '/v1/heartbeat',
    'heartbeat_last_success_age_ms',
    'id="heartbeat"',
]
for token in required:
    if token not in source:
        raise SystemExit(f'Missing required v1.1.8 heartbeat token: {token}')

path.write_text(source, encoding='utf-8')
print('Prepared v1.1.8 with non-fatal Worker heartbeat reporting.')
