namespace SphereSSLv2.Models.ReportModels
{
    public sealed class CertificateInventoryReport
    {
        public string Title { get; init; } = "SphereSSL Certificate Inventory Report";
        public DateTime GeneratedAtUtc { get; init; }
        public string Filter { get; init; } = "All";
        public List<CertificateInventoryReportRow> Rows { get; init; } = new();
    }

    public sealed class CertificateInventoryReportRow
    {
        public string Domains { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime IssuedDate { get; init; }
        public DateTime ExpirationDate { get; init; }
        public int DaysRemaining { get; init; }
        public string AutoRenew { get; init; } = string.Empty;
        public string ChallengeType { get; init; } = string.Empty;
        public string Providers { get; init; } = string.Empty;
    }
}
