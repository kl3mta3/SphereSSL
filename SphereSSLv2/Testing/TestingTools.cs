using SphereSSLv2.Data.Database;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Services.Config;
using SphereSSLv2.Services.AcmeServices;

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
                    UserId = $"{i}{i}{i}{i}{i}{i}{i}{i}{i}{i}{i}{i}",
                    OrderId = AcmeService.GenerateCertRequestId(),
                    Domain = $"test{i}.example.com",
                    Email = $"user{i}@example.com",
                    DnsChallengeToken = $"token-{i}",
                    SavePath = $"/fake/path/cert{i}.pem",
                    Provider = ConfigureService.CapitalizeFirstLetter(fakeProvider),
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
                    await CertRepository.InsertExpiredCert(cert);
                }
                else if (i < 7)
                {
                    // Expiring soon (1–30 days from now)
                    cert.ExpiryDate = now.AddDays(Random.Shared.Next(1, 31));
                    cert.CreationDate = cert.ExpiryDate.AddDays(-90);
                    ConfigureService.ExpiringSoonCertRecords.Add(cert);
                }
                else
                {
                    // Valid (31–90 days from now)
                    cert.ExpiryDate = now.AddDays(Random.Shared.Next(31, 91));
                    cert.CreationDate = cert.ExpiryDate.AddDays(-90);
                }


                await CertRepository.InsertCertRecord(cert);
            }
        }
    }
}
