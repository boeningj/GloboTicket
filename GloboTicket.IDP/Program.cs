using GloboTicket.IDP;
using Microsoft.AspNetCore.Mvc.Razor.Infrastructure;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
        theme: AnsiConsoleTheme.Code)
    .CreateBootstrapLogger();

Log.Information("Starting up GloboTicket.IDP");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        // uncomment to write to Azure diagnostics stream
        //.WriteTo.File(
        //    @"D:\home\LogFiles\Application\identityserver.txt",
        //    fileSizeLimitBytes: 1_000_000,
        //    rollOnFileSizeLimit: true,
        //    shared: true,
        //    flushToDiskInterval: TimeSpan.FromSeconds(1))
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
            theme: AnsiConsoleTheme.Code)
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(ctx.Configuration));

    var app = builder
        .ConfigureServices()
        .ConfigurePipeline();
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception during startup");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}