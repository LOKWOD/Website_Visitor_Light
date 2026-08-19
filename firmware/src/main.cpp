#include <Arduino.h>
#include <Adafruit_NeoPixel.h>
#include <ArduinoJson.h>
#include <ArduinoOTA.h>
#include <ESPmDNS.h>
#include <HTTPClient.h>
#include <HTTPUpdate.h>
#if __has_include(<NetworkClientSecure.h>)
#include <NetworkClientSecure.h>
using LokwodSecureClient = NetworkClientSecure;
#else
#include <WiFiClientSecure.h>
using LokwodSecureClient = WiFiClientSecure;
#endif
#include <Preferences.h>
#include <WebServer.h>
#include <WiFi.h>
#include <WiFiManager.h>
#include <time.h>
#include <esp_system.h>

#include "secrets.h"
#include "trusted_roots.h"

namespace AppConfig {
constexpr char kFirmwareVersion[] = "1.1.3";
constexpr char kHostname[] = "lokwod-visitor-light";
constexpr char kSetupAccessPoint[] = "LOKWOD-Visitor-Light";
constexpr uint8_t kRgbPin = 48;
constexpr uint8_t kRgbCount = 1;
constexpr uint8_t kBrightness = 70;
constexpr uint32_t kPollIntervalMs = 3000;
constexpr uint32_t kReconnectAttemptMs = 5000;
constexpr uint32_t kReconnectRestartMs = 90000;
constexpr uint32_t kConfigPortalTimeoutSeconds = 120;
constexpr uint8_t kConnectRetries = 3;
constexpr uint32_t kNtpRetryMs = 15000;
constexpr uint32_t kHttpTimeoutMs = 8000;
constexpr char kLatestReleaseApi[] = "https://api.github.com/repos/LOKWOD/Website_Visitor_Light/releases/latest";
constexpr char kFirmwareAssetName[] = "LOKWOD_Visitor_Light.bin";
constexpr uint32_t kAutoUpdateFirstCheckMs = 45000;
constexpr uint32_t kAutoUpdateIntervalMs = 6UL * 60UL * 60UL * 1000UL;
constexpr uint32_t kAutoUpdateRetryMs = 15UL * 60UL * 1000UL;
constexpr uint32_t kFirmwareDownloadTimeoutMs = 30000;
constexpr size_t kFlashQueueCapacity = 16;
constexpr uint16_t kFlashOnMs = 210;
constexpr uint16_t kFlashOffMs = 130;
}  // namespace AppConfig

struct FlashJob {
  uint8_t red = 0;
  uint8_t green = 0;
  uint8_t blue = 0;
  uint8_t pulses = 3;
  String label;
  String path;
};

Adafruit_NeoPixel rgbLed(AppConfig::kRgbCount, AppConfig::kRgbPin, NEO_GRB + NEO_KHZ800);
Preferences preferences;
WebServer webServer(80);

String workerBaseUrl;
String deviceToken;
String dashboardPassword;
String setupApPassword;
String otaPassword;
uint64_t eventCursor = 0;
bool cursorInitialized = false;

FlashJob flashQueue[AppConfig::kFlashQueueCapacity];
size_t flashQueueHead = 0;
size_t flashQueueTail = 0;
size_t flashQueueCount = 0;
FlashJob currentFlash;
bool flashActive = false;
bool flashLedOn = false;
uint8_t flashPulsesRemaining = 0;
uint32_t flashDeadlineMs = 0;

uint32_t lastPollAttemptMs = 0;
uint32_t lastSuccessfulPollMs = 0;
uint32_t disconnectedSinceMs = 0;
uint32_t lastReconnectAttemptMs = 0;
uint32_t reconnectAttemptCount = 0;
uint32_t lastNtpAttemptMs = 0;
uint32_t todayAcceptedVisits = 0;
int lastHttpStatus = 0;
unsigned consecutivePollFailures = 0;
String lastError;
String lastEventLabel = "None yet";
String lastEventPath = "";
String lastEventTitle = "";
String lastEventVisitorId = "";
String lastEventCity = "";
String lastEventRegion = "";
String lastEventCountry = "";
uint64_t lastEventTimestamp = 0;
bool workerEverConnected = false;
bool otaInProgress = false;
uint32_t nextAutoUpdateCheckMs = 0;
uint32_t lastAutoUpdateCheckMs = 0;
bool autoUpdateCheckRequested = false;
String latestFirmwareVersion;
String autoUpdateStatus = "Waiting for first automatic update check.";

bool timeReached(uint32_t deadline) {
  return static_cast<int32_t>(millis() - deadline) >= 0;
}

void setLed(uint8_t red, uint8_t green, uint8_t blue) {
  rgbLed.setPixelColor(0, rgbLed.Color(red, green, blue));
  rgbLed.show();
}

void turnLedOff() {
  setLed(0, 0, 0);
}

String normalizeWorkerUrl(String value) {
  value.trim();
  while (value.endsWith("/")) value.remove(value.length() - 1);
  if (!value.startsWith("https://")) return "";
  return value;
}

String htmlEscape(const String &input) {
  String output;
  output.reserve(input.length() + 16);
  for (size_t index = 0; index < input.length(); ++index) {
    switch (input[index]) {
      case '&': output += F("&amp;"); break;
      case '<': output += F("&lt;"); break;
      case '>': output += F("&gt;"); break;
      case '"': output += F("&quot;"); break;
      case '\'': output += F("&#39;"); break;
      default: output += input[index]; break;
    }
  }
  return output;
}

