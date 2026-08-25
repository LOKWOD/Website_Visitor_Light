using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LOKWOD.VisitorKey
{
    internal sealed class VisitorStatus
    {
        internal long Timestamp { get; set; }
        internal string Site { get; set; } = string.Empty;
        internal string Kind { get; set; } = string.Empty;
        internal bool WorkerConnected { get; set; }
        internal Color Color => SiteColor(Site, Kind);

        private static Color SiteColor(string site, string kind)
        {
            if (string.Equals(kind, "affiliate_click", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(255, 215, 0);
            string value = (site ?? string.Empty).ToLowerInvariant();
            if (value.Contains("nautical")) return Color.FromArgb(0, 185, 255);
            if (value.Contains("life in the simulation")) return Color.FromArgb(0, 235, 95);
            if (value.Contains("beautiful men's") || value.Contains("beautiful mens")) return Color.FromArgb(180, 35, 255);
            if (value.Contains("adventure")) return Color.FromArgb(255, 96, 0);
            if (value.Contains("syracuse")) return Color.FromArgb(255, 174, 0);
            if (value.Contains("(.org)")) return Color.FromArgb(255, 0, 0);
            if (value.Contains("accurate re")) return Color.FromArgb(235, 235, 235);
            if (value.Contains("dish")) return Color.FromArgb(255, 0, 140);
            if (value.Contains("blappos")) return Color.FromArgb(0, 255, 180);
            if (value.Contains("big bud")) return Color.FromArgb(170, 255, 0);
            if (value.Contains("crypto")) return Color.FromArgb(75, 0, 255);
            return Color.FromArgb(0, 82, 255);
        }
    }

    internal sealed class VisitorLightClient : IDisposable
    {
        private readonly CookieContainer _cookies = new CookieContainer();
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _password;
        private bool _authenticated;

        internal VisitorLightClient(string baseUrl, string password)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _password = password;
            var handler = new SocketsHttpHandler
            {
                CookieContainer = _cookies,
                UseCookies = true,
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(4),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };
            _http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(4),
            };
        }

        public void Dispose() => _http.Dispose();

        internal async Task<VisitorStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            if (!_authenticated) await LoginAsync(cancellationToken).ConfigureAwait(false);
            HttpResponseMessage response = await GetStatusResponseAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // The ESP32 sends a redirect to /login when the dashboard session
                // expires. The original companion only handled HTTP 401, so it
                // silently stopped receiving visitors after that first expiry.
                if (RequiresLogin(response))
                {
                    response.Dispose();
                    _authenticated = false;
                    await LoginAsync(cancellationToken).ConfigureAwait(false);
                    response = await GetStatusResponseAsync(cancellationToken).ConfigureAwait(false);
                }
                if (RequiresLogin(response))
                    throw new UnauthorizedAccessException("Visitor Light dashboard login expired and could not be renewed.");

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                return new VisitorStatus
                {
                    Timestamp = ReadLong(root, "last_event_timestamp"),
                    Site = ReadString(root, "visitor_site"),
                    Kind = ReadString(root, "visitor_kind"),
                    WorkerConnected = ReadBoolean(root, "worker_connected"),
                };
            }
            finally { response.Dispose(); }
        }

        private Task<HttpResponseMessage> GetStatusResponseAsync(CancellationToken cancellationToken) =>
            _http.GetAsync(_baseUrl + "/api/status", HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        private async Task LoginAsync(CancellationToken cancellationToken)
        {
            using var body = new FormUrlEncodedContent(new KeyValuePair<string?, string?>[]
            {
                new KeyValuePair<string?, string?>("username", "admin"),
                new KeyValuePair<string?, string?>("password", _password),
                new KeyValuePair<string?, string?>("remember", "1"),
            });
            using HttpResponseMessage response = await _http.PostAsync(_baseUrl + "/login", body, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.SeeOther && response.StatusCode != HttpStatusCode.Redirect)
                throw new UnauthorizedAccessException("Visitor Light dashboard login failed. Check the saved password.");
            _authenticated = true;
        }

        private static bool RequiresLogin(HttpResponseMessage response)
        {
            int status = (int)response.StatusCode;
            return response.StatusCode == HttpStatusCode.Unauthorized ||
                   response.StatusCode == HttpStatusCode.Forbidden ||
                   (status >= 300 && status <= 399);
        }

        private static string ReadString(JsonElement root, string property) =>
            root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

        private static long ReadLong(JsonElement root, string property)
        {
            if (!root.TryGetProperty(property, out JsonElement value)) return 0;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number)) return number;
            return value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number) ? number : 0;
        }

        private static bool ReadBoolean(JsonElement root, string property) =>
            root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.True;
    }
}
