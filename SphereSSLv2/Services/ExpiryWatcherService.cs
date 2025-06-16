using SphereSSLv2.Data;

namespace SphereSSLv2.Services
{
    public class ExpiryWatcherService : BackgroundService
    {

        private readonly Logger _logger;

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
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (Exception ex)
                {
                    // log error
                }
            }
        }

        private async Task RefreshExpiringCertList()
        {
            while (true)
            {
                try
                {
                    var now = DateTime.UtcNow;

                    Spheressl.ExpiringSoonCertRecords = Spheressl.CertRecords
                        .FindAll(cert => cert.ExpiryDate >= now && cert.ExpiryDate <= now.AddDays(Spheressl.ExpiringNoticePeriodInDays));
                }
                catch (Exception ex)
                {
                    await _logger.Error(" Error Refreshing cert list...");
                }

                await Task.Delay(TimeSpan.FromMinutes(Spheressl.RefreshExpiringSoonRateInMinutes));
            }
        }
    }
}