String formatUptime() {
  const uint64_t seconds = millis() / 1000ULL;
  const uint32_t days = seconds / 86400ULL;
  const uint8_t hours = (seconds % 86400ULL) / 3600ULL;
  const uint8_t minutes = (seconds % 3600ULL) / 60ULL;
  char buffer[40];
  snprintf(buffer, sizeof(buffer), "%lu d %02u:%02u", static_cast<unsigned long>(days), hours, minutes);
  return String(buffer);
}

bool systemTimeIsValid() {
  return time(nullptr) > 1735689600;
}

void beginTimeSynchronization() {
  lastNtpAttemptMs = millis();
  configTime(0, 0, "time.cloudflare.com", "pool.ntp.org", "time.google.com");
}

String generateLocalSecret(const char *prefix) {
  char buffer[48];
  const uint32_t randomA = esp_random();
  const uint32_t randomB = esp_random();
  snprintf(buffer, sizeof(buffer), "%s-%08lX%08lX", prefix,
           static_cast<unsigned long>(randomA), static_cast<unsigned long>(randomB));
  return String(buffer);
}

bool buildSecretIsUsable(const String &value, const char *placeholder) {
  if (value.length() < 8) return false;
  if (placeholder != nullptr && value == placeholder) return false;
  if (value.startsWith("replace-") || value.startsWith("choose-") ||
      value.startsWith("use-the-same-")) return false;
  return true;
}

String loadOrMigrateLocalSecret(const char *key, const char *buildValue,
                                const char *placeholder, const char *generatedPrefix) {
  String saved = preferences.getString(key, "");
  if (saved.length() >= 8) return saved;

  const String compiled = buildValue == nullptr ? String() : String(buildValue);
  if (buildSecretIsUsable(compiled, placeholder)) {
    preferences.putString(key, compiled);
    Serial.printf("Migrated %s into persistent device storage.\n", key);
    return compiled;
  }

  const String generated = generateLocalSecret(generatedPrefix);
  preferences.putString(key, generated);
  Serial.printf("Generated persistent %s: %s\n", key, generated.c_str());
  return generated;
}

void loadConfiguration() {
  preferences.begin("visitor-light", false);

  const String savedWorker = preferences.getString("worker", "");
  if (savedWorker.length() > 0) {
    workerBaseUrl = normalizeWorkerUrl(savedWorker);
  } else {
    workerBaseUrl = normalizeWorkerUrl(LOKWOD_DEFAULT_WORKER_URL);
    if (workerBaseUrl.length() > 0 && workerBaseUrl.indexOf("your-worker") < 0) {
      preferences.putString("worker", workerBaseUrl);
    }
  }

  deviceToken = preferences.getString("token", "");
  if (deviceToken.length() < 32) {
    const String compiledToken = LOKWOD_DEVICE_TOKEN;
    if (buildSecretIsUsable(compiledToken, "use-the-same-random-token-as-the-worker")) {
      deviceToken = compiledToken;
      preferences.putString("token", deviceToken);
      Serial.println(F("Migrated Worker device token into persistent device storage."));
    } else {
      deviceToken = "";
    }
  }

  dashboardPassword = loadOrMigrateLocalSecret(
      "dash-pass", LOKWOD_DASHBOARD_PASSWORD,
      "replace-with-local-dashboard-password", "VL-DASH");
  setupApPassword = loadOrMigrateLocalSecret(
      "setup-pass", LOKWOD_SETUP_AP_PASSWORD, nullptr, "VL-SETUP");
  otaPassword = loadOrMigrateLocalSecret(
      "ota-pass", LOKWOD_OTA_PASSWORD,
      "choose-a-long-local-ota-password", "VL-OTA");

  eventCursor = preferences.getULong64("cursor", 0);
  cursorInitialized = preferences.getBool("cursor-ok", false);
}

void saveCloudConfiguration(const String &newWorkerUrl, const String &newToken) {
  const String normalized = normalizeWorkerUrl(newWorkerUrl);
  bool changed = false;
  if (normalized.length() > 0 && normalized != workerBaseUrl) {
    workerBaseUrl = normalized;
    preferences.putString("worker", workerBaseUrl);
    changed = true;
  }
  if (newToken.length() >= 32 && newToken != deviceToken) {
    deviceToken = newToken;
    preferences.putString("token", deviceToken);
    changed = true;
  }
  if (changed) {
    eventCursor = 0;
    cursorInitialized = false;
    preferences.putULong64("cursor", eventCursor);
    preferences.putBool("cursor-ok", cursorInitialized);
    workerEverConnected = false;
    lastError = "Configuration changed; awaiting first Worker poll.";
  }
}

void enqueueFlash(const FlashJob &job) {
  if (flashQueueCount == AppConfig::kFlashQueueCapacity) {
    flashQueueHead = (flashQueueHead + 1) % AppConfig::kFlashQueueCapacity;
    --flashQueueCount;
  }
  flashQueue[flashQueueTail] = job;
  flashQueueTail = (flashQueueTail + 1) % AppConfig::kFlashQueueCapacity;
  ++flashQueueCount;
}

void enqueueColor(uint8_t red, uint8_t green, uint8_t blue, uint8_t pulses,
                  const String &label, const String &path = "") {
  FlashJob job;
  job.red = red;
  job.green = green;
  job.blue = blue;
  job.pulses = pulses == 0 ? 1 : pulses;
  job.label = label;
  job.path = path;
  enqueueFlash(job);
}

void startNextFlash() {
  if (otaInProgress || flashActive || flashQueueCount == 0) return;
  currentFlash = flashQueue[flashQueueHead];
  flashQueueHead = (flashQueueHead + 1) % AppConfig::kFlashQueueCapacity;
  --flashQueueCount;
  flashPulsesRemaining = currentFlash.pulses;
  flashLedOn = true;
  flashActive = true;
  setLed(currentFlash.red, currentFlash.green, currentFlash.blue);
  flashDeadlineMs = millis() + AppConfig::kFlashOnMs;
}

