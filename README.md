# TaskCron

`TaskCron` is a lightweight Windows command scheduler built on `.NET 9` and `Quartz`.
It loads jobs from a text file, executes shell commands, enforces optional timeouts, writes execution logs, and reloads jobs automatically when the cron file changes.

## Features

- Linux-style 5-field cron expressions (`min hour day month dayOfWeek`)
- Per-job timeout (seconds)
- Command execution through `cmd.exe /c`
- Daily log files in a configured directory
- Auto-reload when the cron file is updated
- Runs as a Windows Service

## Requirements

- Windows
- .NET SDK/Runtime 9.0

## Configuration

Configuration is in `src/TaskCron/appsettings.json`:

- `CronSettings:CronFile`: Full path to the cron definition file
- `CronSettings:LogDir`: Full path to the log directory
- `Quartz:ThreadPool:ThreadCount`: Number of worker threads

Example:

```json
{
  "CronSettings": {
    "CronFile": "C:\\cron\\cron.txt",
    "LogDir": "C:\\cron\\logs"
  },
  "Quartz": {
    "ThreadPool": {
      "ThreadCount": "4"
    }
  }
}
```

## Cron file format

Each non-empty, non-comment line must follow:

`<cron>|<timeoutSeconds>|<command>`

- `cron`: 5-field Linux cron expression
- `timeoutSeconds`:
  - `0` = no timeout
  - `> 0` = kill process if it exceeds the timeout
- `command`: command executed by `cmd.exe /c`

Comment lines start with `#`.

Example `cron.txt`:

```txt
# Every day at 07:30
30 7 * * *|60|echo Good morning

# Every 15 minutes
*/15 * * * *|30|powershell -File C:\scripts\job.ps1
```

## Build and run (console)

From repository root:

```powershell
dotnet build .\src\TaskCron\TaskCron.csproj

dotnet run --project .\src\TaskCron\TaskCron.csproj
```

## Publish

```powershell
dotnet publish .\src\TaskCron\TaskCron.csproj -c Release -r win-x64 --self-contained false
```

Output folder (default):

`src\TaskCron\bin\Release\net9.0\win-x64\publish\`

## Install as Windows Service

Run PowerShell as Administrator:

```powershell
sc.exe create TaskCron binPath= "D:\TaskCron\bin\Release\net9.0\win-x64\publish\TaskCron.exe" start= auto
sc.exe start TaskCron
```

Useful commands:

```powershell
sc.exe query TaskCron
sc.exe stop TaskCron
sc.exe delete TaskCron
```

## Logs

Logs are written to `CronSettings:LogDir` using one file per day:

`yyyy-MM-dd.log`

Typical entries include:

- `Reload requested`
- `Loaded ...`
- `Reload finished`
- `OK (...)`
- `FAIL (...)`
- `TIMEOUT ...`
- `ERROR ...`

## Notes

- Ensure the service account has read/write access to:
  - cron file path
  - log directory
- `appsettings.json` must be present next to `TaskCron.exe` in the publish folder.
- Invalid cron lines are ignored if they do not match expected format.
