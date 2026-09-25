# Cornerwork Boxing Coach

Cornerwork is a personal boxing training companion built with .NET 10 Blazor and SQLite. It recommends sessions based on an athlete's goal, experience, and available time while keeping a private history of completed workouts.

## Features

- Local account registration and sign-in with ASP.NET Core Identity
- Personalized workout recommendations
- Seeded technique, conditioning, defense, power, and recovery plans
- Session logging with duration, effort, quality, and notes
- Per-user training history and dashboard totals
- Athlete profile controls for recommendation tuning
- SQLite persistence with automatic EF Core migrations
- Administrator-only database backup and restore portal
- Responsive Blazor Server interface
- Health endpoint at `/healthz`

## Project Structure

```text
BoxingCoach.Web.slnx
BoxingCoach.Web/
  Components/
    Account/             Identity UI
    Layout/              Application shell and navigation
    Pages/               Dashboard and training workflows
  Data/                  EF Core entities, migrations, and seed data
  Services/              Coaching recommendation logic
  wwwroot/               Application styling and static assets
BoxingCoach.Web.Tests/   MSTest service tests
.github/workflows/       GitHub Actions build and test pipeline
```

## Run Locally

Prerequisites:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

From the repository root:

```powershell
dotnet tool restore
dotnet restore
dotnet run --project .\BoxingCoach.Web\BoxingCoach.Web.csproj
```

Open the HTTPS URL printed by the application, register an account, and begin with the athlete profile page. The SQLite database is created as `boxing-coach.db` and is excluded from Git.

## Database Administration

The backup portal is available at `/admin/backups` to users in the `Administrator` role. Add registered account emails to configuration, then restart the application to grant the role:

```json
{
  "Admin": {
    "Emails": ["admin@example.com"]
  }
}
```

Use environment variables or deployment secrets in production, for example `Admin__Emails__0=admin@example.com`. Sign out and back in after the role is first granted so the authentication cookie includes the updated role.

Backups are stored in `App_Data/Backups` by default. Set `DatabaseBackups__Directory` to an absolute path to place them on durable storage outside the deployment directory. Each restore validates the selected SQLite file and creates a `pre-restore` safety snapshot before replacing current data. For multi-instance deployments, use a shared operation lock or restrict backup administration to one instance because the built-in operation lock is process-local.

## Database Changes

The repository pins `dotnet-ef` as a local tool. After changing an entity:

```powershell
dotnet tool run dotnet-ef migrations add <MigrationName> `
  --project .\BoxingCoach.Web\BoxingCoach.Web.csproj `
  --startup-project .\BoxingCoach.Web\BoxingCoach.Web.csproj
```

Migrations run automatically during application startup.

## Validation

```powershell
dotnet build .\BoxingCoach.Web.slnx --configuration Release
dotnet test .\BoxingCoach.Web.slnx --configuration Release --no-build
dotnet list .\BoxingCoach.Web.slnx package --vulnerable --include-transitive
```

## Coaching Disclaimer

Cornerwork provides general training suggestions, not medical advice. Train within your ability, use appropriate protective equipment, and work with a qualified coach for contact training.