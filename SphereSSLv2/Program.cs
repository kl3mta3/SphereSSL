﻿using SphereSSLv2.Services.Config;
public class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += ConfigureService.OnProcessExit;
        await StartUp.ConfigureApplication();
        WebApplication app = StartUp.CreateWebApp(args, ConfigureService.ServerPort);
        var webApp = app.RunAsync();
        await webApp;

    }



}