void serviceFlashQueue() {
  if (otaInProgress) return;
  if (!flashActive) {
    startNextFlash();
    return;
  }
  if (!timeReached(flashDeadlineMs)) return;
  if (flashLedOn) {
    turnLedOff();
    flashLedOn = false;
    if (flashPulsesRemaining > 0) --flashPulsesRemaining;
    if (flashPulsesRemaining == 0) {
      flashActive = false;
      flashDeadlineMs = millis() + AppConfig::kFlashOffMs;
      return;
    }
    flashDeadlineMs = millis() + AppConfig::kFlashOffMs;
  } else {
    setLed(currentFlash.red, currentFlash.green, currentFlash.blue);
    flashLedOn = true;
    flashDeadlineMs = millis() + AppConfig::kFlashOnMs;
  }
}

void wifiManagerAccessPointCallback(WiFiManager *) {
  Serial.println(F("Wi-Fi setup portal active at http://192.168.4.1"));
  setLed(40, 0, 55);
}

void connectToWiFi() {
  WiFi.setHostname(AppConfig::kHostname);
  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  WiFi.persistent(true);

  char workerBuffer[220] = {0};
  workerBaseUrl.toCharArray(workerBuffer, sizeof(workerBuffer));
  WiFiManagerParameter workerParameter(
      "worker_url", "Visitor Worker URL (https://...workers.dev)", workerBuffer,
      static_cast<int>(sizeof(workerBuffer) - 1));

  WiFiManager manager;
  manager.setAPCallback(wifiManagerAccessPointCallback);
  manager.setHostname(AppConfig::kHostname);
  manager.setWiFiAutoReconnect(true);
  manager.setConnectRetries(AppConfig::kConnectRetries);
  manager.setConnectTimeout(25);
  manager.setConfigPortalTimeout(AppConfig::kConfigPortalTimeoutSeconds);
  manager.setConfigPortalBlocking(true);
  manager.addParameter(&workerParameter);

  setLed(0, 0, 45);
  const bool connected = manager.autoConnect(AppConfig::kSetupAccessPoint, setupApPassword.c_str());
  if (!connected) {
    Serial.println(F("Wi-Fi configuration failed; restarting."));
    setLed(60, 0, 0);
    delay(1000);
    ESP.restart();
  }

  const String portalWorkerUrl = normalizeWorkerUrl(String(workerParameter.getValue()));
  if (portalWorkerUrl.length() > 0 && portalWorkerUrl != workerBaseUrl) {
    saveCloudConfiguration(portalWorkerUrl, "");
  }

  setLed(0, 55, 5);
  delay(500);
  turnLedOff();
  Serial.printf("Wi-Fi connected: %s\n", WiFi.localIP().toString().c_str());
}

void configureArduinoOta() {
  ArduinoOTA.setHostname(AppConfig::kHostname);
  ArduinoOTA.setPassword(otaPassword.c_str());
  ArduinoOTA.onStart([]() {
    otaInProgress = true;
    setLed(55, 35, 0);
    Serial.println(F("OTA update started."));
  });
  ArduinoOTA.onProgress([](unsigned int progress, unsigned int total) {
    const uint8_t level = total == 0 ? 20 : static_cast<uint8_t>(10 + (progress * 60ULL / total));
    setLed(level, level / 2, 0);
  });
  ArduinoOTA.onEnd([]() {
    setLed(0, 65, 5);
    Serial.println(F("OTA update complete."));
  });
  ArduinoOTA.onError([](ota_error_t error) {
    otaInProgress = false;
    setLed(70, 0, 0);
    Serial.printf("OTA error: %u\n", static_cast<unsigned>(error));
  });
  ArduinoOTA.begin();
  MDNS.addService("http", "tcp", 80);
}

bool beginSecureRequest(LokwodSecureClient &client, HTTPClient &http, const String &url) {
  client.setCACert(LOKWOD_TRUSTED_ROOTS);
  client.setHandshakeTimeout(10);
  http.setTimeout(AppConfig::kHttpTimeoutMs);
  if (!http.begin(client, url)) {
    lastError = "Could not initialize HTTPS request.";
    return false;
  }
  http.addHeader("Authorization", "Bearer " + deviceToken);
  http.addHeader("X-LOKWOD-Device", "esp32-s3-visitor-light");
  return true;
}

void rememberCursor(uint64_t cursor) {
  eventCursor = cursor;
  cursorInitialized = true;
  preferences.putULong64("cursor", eventCursor);
  preferences.putBool("cursor-ok", true);
}

void processWorkerPayload(const String &payload) {
  JsonDocument document;
  const DeserializationError error = deserializeJson(document, payload);
  if (error) {
    lastError = String("Worker JSON error: ") + error.c_str();
    ++consecutivePollFailures;
    return;
  }
  if (!document["ok"].as<bool>()) {
    lastError = "Worker returned an unsuccessful response.";
    ++consecutivePollFailures;
    return;
  }

  const uint64_t newCursor = document["cursor"].is<uint64_t>()
                                 ? document["cursor"].as<uint64_t>()
                                 : eventCursor;
  todayAcceptedVisits = document["stats"]["total"] | todayAcceptedVisits;
  JsonArray events = document["events"].as<JsonArray>();
  for (JsonObject event : events) {
    JsonArray color = event["color"].as<JsonArray>();
    const uint8_t red = color.size() > 0 ? color[0].as<uint8_t>() : 0;
    const uint8_t green = color.size() > 1 ? color[1].as<uint8_t>() : 80;
    const uint8_t blue = color.size() > 2 ? color[2].as<uint8_t>() : 255;
    const String label = event["label"] | "Website visitor";
    const String path = event["path"] | "/";
    const String title = event["title"] | "";
    const String visitorId = event["visitorId"] | "";
    const String city = event["city"] | "";
    const String region = event["region"] | "";
    const String country = event["country"] | "";
    enqueueColor(red, green, blue, 3, label, path);
    lastEventLabel = label;
    lastEventPath = path;
    lastEventTitle = title;
    lastEventVisitorId = visitorId;
    lastEventCity = city;
    lastEventRegion = region;
    lastEventCountry = country;
    lastEventTimestamp = event["ts"].is<uint64_t>() ? event["ts"].as<uint64_t>() : 0;
    Serial.printf("New visitor: %s %s [%s %s, %s %s]\n", label.c_str(), path.c_str(),
                  visitorId.c_str(), city.c_str(), region.c_str(), country.c_str());
  }

  rememberCursor(newCursor);
  lastSuccessfulPollMs = millis();
  consecutivePollFailures = 0;
  lastError = "";
  workerEverConnected = true;
}

