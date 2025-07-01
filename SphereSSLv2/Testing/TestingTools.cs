using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Services.Config;
using SphereSSLv2.Services.AcmeServices;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Services.Security.Auth;
using SphereSSLv2.Models.UserModels;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.Json;

namespace SphereSSLv2.Testing
{
    public class TestingTools
    {
        private static DnsProviderRepository _dnsProviderRepository = new();
        private static readonly UserRepository _userRepository = new(_dnsProviderRepository);
        public static async Task GenerateFakeCertRecords()
        {
            var now = DateTime.UtcNow;

             List<string> SupportedAutoProviders = Enum.GetValues(typeof(DNSProvider.ProviderType))
            .Cast<DNSProvider.ProviderType>()
            .Select(p => p.ToString())
            .ToList();

            for (int i = 0; i < 12; i++)
            {
                var fakeUserId = Guid.NewGuid().ToString("N");
                var fakeUser = new User
                {
                    UserId = fakeUserId,
                    Username = $"testuser{i}",
                    PasswordHash = PasswordService.HashPassword("testpass"),
                    Name = $"testuser{i}",
                    Email = $"testuser{i}@email.com",
                    CreationTime = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    UUID = Guid.NewGuid().ToString(),
                    Notes = $"Test User {i}"

                };

                var fakeRole = new UserRole
                {
                    UserId = fakeUserId,
                    Role = "Viewer",
                    IsAdmin = false,
                    IsEnabled = false,

                };

                var fakeStat = new UserStat
                {
                    UserId = fakeUserId,
                    TotalCerts = 0,
                    CertsRenewed = 0,
                    CertCreationsFailed = 0,
                    CertRenewalsFailed = 0,
                    LastCertCreated = null
                };

                await _userRepository.InsertUserintoDatabaseAsync(fakeUser);
                await _userRepository.InsertUserRoleAsync(fakeRole);
                await _userRepository.InsertUserStatAsync(fakeStat);

                string fakeProvider = SupportedAutoProviders[Random.Shared.Next(SupportedAutoProviders.Count)];
                DateTime creationDate = now.AddDays(-Random.Shared.Next(1, 100));
                var cert = new CertRecord
                {
                    UserId = fakeUserId,
                    OrderId = AcmeService.GenerateCertRequestId(),
                    Email = $"user{i}@example.com",
                    SavePath = $"/fake/path/cert{i}.pem",
                    UseSeparateFiles = i % 2 == 0,
                    SaveForRenewal = true,
                    autoRenew = i % 2 == 0,
                    FailedRenewals = Random.Shared.Next(0, 3),
                    SuccessfulRenewals = Random.Shared.Next(0, 10),
                    Signer = "MockSigner",
                    AccountID = $"acct-{i}",
                    OrderUrl = $"https://acme.fake/{i}",
                    ChallengeType = "dns-01",
                    Thumbprint = $"thumb-{i}",
                    Challenges = new List<AcmeChallenge>(),
                    CreationDate = creationDate,
                    ExpiryDate = creationDate.AddDays(90)
                };

                cert.Challenges = new List<AcmeChallenge>
                {
                    new AcmeChallenge
                    {
                        ChallengeId = Guid.NewGuid().ToString("N"),
                        OrderId = cert.OrderId,
                        UserId = cert.UserId,
                        AuthorizationUrl = $"https://acme.fake/authorize/{i}",
                        Domain =  $"test{i}.example.com",
                        DnsChallengeToken =  $"token-{i}",
                        Status = "Valid",
                        ZoneId = $"zone-{i}",
                        ProviderId=ConfigureService.CapitalizeFirstLetter(fakeProvider)
                    }
                };
               
                await CertRepository.InsertCertRecord(cert);
            }
        }
    }
}
