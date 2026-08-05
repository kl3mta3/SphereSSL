using System.Text;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Models.ReportModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Services.ReportServices
{
    public static class CertificateInventoryReportService
    {
        private static readonly HashSet<string> ValidFilters =
            new(StringComparer.OrdinalIgnoreCase) { "all", "active", "expiring", "expired" };

        public static bool TryNormalizeFilter(string? filter, out string normalizedFilter)
        {
            normalizedFilter = string.IsNullOrWhiteSpace(filter)
                ? "all"
                : filter.Trim().ToLowerInvariant();

            return ValidFilters.Contains(normalizedFilter);
        }

        public static CertificateInventoryReport Build(
            IEnumerable<CertRecord> certificates,
            IEnumerable<DNSProvider> providers,
            string normalizedFilter)
        {
            var generatedAtUtc = DateTime.UtcNow;
            var providerNames = providers
                .Where(provider => !string.IsNullOrWhiteSpace(provider.ProviderId))
                .GroupBy(provider => provider.ProviderId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var provider = group.First();
                        return !string.IsNullOrWhiteSpace(provider.ProviderName)
                            ? provider.ProviderName
                            : provider.Provider;
                    });

            var rows = certificates
                .Select(certificate => CreateRow(certificate, providerNames, generatedAtUtc))
                .Where(row => MatchesFilter(row, normalizedFilter))
                .OrderBy(row => row.ExpirationDate)
                .ThenBy(row => row.Domains, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new CertificateInventoryReport
            {
                GeneratedAtUtc = generatedAtUtc,
                Filter = GetFilterLabel(normalizedFilter),
                Rows = rows
            };
        }

        public static byte[] BuildCsv(CertificateInventoryReport report)
        {
            var csv = new StringBuilder();
            csv.AppendLine(EscapeCsvValue(report.Title));
            csv.AppendLine($"{EscapeCsvValue("Generated (UTC)")},{EscapeCsvValue(report.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"))}");
            csv.AppendLine($"{EscapeCsvValue("Filter")},{EscapeCsvValue(report.Filter)}");
            csv.AppendLine($"{EscapeCsvValue("Records")},{report.Rows.Count}");
            csv.AppendLine();
            csv.AppendLine("Domain,Status,Issued Date,Expiration Date,Days Remaining,Auto-Renew,Challenge Type,Provider");

            foreach (var row in report.Rows)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsvValue(row.Domains),
                    EscapeCsvValue(row.Status),
                    EscapeCsvValue(row.IssuedDate.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")),
                    EscapeCsvValue(row.ExpirationDate.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")),
                    row.DaysRemaining.ToString(),
                    EscapeCsvValue(row.AutoRenew),
                    EscapeCsvValue(row.ChallengeType),
                    EscapeCsvValue(row.Providers)));
            }

            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv.ToString());
        }

        private static CertificateInventoryReportRow CreateRow(
            CertRecord certificate,
            IReadOnlyDictionary<string, string> providerNames,
            DateTime generatedAtUtc)
        {
            var domains = certificate.Challenges
                .Select(challenge => challenge.Domain)
                .Where(domain => !string.IsNullOrWhiteSpace(domain))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase);

            var providerLabels = certificate.Challenges
                .Select(challenge => GetProviderLabel(challenge.ProviderId, providerNames))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(provider => provider, StringComparer.OrdinalIgnoreCase);

            var status = certificate.ExpiryDate < generatedAtUtc
                ? "Expired"
                : certificate.ExpiryDate <= generatedAtUtc.AddDays(ConfigureService.RenewBeforeExpiryDays)
                    ? "Expiring Soon"
                    : "Active";

            return new CertificateInventoryReportRow
            {
                Domains = string.Join("; ", domains) is { Length: > 0 } domainList ? domainList : "No domains",
                Status = status,
                IssuedDate = certificate.CreationDate,
                ExpirationDate = certificate.ExpiryDate,
                DaysRemaining = (certificate.ExpiryDate - generatedAtUtc).Days,
                AutoRenew = certificate.autoRenew ? "Enabled" : "Disabled",
                ChallengeType = string.IsNullOrWhiteSpace(certificate.ChallengeType)
                    ? "Unknown"
                    : certificate.ChallengeType,
                Providers = string.Join("; ", providerLabels) is { Length: > 0 } providerList
                    ? providerList
                    : "Not configured"
            };
        }

        private static string GetProviderLabel(
            string? providerId,
            IReadOnlyDictionary<string, string> providerNames)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                return "Manual DNS";
            }

            return providerNames.TryGetValue(providerId, out var providerName) &&
                   !string.IsNullOrWhiteSpace(providerName)
                ? providerName
                : "Unknown Provider";
        }

        private static bool MatchesFilter(CertificateInventoryReportRow row, string filter)
        {
            return filter switch
            {
                "active" => row.Status == "Active",
                "expiring" => row.Status == "Expiring Soon",
                "expired" => row.Status == "Expired",
                _ => true
            };
        }

        private static string GetFilterLabel(string filter)
        {
            return filter switch
            {
                "active" => "Active",
                "expiring" => "Expiring Soon",
                "expired" => "Expired",
                _ => "All"
            };
        }

        private static string EscapeCsvValue(string? value)
        {
            var safeValue = value ?? string.Empty;
            if (safeValue.Length > 0 && "=+-@".Contains(safeValue[0]))
            {
                safeValue = $"'{safeValue}";
            }

            return $"\"{safeValue.Replace("\"", "\"\"")}\"";
        }
    }
}
