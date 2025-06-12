using SphereSSL2.View;
using SphereSSLv2.Services;
using SphereSSLv2.Data;
public class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += Spheressl.OnProcessExit;
        await StartUp.ConfigureApplication();
        WebApplication app = StartUp.CreateWebApp(args, Spheressl.ServerPort);
        var webApp = app.RunAsync();
        await webApp;

    }



}
