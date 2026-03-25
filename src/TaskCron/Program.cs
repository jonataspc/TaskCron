using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TaskCron;

var builder = Host.CreateApplicationBuilder(args);

string logDir = builder.Configuration["CronSettings:LogDir"]
    ?? throw new InvalidOperationException("Missing configuration: CronSettings:LogDir");

Directory.CreateDirectory(logDir);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logDir, ".log"),
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "TaskCron";
});

builder.Services.AddHostedService<CronWorker>();

try
{
    var host = builder.Build();
    await host.RunAsync();
}
finally
{
    await Log.CloseAndFlushAsync();
}