void pollWorker() {
  lastPollAttemptMs = millis();
  if (WiFi.status() != WL_CONNECTED) return;
  if (!systemTimeIsValid()) {
    lastError = "Waiting for network time before secure HTTPS polling.";
    return;
  }
  if (workerBaseUrl.length() == 0 || deviceToken.length() < 32) {
    lastError = "Worker URL or device token is not configured.";
    return;
  }

  char cursorBuffer[24];
  snprintf(cursorBuffer, sizeof(cursorBuffer), "%llu", static_cast<unsigned long long>(eventCursor));
  String url = workerBaseUrl + "/v1/events?after=" + cursorBuffer;
  if (!cursorInitialized) url += "&bootstrap=1";

  LokwodSecureClient secureClient;
  HTTPClient http;
  if (!beginSecureRequest(secureClient, http, url)) {
    ++consecutivePollFailures;
    return;
  }

  lastHttpStatus = http.GET();
  if (lastHttpStatus == HTTP_CODE_OK) {
    processWorkerPayload(http.getString());
  } else {
    ++consecutivePollFailures;
    lastError = "Worker poll failed with HTTP " + String(lastHttpStatus) + ".";
    Serial.println(lastError);
  }
  http.end();
}

bool triggerCloudTest(const String &siteId) {
  if (WiFi.status() != WL_CONNECTED || !systemTimeIsValid() || workerBaseUrl.length() == 0) {
    lastError = "Cloud test unavailable until Wi-Fi, time, and Worker configuration are ready.";
    return false;
  }

  LokwodSecureClient secureClient;
  HTTPClient http;
  if (!beginSecureRequest(secureClient, http, workerBaseUrl + "/v1/test")) return false;
  http.addHeader("Content-Type", "application/json");
  JsonDocument document;
  document["site"] = siteId;
  String body;
  serializeJson(document, body);
  const int status = http.POST(body);
  http.end();
  if (status != HTTP_CODE_OK) {
    lastError = "Cloud test failed with HTTP " + String(status) + ".";
    return false;
  }
  return true;
}

String normalizedVersion(String value) {
  value.trim();
  if (value.startsWith("v") || value.startsWith("V")) value.remove(0, 1);
  int suffix = value.indexOf('-');
  if (suffix < 0) suffix = value.indexOf('+');
  if (suffix >= 0) value.remove(suffix);
  return value;
}

bool parseVersion(const String &input, int &major, int &minor, int &patch) {
  const String value = normalizedVersion(input);
  const int firstDot = value.indexOf('.');
  const int secondDot = firstDot >= 0 ? value.indexOf('.', firstDot + 1) : -1;
  if (firstDot <= 0 || secondDot <= firstDot + 1 || secondDot >= static_cast<int>(value.length()) - 1) return false;
  if (value.indexOf('.', secondDot + 1) >= 0) return false;

  const String parts[3] = {
      value.substring(0, firstDot),
      value.substring(firstDot + 1, secondDot),
      value.substring(secondDot + 1)};
  for (const String &part : parts) {
    if (part.length() == 0) return false;
    for (size_t index = 0; index < part.length(); ++index) {
      if (!isDigit(part[index])) return false;
    }
  }

  major = parts[0].toInt();
  minor = parts[1].toInt();
  patch = parts[2].toInt();
  return true;
}

int compareVersions(const String &left, const String &right) {
  int leftMajor = 0, leftMinor = 0, leftPatch = 0;
  int rightMajor = 0, rightMinor = 0, rightPatch = 0;
  if (!parseVersion(left, leftMajor, leftMinor, leftPatch) ||
      !parseVersion(right, rightMajor, rightMinor, rightPatch)) return 0;
  if (leftMajor != rightMajor) return leftMajor > rightMajor ? 1 : -1;
  if (leftMinor != rightMinor) return leftMinor > rightMinor ? 1 : -1;
  if (leftPatch != rightPatch) return leftPatch > rightPatch ? 1 : -1;
  return 0;
}

