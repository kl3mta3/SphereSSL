using SphereSSL2.Model;
using SphereSSLv2.Data;

namespace SphereSSLv2.Testing
{
    public class TestingTools
    {

        public static async Task GenerateFakeCertRecords()
        {
            var now = DateTime.UtcNow;

             List<string> SupportedAutoProviders = Enum.GetValues(typeof(DNSProvider.ProviderType))
            .Cast<DNSProvider.ProviderType>()
            .Select(p => p.ToString())
            .ToList();

            
           


            for (int i = 0; i < 12; i++)
            {

                string fakeProvider = SupportedAutoProviders[Random.Shared.Next(SupportedAutoProviders.Count)];
                var cert = new CertRecord
                {
                    OrderId = AcmeService.GenerateCertRequestId(),
                    Domain = $"test{i}.example.com",
                    Email = $"user{i}@example.com",
                    DnsChallengeToken = $"token-{i}",
                    SavePath = $"/fake/path/cert{i}.pem",
                    Provider = Spheressl.CapitalizeFirstLetter(fakeProvider),
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
                };

                // Set the expiry date based on index to create a mix
                if (i < 3)
                {
                    // Expired (1–29 days ago)
                    cert.ExpiryDate = now.AddDays(-Random.Shared.Next(1, 30));
                    cert.CreationDate = cert.ExpiryDate.AddDays(-90);
                    await DatabaseManager.InsertExpiredCert(cert);
                }
                else if (i < 7)
                {
                    // Expiring soon (1–30 days from now)
                    cert.ExpiryDate = now.AddDays(Random.Shared.Next(1, 31));
                    cert.CreationDate = cert.ExpiryDate.AddDays(-90);
                    Spheressl.ExpiringSoonCertRecords.Add(cert);
                }
                else
                {
                    // Valid (31–90 days from now)
                    cert.ExpiryDate = now.AddDays(Random.Shared.Next(31, 91));
                    cert.CreationDate = cert.ExpiryDate.AddDays(-90);
                }


                await DatabaseManager.InsertCertRecord(cert);
            }
        }
    }
}
