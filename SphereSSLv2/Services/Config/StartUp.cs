using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Diagnostics;
using SphereSSLv2.Testing;
using Microsoft.AspNetCore.SignalR;
using SphereSSLv2.Services.CertServices;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Data.Database;

namespace SphereSSLv2.Services.Config
{
    public class StartUp
    {
        private readonly Logger _logger;
        private readonly DatabaseManager _databaseManager;
        public StartUp(Logger logger, DatabaseManager databaseManager)
        {
            _logger = logger;
            _databaseManager = databaseManager;
        }

        public static WebApplication CreateWebApp(string[] args, int port)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Error);
            builder.Services.AddControllersWithViews();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<ConfigureService>();
            builder.Services.AddSingleton(provider =>
            {
                var hubContext = provider.GetRequiredService<IHubContext<SignalHub>>();
                return new Logger(hubContext);
            });

            builder.Services.AddHostedService<ExpiryWatcherService>();
            builder.Services.AddSingleton<DatabaseManager>();
            builder.Services.AddScoped<UserRepository>();
            builder.Services.AddScoped<HealthRepository>();
            builder.Services.AddScoped<CertRepository>();
            builder.Services.AddScoped<DnsProviderRepository>();
            builder.Services.AddScoped<ApiRepository>();

            // CORS Policy
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            builder.Services.AddRazorPages(options =>
            {
                options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
            });

            builder.Services.AddAuthorization();
            builder.Services.AddScoped<DatabaseManager>();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Parse(ConfigureService.ServerIP), port); // HTTP

                //options.Listen(IPAddress.Parse(ConfigureService.ServerIP), port -1, listen =>
                //{
                //    listen.UseHttps("c:/SphereVRF/spherevrf.pfx", "Empanada1030!");
                //});
            });

            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(15);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });

            var app = builder.Build();

            app.UseStaticFiles();
            app.UseSession();
            app.UseRouting();
            app.UseCors();
            app.UseAuthorization();



            app.Use(async (context, next) =>
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString();
                
                if (string.IsNullOrWhiteSpace(remoteIp) ||
                    !remoteIp.StartsWith("10.") &&
                     !remoteIp.StartsWith("192.168.") &&
                     !remoteIp.StartsWith("127."))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Forbidden");
                    return;
                }

                await next();
            });

            app.MapRazorPages();
            app.MapControllers();
            app.MapHub<SignalHub>("/logHub");


            return app;
        }

        public static async Task ConfigureApplication()
        {
            if (!File.Exists(ConfigureService.ConfigFilePath))
            {   
              
                File.Create(ConfigureService.ConfigFilePath).Close();
            }
            else
            {
              
                await ConfigureService.LoadConfigFile();
            }

            if (!File.Exists(Logger.LogFilePath))
            {
                File.Create(Logger.LogFilePath).Close();
            }
           
            await InitilizeDatabase();
            await StartTrayApp();
           
        }

        private static async Task StartTrayApp()
        {
            var processName = Path.GetFileNameWithoutExtension(ConfigureService.TrayAppPath);


            var existing = Process.GetProcessesByName(processName).FirstOrDefault();
            if (existing != null && !existing.HasExited)
            {

                try
                {

                    if (existing.MainModule.FileName != ConfigureService.TrayAppPath)
                    {
                        ConfigureService.TrayAppProcess = existing;
                        return;
                    }
                }
                catch { }


                return;
            }

            if (!File.Exists(ConfigureService.TrayAppPath))
            {
                
                return;
            }

            ConfigureService.TrayAppProcess = new Process();
            ConfigureService.TrayAppProcess.StartInfo.FileName = ConfigureService.TrayAppPath;
            ConfigureService.TrayAppProcess.StartInfo.UseShellExecute = true;
            ConfigureService.TrayAppProcess.Start();
        }

        private static async Task InitilizeDatabase()
        {
     
            var now = DateTime.UtcNow;

            await DatabaseManager.Initialize();
            await HealthRepository.RecalculateHealthStats();

            //for testing (remove later)
            int dbSize = await HealthRepository.GetTotalCertsInDB();
            if (dbSize==0 && ConfigureService.GenerateFakeTestCerts)
            {

                await TestingTools.GenerateFakeCertRecords();
            }
            else if (dbSize == 0 && !ConfigureService.GenerateFakeTestCerts)
            {
                await ConfigureService.SeedCertRecords();
            }

            if (ConfigureService.CertRecords.Count <= 1)
            {

                ConfigureService.ExpiredCertRecords = ConfigureService.CertRecords
                    .FindAll(cert => cert.ExpiryDate < now);
                ConfigureService.ExpiringSoonCertRecords = ConfigureService.CertRecords
                    .FindAll(cert => cert.ExpiryDate >= now && cert.ExpiryDate <= now.AddDays(30));
            }

       
        }

    }
}