bool fetchLatestFirmwareRelease(String &version, String &downloadUrl) {
  autoUpdateStatus = "Checking GitHub for firmware updates...";

  LokwodSecureClient secureClient;
  secureClient.setCACert(LOKWOD_TRUSTED_ROOTS);
  secureClient.setHandshakeTimeout(12);

  HTTPClient http;
  http.setTimeout(AppConfig::kHttpTimeoutMs);
  http.setUserAgent(String("LOKWOD-Visitor-Light/") + AppConfig::kFirmwareVersion);
  if (!http.begin(secureClient, AppConfig::kLatestReleaseApi)) {
    autoUpdateStatus = "Update check could not initialize HTTPS.";
    return false;
  }
  http.addHeader("Accept", "application/vnd.github+json");
  http.addHeader("Cache-Control", "no-cache");

  const int status = http.GET();
  if (status == HTTP_CODE_NOT_FOUND) {
    http.end();
    autoUpdateStatus = "No published firmware release yet.";
    return true;
  }
  if (status != HTTP_CODE_OK) {
    autoUpdateStatus = "GitHub update check failed with HTTP " + String(status) + ".";
    http.end();
    return false;
  }

  const String payload = http.getString();
  http.end();

  JsonDocument document;
  const DeserializationError error = deserializeJson(document, payload);
  if (error) {
    autoUpdateStatus = String("GitHub release JSON error: ") + error.c_str();
    return false;
  }

  const String tag = document["tag_name"] | "";
  version = normalizedVersion(tag);
  int major = 0, minor = 0, patch = 0;
  if (!parseVersion(version, major, minor, patch)) {
    autoUpdateStatus = "Latest GitHub release has an invalid firmware version tag.";
    return false;
  }

  JsonArray assets = document["assets"].as<JsonArray>();
  for (JsonObject asset : assets) {
    const String name = asset["name"] | "";
    if (name == AppConfig::kFirmwareAssetName) {
      downloadUrl = asset["browser_download_url"] | "";
      break;
    }
  }

  if (downloadUrl.length() == 0) {
    autoUpdateStatus = "Latest release has no LOKWOD_Visitor_Light.bin asset.";
    return false;
  }
  return true;
}

bool installFirmwareUpdate(const String &version, const String &downloadUrl) {
  otaInProgress = true;
  autoUpdateStatus = "Downloading and installing v" + version + "...";
  Serial.printf("Automatic firmware update: v%s -> v%s\n",
                AppConfig::kFirmwareVersion, version.c_str());
  setLed(55, 35, 0);

  LokwodSecureClient secureClient;
  secureClient.setCACert(LOKWOD_TRUSTED_ROOTS);
  secureClient.setHandshakeTimeout(15);

  HTTPUpdate updater(AppConfig::kFirmwareDownloadTimeoutMs);
  updater.rebootOnUpdate(false);
  updater.setFollowRedirects(HTTPC_FORCE_FOLLOW_REDIRECTS);
  updater.onProgress([](int progress, int total) {
    const uint8_t level = total <= 0
                              ? 25
                              : static_cast<uint8_t>(15 + (progress * 55LL / total));
    setLed(level, level / 2, 0);
  });

  const t_httpUpdate_return result = updater.update(
      secureClient, downloadUrl, AppConfig::kFirmwareVersion,
      [](HTTPClient *request) {
        request->setUserAgent(String("LOKWOD-Visitor-Light/") + AppConfig::kFirmwareVersion);
        request->addHeader("Accept", "application/octet-stream");
        request->addHeader("Cache-Control", "no-cache");
      });

  if (result == HTTP_UPDATE_OK) {
    autoUpdateStatus = "Installed v" + version + "; rebooting.";
    Serial.println(autoUpdateStatus);
    setLed(0, 70, 5);
    delay(700);
    ESP.restart();
    return true;
  }

  otaInProgress = false;
  if (result == HTTP_UPDATE_NO_UPDATES) {
    autoUpdateStatus = "Firmware server reported no update.";
  } else {
    autoUpdateStatus = "Firmware install failed: " + updater.getLastErrorString();
  }
  Serial.println(autoUpdateStatus);
  setLed(70, 0, 0);
  delay(500);
  turnLedOff();
  return false;
}

void serviceAutomaticUpdate() {
  if (otaInProgress || WiFi.status() != WL_CONNECTED || !systemTimeIsValid()) return;
  if (flashActive || flashQueueCount > 0) return;

  const bool scheduled = timeReached(nextAutoUpdateCheckMs);
  if (!autoUpdateCheckRequested && !scheduled) return;

  autoUpdateCheckRequested = false;
  lastAutoUpdateCheckMs = millis();
  nextAutoUpdateCheckMs = millis() + AppConfig::kAutoUpdateIntervalMs;

  String version;
  String downloadUrl;
  if (!fetchLatestFirmwareRelease(version, downloadUrl)) {
    nextAutoUpdateCheckMs = millis() + AppConfig::kAutoUpdateRetryMs;
    return;
  }

  if (version.length() == 0) return;
  latestFirmwareVersion = version;

  const int comparison = compareVersions(version, AppConfig::kFirmwareVersion);
  if (comparison <= 0) {
    autoUpdateStatus = "Up to date on v" + String(AppConfig::kFirmwareVersion) + ".";
    return;
  }

  autoUpdateStatus = "New firmware v" + version + " found.";
  installFirmwareUpdate(version, downloadUrl);
}

