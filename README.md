# Cornerwork Boxing Coach

Cornerwork is a boxing-focused training planner and workout tracker built with .NET 10, Blazor Server, ASP.NET Core Identity, and SQLite. The application helps athletes create a profile, receive personalized plan recommendations, log completed sessions, and review long-term training history.

## Documentation

- [User Manual](docs/user-manual.md)
- [Technical Specification](docs/technical-specification.md)

## Product Summary

Cornerwork blends three activities into one coaching workflow:

- Profile-based recommendation: each athlete sets goal, experience, preferred minutes, and training days.
- Plan discovery: the app ranks workout plans by focus match, difficulty fit, and duration fit.
- Workout logging: athletes record session intensity, perceived effort, and personal notes for trend analysis.

The app is designed for a single-user or small-team environment and emphasizes simplicity, privacy, and repeatable coaching decisions.

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run locally

```powershell
dotnet tool restore
dotnet restore
dotnet run --project .\BoxingCoach.Web\BoxingCoach.Web.csproj
```

Then browse to the HTTPS URL printed by the terminal, create an account, and visit the athlete profile page to begin customizing your training.

The default SQLite database is created automatically as a local file in the app directory. The repository intentionally keeps runtime data out of source control.

## Features

- Identity-based account creation and sign-in
- Personalized recommendation engine for technique, conditioning, defense, power, and recovery plans
- Seeded boxing training catalog with structured exercises and cues
- History dashboard with prior workout sessions and totals
- Goal, duration, and training schedule preferences per user
- Admin-only database backup and restore tools
- Responsive UI with Blazor Server interactive components
- Health endpoint at `/healthz`

## Default Admin Configuration

Administrative backup access is controlled by the `Admin:Emails` configuration section. Add a trusted email to enable the role after the account exists:

```json
{
  "Admin": {
    "Emails": ["admin@example.com"]
  }
}
```

In production, prefer environment variables or deployment secrets such as `Admin__Emails__0=admin@example.com`. Sign out and sign back in after the first role grant so the updated claims are loaded in the authentication cookie.

## Database Notes

- Backups are saved under `App_Data/Backups` by default.
- The `DatabaseBackups__Directory` setting can redirect backups to durable storage.
- Restores create a `pre-restore` snapshot before replacing current data.
- The backup lock is process-local and is not a distributed lock for multi-instance coordination.

## Validation

```powershell
dotnet build .\BoxingCoach.Web.slnx --configuration Release
dotnet test .\BoxingCoach.Web.slnx --configuration Release --no-build
```

## Script helpers

The repository includes a few PowerShell helpers in [scripts](scripts) for common local workflows.

### Clean build

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

This removes only the project’s known `bin` and `obj` folders, restores dependencies, and rebuilds the solution. It exits with an error if any step fails.

### Start the app

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run.ps1
```

This checks whether the project is already running, stops only that project process when present, starts it again, and confirms the app responds on the expected local URL.

### Run tests and export a PDF report

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

This runs the solution tests, saves the raw output and TRX results under `.test-results`, writes an HTML summary, and exports a PDF version of the report. The generated PDF is saved in the `.test-results` folder, with names like `test-report-YYYYMMDD-HHMMSS.pdf`.

> Requires a Chromium-based browser such as Microsoft Edge or Google Chrome to be installed on the machine.

### Assumptions and safety notes

- Cleanup is limited to the known project outputs in the app and test folders only; it does not delete the repository root, user profile, or a drive.
- The app restart logic identifies the project by its exact project file path in the `dotnet` command line and by the `BoxingCoach.Web.dll` host, so unrelated processes are not targeted.
- The app is started through its normal local development command: `dotnet run --project ... --launch-profile https`.
- Test reports are written to `.test-results` and the PDF export depends on a Chromium-based browser being installed.

### Latest generated report

The current generated PDF report is in [.test-results](.test-results), with the latest run saved as [.test-results/test-report-20261023-112544.pdf](.test-results/test-report-20261023-112544.pdf).

### Brief reflection

1. What did the AI-generated draft get right?
   - It correctly identified the project as a .NET 10 solution, proposed a safe minimal cleanup scope, and matched the app’s local run flow to `dotnet run` with a launch profile.

2. What did you need to correct or verify?
   - I corrected the script behavior to target only exact project outputs and project processes, verified the browser discovery logic for Windows installs under `Program Files (x86)`, and checked that exit codes and file creation were handled explicitly.

3. What safety or reliability issue did you consider most carefully?
   - The most important issue was avoiding broad or unintended deletion and process termination. I constrained cleanup to known output folders and limited process matching to the specific project path so the scripts would not kill unrelated programs.

## Coaching Disclaimer

Cornerwork provides general fitness and boxing recommendations, not medical advice. Train within your ability, use appropriate protective equipment, and consult a qualified coach for contact work or technical corrections.

## Repository Layout

```text
BoxingCoach.Web.slnx
BoxingCoach.Web/
  Components/          Blazor pages, layouts, and identity UI
  Data/                EF Core entities, migrations, and seed data
  Services/            Recommendations and backup operations
  wwwroot/             CSS and static assets
BoxingCoach.Web.Tests/
  MSTest validation suite
docs/
  user-manual.md
  technical-specification.md
```