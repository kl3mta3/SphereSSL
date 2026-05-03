using SphereSSLv2.Services.Config;
public class Program
{
    public static async Task Main(string[] args)
    {
        await StartUp.ConfigureApplication();
        WebApplication app = StartUp.CreateWebApp(args, ConfigureService.ServerPort);
        var webApp = app.RunAsync();
        await webApp;

    }



}
