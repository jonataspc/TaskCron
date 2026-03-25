using Quartz;
using Quartz.Impl.Matchers;
using Serilog;

namespace TaskCron;

internal static class CronHelpers
{
    private static string ConvertCronExpression(string cron)
    {
        var f = cron.Split(' ');

        if (f.Length == 5)
        {
            var min = f[0];
            var hour = f[1];
            var day = f[2];
            var month = f[3];
            var dow = f[4];

            if (day == "*")
                return $"0 {min} {hour} {day} {month} ?";
            else
                return $"0 {min} {hour} ? {month} {dow}";
        }

        return cron;
    }

    public static async Task LoadJobs(string cronFile, IScheduler scheduler)
    {
        Log.Information("Reload requested");

        await scheduler.Standby();

        var jobKeys = await scheduler.GetJobKeys(
            GroupMatcher<JobKey>.AnyGroup()
        );

        foreach (var key in jobKeys)
        {
            await scheduler.DeleteJob(key);
        }

        if (!File.Exists(cronFile))
        {
            Log.Fatal("cron file not found");

            await scheduler.Start();

            return;
        }

        var lines = await File.ReadAllLinesAsync(cronFile);

        int id = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith('#')) continue;

            var parts = line.Split('|', 3, StringSplitOptions.None);

            if (parts.Length < 3) continue;

            var cronLinux = parts[0].Trim();
            var timeout = int.Parse(parts[1].Trim());
            var cmd = parts[2].Trim();

            var cron = ConvertCronExpression(cronLinux);

            var job = JobBuilder.Create<Job>()
                .WithIdentity($"job{id}", "DEFAULT")
                .UsingJobData("cmd", cmd)
                .UsingJobData("timeout", timeout)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"trigger{id}", "DEFAULT")
                .WithCronSchedule(cron)
                .Build();

            await scheduler.ScheduleJob(job, trigger);

            Log.Information("Loaded {CronLinux} -> {Cron} | {Cmd}", cronLinux, cron, cmd);

            id++;
        }

        await scheduler.Start();
        Log.Information("Reload finished");
    }
}