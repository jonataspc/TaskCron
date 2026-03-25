using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Quartz;
using Quartz.Impl;
using Serilog;
using System.Collections.Specialized;

namespace TaskCron;

internal sealed class CronWorker(IConfiguration config) : BackgroundService
{
    private readonly IConfiguration _config = config;
    private IScheduler? _scheduler;
    private FileSystemWatcher? _watcher;
    private string _cronFile = string.Empty;
    private string _logDir = string.Empty;
    private int _reloading;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _cronFile = _config["CronSettings:CronFile"]
            ?? throw new InvalidOperationException("Missing configuration: CronSettings:CronFile");

        _logDir = _config["CronSettings:LogDir"]
            ?? throw new InvalidOperationException("Missing configuration: CronSettings:LogDir");

        string threadCount = _config["Quartz:ThreadPool:ThreadCount"]
            ?? throw new InvalidOperationException("Missing configuration: Quartz:ThreadPool:ThreadCount");

        Directory.CreateDirectory(_logDir);

        var props = new NameValueCollection
        {
            ["quartz.threadPool.threadCount"] = threadCount
        };

        var schedulerFactory = new StdSchedulerFactory(props);
        _scheduler = await schedulerFactory.GetScheduler();

        await _scheduler.Start();
        await CronHelpers.LoadJobs(_cronFile, _scheduler);

        _watcher = new FileSystemWatcher(Path.GetDirectoryName(_cronFile)!, Path.GetFileName(_cronFile)!)
        {
            NotifyFilter = NotifyFilters.LastWrite
        };

        _watcher.Changed += OnCronFileChanged;
        _watcher.EnableRaisingEvents = true;

        Log.Information("TaskCron started");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher != null)
        {
            _watcher.Changed -= OnCronFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        if (_scheduler != null)
        {
            await _scheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
            _scheduler = null;
        }

        await base.StopAsync(cancellationToken);
    }

    private void OnCronFileChanged(object sender, FileSystemEventArgs e)
    {
        _ = ReloadJobsAsync();
    }

    private async Task ReloadJobsAsync()
    {
        if (_scheduler is null)
        {
            return;
        }

        if (Interlocked.Exchange(ref _reloading, 1) == 1)
        {
            return;
        }

        try
        {
            await Task.Delay(1000);
            await CronHelpers.LoadJobs(_cronFile, _scheduler);
        }
        finally
        {
            Interlocked.Exchange(ref _reloading, 0);
        }
    }
}