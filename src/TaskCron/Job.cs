using Quartz;
using Serilog;
using System.Diagnostics;

namespace TaskCron;

[DisallowConcurrentExecution]
public class Job : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cmd = context.JobDetail.JobDataMap.GetString("cmd");
        var timeout = context.JobDetail.JobDataMap.GetInt("timeout");

        var sw = Stopwatch.StartNew();

        try
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c " + cmd)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            var p = Process.Start(psi);

            if (p is null)
            {
                Log.Error("ERROR start failed {Cmd}", cmd);
                return;
            }

            bool exited;

            if (timeout > 0)
            {
                exited = p.WaitForExit(timeout * 1000);

                if (!exited)
                {
                    p.Kill(true);

                    sw.Stop();

                    Log.Information("TIMEOUT {Elapsed} {Cmd}", sw.Elapsed, cmd);
                    return;
                }
            }
            else
            {
                await p.WaitForExitAsync();
            }

            sw.Stop();

            var code = p.ExitCode;

            if (code == 0)
            {
                Log.Information("OK ({Code}) {Elapsed} {Cmd}", code, sw.Elapsed, cmd);
            }
            else
            {
                Log.Information("FAIL ({Code}) {Elapsed} {Cmd}", code, sw.Elapsed, cmd);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Error(ex, "ERROR {Elapsed} {Cmd}", sw.Elapsed, cmd);
        }
    }
}