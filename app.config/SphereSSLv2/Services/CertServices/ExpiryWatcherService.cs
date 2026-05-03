using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Services;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Services.CertServices
{
    public class ExpiryWatcherService : BackgroundService
    {

        private  readonly Logger _logger;

        public ExpiryWatcherService(Logger logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _logger.Info("Refreshing cert list...");
                    await RefreshExpiringCertList();
                }
                catch (Exception ex)
                {
                    await _logger.Error($"Failed to refresh expiring cert list. {ex.Message}");
                }

                // Always delay, even after a caught error!
                await Task.Delay(TimeSpan.FromHours(ConfigureService.RefreshExpiringSoonRateInHours), stoppingToken);
            }
        }

        internal async Task RefreshExpiringCertList()
        {
            try
            {
                var now = DateTime.UtcNow;
                List<CertRecord> certRecords = await CertRepository.GetAllCertRecords();

                // Find all certs expiring within the window
                ConfigureService.ExpiringSoonCertRecords = certRecords
                    .FindAll(cert => cert.ExpiryDate >= now && cert.ExpiryDate <= now.AddDays(ConfigureService.RenewBeforeExpiryDays));

                var userRepo = new UserRepository(new DnsProviderRepository());

                // PreExpiry notification — fire for every cert in the window (regardless of autoRenew)
                foreach (var cert in ConfigureService.ExpiringSoonCertRecords)
                {
                    var domains = string.Join(", ", cert.Challenges.Select(c => c.Domain).Distinct());
                    var daysLeft = (int)(cert.ExpiryDate - now).TotalDays;
                    var ownerName = await userRepo.GetUsernameByIdAsync(cert.UserId) ?? "unknown";
                    _ = NotificationService.NotifyOwnerAndAdminsAsync(cert.UserId, "PreExpiry",
                        $"SphereSSL: certificate for {domains} (owner: {ownerName}) expires in {daysLeft} day(s).");
                }

                // Only certs with autoRenew enabled
                List<CertRecord> expiringSoon = ConfigureService.ExpiringSoonCertRecords
                    .Where(c => c.autoRenew)
                    .ToList();

                bool success = true;
                foreach (var cert in expiringSoon)
                {
                    var domains = string.Join(", ", cert.Challenges.Select(c => c.Domain).Distinct());
                    var ownerName = await userRepo.GetUsernameByIdAsync(cert.UserId) ?? "unknown";
                    _ = NotificationService.NotifyOwnerAndAdminsAsync(cert.UserId, "PreRenew",
                        $"SphereSSL: starting auto-renewal for {domains} (owner: {ownerName}).");

                    CertRecordServiceManager certManager = new CertRecordServiceManager();
                    // Attempt auto-renew (success is true only if all pass)
                    success &= await certManager.RenewCertRecordWithAutoDNSById(_logger, cert.OrderId);
                }

                if (success)
                {
                    await _logger.Info("Successfully refreshed expiring cert list.");
                }
                else
                {
                    await _logger.Error("Failed to refresh expiring cert list.");
                }
            }
            catch (Exception ex)
            {
                await _logger.Error("Error Refreshing cert list... " + ex.Message);
            }
        }
    }




}

