using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Models.ConnectionModels;

namespace SphereSSLv2.Services
{
    public static class NotificationService
    {
        private static readonly HttpClient _http = new();
        private static readonly ConnectionRepository _connectionRepo = new();

        // Fan out an event to every connection the user owns.
        // Safe to fire-and-forget — all errors are swallowed per-connection.
        public static async Task NotifyUserAsync(string userId, string eventType, string message)
        {
            if (string.IsNullOrEmpty(userId)) return;
            try
            {
                var conns = await _connectionRepo.GetConnectionsByUserIdAsync(userId);
                foreach (var c in conns)
                    await SendAsync(c, eventType, message);
            }
            catch { /* swallow — never let notifications break cert ops */ }
        }

        public static async Task SendAsync(UserConnection conn, string eventType, string message)
        {
            if (!conn.IsEnabled) return;
            if (!ShouldSend(conn, eventType)) return;
            try
            {
                await SendInternal(conn, message);
            }
            catch { /* swallow — notification failure must never break cert ops */ }
        }

        // Same as SendAsync but throws on failure — used by the Test button so users see real errors.
        public static Task SendTestAsync(UserConnection conn, string message) => SendInternal(conn, message);

        private static async Task SendInternal(UserConnection conn, string message)
        {
            switch (conn.ConnectionType.ToLower())
            {
                case "discord":  await SendDiscord(conn.Settings, message);  break;
                case "slack":    await SendSlack(conn.Settings, message);    break;
                case "webhook":  await SendWebhook(conn.Settings, message);  break;
                case "ntfy":     await SendNtfy(conn.Settings, message);     break;
                case "gotify":   await SendGotify(conn.Settings, message);   break;
                case "telegram": await SendTelegram(conn.Settings, message); break;
                case "email":    await SendEmail(conn.Settings, message);    break;
                case "script":   await RunScript(conn.Settings, message);    break;
                default: throw new InvalidOperationException($"Unknown connection type: {conn.ConnectionType}");
            }
        }

        private static bool ShouldSend(UserConnection conn, string eventType) => eventType switch
        {
            "PreRenew"     => conn.OnPreRenew,
            "PreExpiry"    => conn.OnPreExpiry,
            "RenewSuccess" => conn.OnRenewSuccess,
            "RenewFail"    => conn.OnRenewFail,
            _ => false
        };

        private static async Task SendDiscord(string settingsJson, string message)
        {
            var s = JsonDocument.Parse(settingsJson).RootElement;
            var url = s.GetProperty("webhookUrl").GetString();
            await _http.PostAsync(url, JsonContent.Create(new { content = message }));
        }

        private static async Task SendSlack(string settingsJson, string message)
        {
            var s = JsonDocument.Parse(settingsJson).RootElement;
            var url = s.GetProperty("webhookUrl").GetString();
            await _http.PostAsync(url, JsonContent.Create(new { text = message }));
        }

        private static async Task SendWebhook(string settingsJson, string message)
        {
            var s = JsonDocument.Parse(settingsJson).RootElement;
            var url = s.GetProperty("url").GetString();
            var method = s.TryGetProperty("method", out var m) ? m.GetString() ?? "POST" : "POST";
            var body = s.TryGetProperty("body", out var b) ? b.GetString()?.Replace("{message}", message) ?? message : message;
            var req = new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            await _http.SendAsync(req);
        }

        private static async Task SendNtfy(string settingsJson, string message)
        {
            var s = JsonDocument.Parse(settingsJson).RootElement;
            var url = s.GetProperty("url").GetString();
            await _http.PostAsync(url, new StringContent(message));
        }

        private static async Task SendGotify(string settingsJson, string message)
        {
            var s = JsonDocument.Parse(settingsJson).RootElement;
            var url = s.GetProperty("url").GetString()?.TrimEnd('/');
            var token = s.GetProperty("token").GetString();
            var req = new HttpRequestMessage(HttpMethod.Post, $"{url}/message?token={token}");
            req.Content = JsonContent.Create(new { title = "SphereSSL", message });
            await _http.SendAsync(req);
        }

        private static async Task SendTelegram(string settingsJson, string message)
        {
            var s = JsonDocument.Parse(settingsJson).RootElement;
            var botToken = s.GetProperty("botToken").GetString();
            var chatId = s.GetProperty("chatId").GetString();
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            await _http.PostAsync(url, JsonContent.Create(new { chat_id = chatId, text = message }));
        }

        private static async Task SendEmail(string settingsJson, string message)
        {
            var s = JsonDocument.Parse(settingsJson).RootElement;
            var host = s.GetProperty("host").GetString();
            int port = 587;
            if (s.TryGetProperty("port", out var p))
            {
                if (p.ValueKind == JsonValueKind.Number) port = p.GetInt32();
                else if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var parsedPort)) port = parsedPort;
            }
            var user = s.GetProperty("username").GetString();
            var pass = s.GetProperty("password").GetString();
            var from = s.GetProperty("from").GetString();
            var to   = s.GetProperty("to").GetString();
            using var smtp = new System.Net.Mail.SmtpClient(host, port)
            {
                Credentials = new System.Net.NetworkCredential(user, pass),
                EnableSsl = true,
                DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network
            };
            await smtp.SendMailAsync(from, to, "SphereSSL Notification", message);
        }

        private static async Task RunScript(string settingsJson, string message)
        {
            var s = JsonDocument.Parse(settingsJson).RootElement;
            var script = s.GetProperty("script").GetString() ?? "";
            var safeMsg = message.Replace("'", "\\'");
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"{script} '{safeMsg}'\"",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            if (proc != null) await proc.WaitForExitAsync();
        }
    }
}
