using Microsoft.AspNetCore.Mvc;
using System.Net;
using SphereSSLv2.Data;
using System.Diagnostics;
using SphereSSLv2.Testing;
using Microsoft.AspNetCore.SignalR;
using SphereSSL2.Model;

namespace SphereSSLv2.Services
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
            builder.Services.AddSingleton<Spheressl>();
            builder.Services.AddSingleton<Logger>(provider =>
            {
                var hubContext = provider.GetRequiredService<IHubContext<SignalHub>>();
                return new Logger(hubContext);
            });
            //builder.Services.AddScoped<AcmeService>();
            builder.Services.AddHostedService<ExpiryWatcherService>();
            builder.Services.AddSingleton<DatabaseManager>();
           
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
                options.Listen(IPAddress.Parse(Spheressl.ServerIP), port); // HTTP
                //options.Listen(IPAddress.Parse(WebAppIP), port + 1, listen =>
                //{
                //    listen.UseHttps();
                //});
            });

            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(15);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
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
                    (!remoteIp.StartsWith("10.") &&
                     !remoteIp.StartsWith("192.168.") &&
                     !remoteIp.StartsWith("127.")))
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
            if (!File.Exists(Spheressl.ConfigFilePath))
            {
                File.Create(Spheressl.ConfigFilePath).Close();
            }
            else
            {
                // await Spheressl.LoadConfigFile();
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
            var processName = Path.GetFileNameWithoutExtension(Spheressl.TrayAppPath);


            var existing = Process.GetProcessesByName(processName).FirstOrDefault();
            if (existing != null && !existing.HasExited)
            {

                try
                {

                    if (existing.MainModule.FileName != Spheressl.TrayAppPath)
                    {
                        Spheressl.TrayAppProcess = existing;
                        return;
                    }
                }
                catch { }


                return;
            }

            if (!File.Exists(Spheressl.TrayAppPath))
            {
                MessageBox.Show($"{Spheressl.TrayAppPath} not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Spheressl.TrayAppProcess = new Process();
            Spheressl.TrayAppProcess.StartInfo.FileName = Spheressl.TrayAppPath;
            Spheressl.TrayAppProcess.StartInfo.UseShellExecute = true;
            Spheressl.TrayAppProcess.Start();
        }

        private static async Task InitilizeDatabase()
        {
            var now = DateTime.UtcNow;

            await DatabaseManager.Initialize();
            await DatabaseManager.RecalculateHealthStats();

            Spheressl.DNSProviders = await DatabaseManager.GetAllDNSProviders();
            Spheressl.CertRecords = await DatabaseManager.GetAllCertRecords();


            //for testing (remove later)
            if (!Spheressl.CertRecords.Any() && Spheressl.GenerateFakeTestCerts)
            {

                await TestingTools.GenerateFakeCertRecords();
            }

            if (Spheressl.CertRecords.Count <= 1)
            {

                Spheressl.ExpiredCertRecords = Spheressl.CertRecords
                    .FindAll(cert => cert.ExpiryDate < now);
                Spheressl.ExpiringSoonCertRecords = Spheressl.CertRecords
                    .FindAll(cert => cert.ExpiryDate >= now && cert.ExpiryDate <= now.AddDays(30));
            }


        }

    }
}
