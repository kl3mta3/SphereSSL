using SphereSSLv2.Data;

namespace SphereSSLv2.Testing
{
    public class TestingTools
    {

        public static async Task GenerateFakeCertRecords()
        {
            var now = DateTime.UtcNow;

            for (int i = 0; i < 12; i++)
            {
                var cert = new CertRecord
                {
                    OrderId = $"ORD-{Guid.NewGuid()}",
                    Domain = $"test{i}.example.com",
                    Email = $"user{i}@example.com",
                    DnsChallengeToken = $"token-{i}",
                    SavePath = $"/fake/path/cert{i}.pem",
                    Provider = "FakeCA",
                    CreationDate = now.AddDays(-Random.Shared.Next(1, 365)),
                    UseSeparateFiles = i % 2 == 0,
                    SaveForRenewal = i % 3 == 0,
                    autoRenew = i % 2 == 0,
                    FailedRenewals = Random.Shared.Next(0, 3),
                    SuccessfulRenewals = Random.Shared.Next(0, 10),
                    ZoneId = $"zone-{i}",
                    Signer = "MockSigner",
                    AccountID = $"acct-{i}",
                    OrderUrl = $"https://acme.fake/{i}",
                    ChallengeType = "dns-01",
                    Thumbprint = $"thumb-{i}",
                    AuthorizationUrls = new List<string> { $"https://auth.fake/{i}" }
                };

                // Set the expiry date based on index to create a mix
                if (i < 3)
                {
                    // Expired
                    cert.ExpiryDate = now.AddDays(-Random.Shared.Next(1, 30));
                    await DatabaseManager.InsertExpiredCert(cert);
                }
                else if (i < 7)
                {
                    // Expiring soon (within 30 days)
                    cert.ExpiryDate = now.AddDays(Random.Shared.Next(1, 30));
                    Spheressl.ExpiringSoonCertRecords.Add(cert);
                }
                else
                {
                    // Valid for longer than 30 days
                    cert.ExpiryDate = now.AddDays(Random.Shared.Next(31, 365));
                }


                await DatabaseManager.InsertCertRecord(cert);
            }
        }
    }
}