String dashboardPage() {
  String page;
  page.reserve(15000);
  page += F(R"HTML(<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>LOKWOD Visitor Light</title>
<style>
:root{font-family:Inter,Segoe UI,Arial,sans-serif;color:#172033;background:#eef3f9}
*{box-sizing:border-box}body{margin:0}.wrap{max-width:980px;margin:auto;padding:28px 18px 50px}
.logo{font-size:32px;font-weight:900;letter-spacing:-1px}.logo span{color:#1477ff}.tag{color:#5e6a7d;margin-top:2px}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:16px;margin-top:22px}
.card{background:#fff;border:1px solid #dbe4ef;border-radius:18px;padding:20px;box-shadow:0 8px 28px rgba(33,55,88,.07)}
h2{font-size:17px;margin:0 0 14px}.stat{font-size:30px;font-weight:800;color:#126ee8}.muted{color:#67758a;font-size:13px}
.row{display:flex;justify-content:space-between;gap:12px;padding:7px 0;border-bottom:1px solid #edf1f6}.row:last-child{border:0}.value{text-align:right;font-weight:650;overflow-wrap:anywhere}
button{border:0;border-radius:10px;background:#1477ff;color:white;padding:11px 15px;font-weight:750;cursor:pointer;margin:4px 5px 4px 0}
button.secondary{background:#273449}button.danger{background:#b62c2c}input,select{width:100%;padding:11px;border:1px solid #cbd6e3;border-radius:10px;margin:5px 0 12px;font:inherit}
label{font-size:13px;font-weight:700;color:#455268}.legend{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px;font-size:13px}.dot{display:inline-block;width:11px;height:11px;border-radius:50%;margin-right:7px}
.notice{padding:10px 12px;border-radius:10px;background:#edf5ff;color:#1c4f89;font-size:13px;margin-top:12px}.ok{color:#08783e}.bad{color:#b02323}.visitor-id{font-size:24px;font-weight:850;color:#126ee8;letter-spacing:.5px;margin:2px 0 4px}.visitor-location{font-size:17px;font-weight:750;margin-bottom:10px}.visitor-page{font-size:13px;color:#455268;overflow-wrap:anywhere}
@media(max-width:520px){.legend{grid-template-columns:1fr}.row{display:block}.value{text-align:left;margin-top:3px}}
</style></head><body><main class="wrap"><div class="logo">LOK<span>WOD</span></div><div class="tag">Website Visitor Light &middot; ESP32-S3</div>
<div class="grid"><section class="card"><h2>Live status</h2><div class="stat" id="visits">--</div><div class="muted">accepted visitors today</div><div style="height:10px"></div>
<div class="row"><span>Wi-Fi</span><span class="value" id="wifi">--</span></div><div class="row"><span>Worker</span><span class="value" id="worker">--</span></div><div class="row"><span>Last event</span><span class="value" id="last">--</span></div><div class="row"><span>Queue</span><span class="value" id="queue">--</span></div><div class="row"><span>Uptime</span><span class="value" id="uptime">--</span></div><div class="notice" id="error">Loading status...</div></section>
<section class="card"><h2>Test the light</h2><p class="muted">Local test checks the LED. Cloud test checks the Worker, secure feed, ESP32 poller, and LED together.</p>
<form method="post" action="/api/test"><button type="submit">Local RGB test</button></form>
<form method="post" action="/api/cloud-test"><label for="site">Cloud test color</label><select id="site" name="site"><option value="nautical-dream">Nautical Dream &mdash; aqua</option><option value="lokwod">LOKWOD &mdash; blue</option><option value="life-in-the-simulation">Life in the Simulation &mdash; green</option><option value="beautiful-mens-club">Beautiful Men's Club &mdash; purple</option><option value="mr-adventure-dad">Mr Adventure Dad &mdash; orange</option><option value="syracuse-appraiser">Syracuse Appraiser &mdash; amber</option><option value="accurate-re-appraisals">Accurate RE Appraisals &mdash; white</option></select><button class="secondary" type="submit">Run cloud test</button></form></section>
<section class="card"><h2>Site colors</h2><div class="legend"><div><i class="dot" style="background:rgb(0,185,255)"></i>Nautical Dream</div><div><i class="dot" style="background:rgb(0,82,255)"></i>LOKWOD</div><div><i class="dot" style="background:rgb(0,235,95)"></i>Life Simulation</div><div><i class="dot" style="background:rgb(180,35,255)"></i>Beautiful Men's Club</div><div><i class="dot" style="background:rgb(255,96,0)"></i>Mr Adventure Dad</div><div><i class="dot" style="background:rgb(255,174,0)"></i>Syracuse Appraiser</div><div><i class="dot" style="background:rgb(235,235,235);border:1px solid #aaa"></i>Accurate Appraisals</div></div></section>
<section class="card"><h2>Cloud configuration</h2><form method="post" action="/api/config"><label for="worker_url">Cloudflare Worker base URL</label><input id="worker_url" name="worker_url" type="url" required value=")HTML");
  page += '"';
  page += htmlEscape(workerBaseUrl);
  page += '"';
  page += F(R"HTML( placeholder="https://...workers.dev"><label for="device_token">New device token</label><input id="device_token" name="device_token" type="password" placeholder="Leave blank to keep the existing token"><button type="submit">Save configuration</button></form><p class="muted">Saving a new URL or token resets the event cursor so old visits do not flash unexpectedly.</p></section>
<section class="card"><h2>Maintenance</h2><div class="row"><span>Firmware</span><span class="value" id="firmware">--</span></div><div class="row"><span>Latest release</span><span class="value" id="latest_firmware">--</span></div><div class="row"><span>Auto update</span><span class="value" id="update_status">--</span></div><form method="post" action="/api/check-update"><button type="submit">Check for firmware update</button></form><form method="post" action="/api/restart"><button class="secondary" type="submit">Restart ESP32</button></form><form method="post" action="/api/reset-wifi" onsubmit="return confirm('Erase saved Wi-Fi and restart setup?')"><button class="danger" type="submit">Reset Wi-Fi</button></form><p class="muted">Automatic GitHub firmware updates check after boot and every six hours. RGB GPIO 48 &middot; secure HTTPS polling.</p></section>
<section class="card"><h2>Latest visitor</h2><div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:12px"><button type="button" id="sound_toggle" onclick="toggleSound()">Enable cash-register sound</button><span class="muted" id="sound_state">Off &middot; appraisal sites only</span></div><div class="visitor-id" id="visitor_id">Waiting for a visitor</div><div class="visitor-location" id="visitor_location">--</div><div class="row"><span>Site</span><span class="value" id="visitor_site">--</span></div><div class="row"><span>Page</span><span class="value visitor-page" id="visitor_path">--</span></div><div class="row"><span>Title</span><span class="value visitor-page" id="visitor_title">--</span></div><div class="row"><span>When</span><span class="value" id="visitor_time">--</span></div><p class="muted" style="margin:12px 0 0">Visitor IDs are anonymous and location is approximate. Raw visitor IP addresses are not stored or shown.</p></section></div></main>
<script>
const text=(id,v)=>document.getElementById(id).textContent=v;
let audioCtx=null,soundEnabled=false,lastSeenEventTs=null,statusInitialized=false;
const appraisalSite=(name)=>/syracuse appraiser|accurate/i.test(name||'');
function cashRegister(){
  if(!soundEnabled||!audioCtx)return;
  if(audioCtx.state==='suspended')audioCtx.resume();
  const now=audioCtx.currentTime;
  const ping=(freq,at,dur,gain,type='sine')=>{const o=audioCtx.createOscillator(),g=audioCtx.createGain();o.type=type;o.frequency.setValueAtTime(freq,now+at);g.gain.setValueAtTime(.0001,now+at);g.gain.exponentialRampToValueAtTime(gain,now+at+.008);g.gain.exponentialRampToValueAtTime(.0001,now+at+dur);o.connect(g);g.connect(audioCtx.destination);o.start(now+at);o.stop(now+at+dur+.02)};
  ping(1568,0,.10,.16,'triangle');ping(2093,.075,.12,.13,'triangle');ping(2637,.15,.18,.11,'sine');
  ping(196,.025,.08,.055,'square');ping(247,.20,.09,.045,'square');
}
function updateSoundUi(){const b=document.getElementById('sound_toggle');b.textContent=soundEnabled?'Mute cash-register sound':'Enable cash-register sound';text('sound_state',(soundEnabled?'On':'Off')+' · appraisal sites only')}
function toggleSound(){
  if(!audioCtx)audioCtx=new (window.AudioContext||window.webkitAudioContext)();
  if(audioCtx.state==='suspended')audioCtx.resume();
  soundEnabled=!soundEnabled;localStorage.setItem('lokwodAppraisalSound',soundEnabled?'1':'0');updateSoundUi();
  if(soundEnabled)cashRegister();
}
async function refresh(){try{const r=await fetch('/api/status',{cache:'no-store'});const s=await r.json();text('visits',s.today_visits);text('wifi',s.wifi_connected?s.ip+' ('+s.rssi+' dBm)':'Disconnected');text('worker',s.worker_connected?'Connected':'Not connected');text('last',s.last_event||'None yet');text('queue',s.queue_depth);text('uptime',s.uptime);text('firmware','v'+s.firmware);text('latest_firmware',s.latest_firmware?('v'+s.latest_firmware):'--');text('update_status',s.auto_update_status||'--');text('visitor_id',s.visitor_id?('Visitor '+s.visitor_id):'Waiting for a visitor');const loc=[s.visitor_city,s.visitor_region,s.visitor_country].filter(Boolean).join(', ');text('visitor_location',loc||'Location unavailable');text('visitor_site',s.visitor_site||'--');text('visitor_path',s.visitor_path||'--');text('visitor_title',s.visitor_title||'--');text('visitor_time',s.last_event_timestamp?new Date(Number(s.last_event_timestamp)).toLocaleString():'--');const ts=String(s.last_event_timestamp||'');if(statusInitialized&&ts&&ts!==lastSeenEventTs&&appraisalSite(s.visitor_site))cashRegister();lastSeenEventTs=ts;statusInitialized=true;const e=document.getElementById('error');e.textContent=s.error||'Everything is operating normally.';e.className='notice '+(s.error?'bad':'ok')}catch(e){text('error','Dashboard status request failed.')}}
updateSoundUi();refresh();setInterval(refresh,2000);
</script></body></html>)HTML");
  return page;
}

bool requireDashboardAuthorization() {
  if (webServer.authenticate("admin", dashboardPassword.c_str())) return true;
  webServer.requestAuthentication();
  return false;
}

void redirectHome() {
  webServer.sendHeader("Location", "/", true);
  webServer.send(303, "text/plain", "");
}

void handleStatusApi() {
  if (!requireDashboardAuthorization()) return;
  JsonDocument document;
  document["firmware"] = AppConfig::kFirmwareVersion;
  document["latest_firmware"] = latestFirmwareVersion;
  document["auto_update_status"] = autoUpdateStatus;
  document["last_update_check_age_ms"] = lastAutoUpdateCheckMs == 0 ? 0 : millis() - lastAutoUpdateCheckMs;
  document["wifi_connected"] = WiFi.status() == WL_CONNECTED;
  document["ip"] = WiFi.status() == WL_CONNECTED ? WiFi.localIP().toString() : "";
  document["rssi"] = WiFi.status() == WL_CONNECTED ? WiFi.RSSI() : 0;
  document["worker_configured"] = workerBaseUrl.length() > 0 && deviceToken.length() >= 32;
  document["worker_connected"] = workerEverConnected && (millis() - lastSuccessfulPollMs < 15000UL);
  document["last_poll_age_ms"] = workerEverConnected ? millis() - lastSuccessfulPollMs : 0;
  document["today_visits"] = todayAcceptedVisits;
  document["cursor"] = eventCursor;
  document["queue_depth"] = flashQueueCount + (flashActive ? 1 : 0);
  document["last_event"] = lastEventLabel == "None yet" ? "" : lastEventLabel + " " + lastEventPath;
  document["visitor_site"] = lastEventLabel == "None yet" ? "" : lastEventLabel;
  document["visitor_path"] = lastEventPath;
  document["visitor_title"] = lastEventTitle;
  document["visitor_id"] = lastEventVisitorId;
  document["visitor_city"] = lastEventCity;
  document["visitor_region"] = lastEventRegion;
  document["visitor_country"] = lastEventCountry;
  document["last_event_timestamp"] = lastEventTimestamp;
  document["last_http_status"] = lastHttpStatus;
  document["poll_failures"] = consecutivePollFailures;
  document["uptime"] = formatUptime();
  document["free_heap"] = ESP.getFreeHeap();
  document["error"] = lastError;
  String payload;
  serializeJson(document, payload);
  webServer.sendHeader("Cache-Control", "no-store");
  webServer.send(200, "application/json", payload);
}

void configureWebServer() {
  webServer.on("/", HTTP_GET, []() {
    if (!requireDashboardAuthorization()) return;
    webServer.send(200, "text/html; charset=utf-8", dashboardPage());
  });
  webServer.on("/api/status", HTTP_GET, handleStatusApi);
  webServer.on("/api/test", HTTP_POST, []() {
    if (!requireDashboardAuthorization()) return;
    enqueueColor(255, 0, 0, 1, "Local red test");
    enqueueColor(0, 255, 0, 1, "Local green test");
    enqueueColor(0, 0, 255, 1, "Local blue test");
    redirectHome();
  });
  webServer.on("/api/cloud-test", HTTP_POST, []() {
    if (!requireDashboardAuthorization()) return;
    const String site = webServer.hasArg("site") ? webServer.arg("site") : "lokwod";
    triggerCloudTest(site);
    redirectHome();
  });
  webServer.on("/api/config", HTTP_POST, []() {
    if (!requireDashboardAuthorization()) return;
    const String newWorker = webServer.hasArg("worker_url") ? webServer.arg("worker_url") : "";
    const String newToken = webServer.hasArg("device_token") ? webServer.arg("device_token") : "";
    saveCloudConfiguration(newWorker, newToken);
    redirectHome();
  });
  webServer.on("/api/check-update", HTTP_POST, []() {
    if (!requireDashboardAuthorization()) return;
    autoUpdateCheckRequested = true;
    webServer.sendHeader("Location", "/", true);
    webServer.send(303, "text/plain", "Firmware update check scheduled.");
  });
  webServer.on("/api/restart", HTTP_POST, []() {
    if (!requireDashboardAuthorization()) return;
    webServer.send(200, "text/plain", "Restarting");
    delay(200);
    ESP.restart();
  });
  webServer.on("/api/reset-wifi", HTTP_POST, []() {
    if (!requireDashboardAuthorization()) return;
    WiFiManager manager;
    manager.resetSettings();
    webServer.send(200, "text/plain", "Wi-Fi settings erased. Restarting.");
    delay(300);
    ESP.restart();
  });
  webServer.onNotFound([]() {
    if (!requireDashboardAuthorization()) return;
    webServer.send(404, "text/plain", "Not found");
  });
  webServer.begin();
}

void serviceConnectivity() {
  if (WiFi.status() == WL_CONNECTED) {
    if (disconnectedSinceMs != 0) {
      Serial.printf("Wi-Fi reconnected after %lu ms. IP: %s\n",
                    static_cast<unsigned long>(millis() - disconnectedSinceMs),
                    WiFi.localIP().toString().c_str());
    }
    disconnectedSinceMs = 0;
    lastReconnectAttemptMs = 0;
    reconnectAttemptCount = 0;
    return;
  }

  const uint32_t nowMs = millis();
  if (disconnectedSinceMs == 0) {
    disconnectedSinceMs = nowMs;
    lastReconnectAttemptMs = nowMs - AppConfig::kReconnectAttemptMs;
    Serial.println(F("Wi-Fi disconnected; automatic reconnect started."));
  }
  if (nowMs - lastReconnectAttemptMs >= AppConfig::kReconnectAttemptMs) {
    lastReconnectAttemptMs = nowMs;
    ++reconnectAttemptCount;
    WiFi.setAutoReconnect(true);
    Serial.printf("Wi-Fi reconnect attempt %lu...\n",
                  static_cast<unsigned long>(reconnectAttemptCount));
    WiFi.reconnect();
  }
  if (nowMs - disconnectedSinceMs > AppConfig::kReconnectRestartMs) {
    Serial.println(F("Wi-Fi remained disconnected; restarting into saved-network recovery flow."));
    delay(100);
    ESP.restart();
  }
}

void serviceTime() {
  if (systemTimeIsValid()) return;
  if (millis() - lastNtpAttemptMs >= AppConfig::kNtpRetryMs) beginTimeSynchronization();
}

void setup() {
  Serial.begin(115200);
  delay(150);
  Serial.println();
  Serial.printf("LOKWOD Website Visitor Light v%s\n", AppConfig::kFirmwareVersion);
  rgbLed.begin();
  rgbLed.setBrightness(AppConfig::kBrightness);
  rgbLed.clear();
  rgbLed.show();
  setLed(25, 25, 25);
  loadConfiguration();
  connectToWiFi();
  beginTimeSynchronization();
  configureWebServer();
  configureArduinoOta();
  Serial.printf("Dashboard: http://%s.local/ or http://%s/\n", AppConfig::kHostname,
                WiFi.localIP().toString().c_str());
  Serial.printf("Worker configured: %s\n", workerBaseUrl.length() > 0 ? "yes" : "no");
  turnLedOff();
  lastPollAttemptMs = millis() - AppConfig::kPollIntervalMs;
  nextAutoUpdateCheckMs = millis() + AppConfig::kAutoUpdateFirstCheckMs;
}

void loop() {
  ArduinoOTA.handle();
  webServer.handleClient();
  serviceFlashQueue();
  serviceConnectivity();
  serviceTime();
  serviceAutomaticUpdate();
  if (!otaInProgress && WiFi.status() == WL_CONNECTED &&
      millis() - lastPollAttemptMs >= AppConfig::kPollIntervalMs) {
    pollWorker();
  }
  delay(2);
}