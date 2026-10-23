# User Manual

## 1. Overview

Cornerwork is a personal boxing training assistant designed for athletes who want structured, personalized coaching without a large platform or complicated setup. The application combines three core capabilities:

- athlete profile customization,
- recommendation of workout plans based on personal goals,
- session logging and history review.

The product is intended for individual use, though it can also support a small group of users under the same deployment.

## 2. Getting Started

### Prerequisites

- .NET 10 SDK
- A browser with access to the local HTTPS endpoint served by the app

### Launching the application

From the repository root, run:

```powershell
dotnet tool restore
dotnet restore
dotnet run --project .\BoxingCoach.Web\BoxingCoach.Web.csproj
```

Once the app starts, open the URL printed to the terminal. The project uses ASP.NET Core Identity for sign-in and user account management.

## 3. Account Creation and Sign-In

1. Open the application in your browser.
2. Choose the registration flow.
3. Create an account with a valid email address and password.
4. Sign in to the app after registration.

After successful sign-in, the profile and workout features become available.

## 4. Athlete Profile Setup

The athlete profile is the central personalization input. It stores:

- experience level,
- preferred training days per week,
- preferred workout duration,
- primary training goal.

When you visit the profile screen, you can edit values to match your current level and schedule. These values directly influence the recommendation engine.

### Recommended use

- Use a lower experience level if you are newer to boxing.
- Align preferred minutes to realistic training time.
- Keep the goal narrow, such as Technique, Conditioning, Defense, Power, or Recovery.

## 5. Workout Recommendations

After saving a profile, the app evaluates the workout catalog and ranks plans according to:

1. focus match with the athlete's primary goal,
2. closeness of difficulty to the athlete's experience,
3. closeness of duration to the athlete's preferred minutes.

The top three plans are surfaced to the athlete. This keeps the recommendation flow quick and relevant while preserving the ability to review multiple options.

## 6. Logging A Workout

The workout logging flow typically includes:

- selecting a workout plan,
- entering actual time spent training,
- rating perceived effort,
- assigning a quality rating,
- entering notes or observations.

This history is important because it allows the athlete to measure whether training is improving over time.

### Notes

It is helpful to record:

- whether the workout felt easier or harder than expected,
- technical weaknesses that emerged,
- conditioning issues or missed cues,
- recovery quality after the session.

## 7. Training History

The history area presents the completed sessions that the athlete logged. Each entry can be reviewed to identify repeated patterns, preferred plan types, and workload consistency.

Good historical review questions include:

- Which session types produce the strongest quality scores?
- Are you over- or under-shooting your preferred duration?
- Which goals are improving most consistently?

## 8. Administrator Features

### Access model

Administrative tools are made available only to users in the `Administrator` role.

### Granting admin access

Add a verified user email in the `Admin:Emails` section of configuration:

```json
{
  "Admin": {
    "Emails": ["admin@example.com"]
  }
}
```

Restart the app or sign out and sign back in after granting the role so the updated claims are loaded.

### Backup and restore portal

The admin portal offers backup and restore operations for the app's SQLite database. This is useful for:

- creating a safety copy before a migration or manual change,
- restoring from a validated backup after an operational issue,
- preserving current data before a destructive restore action.

## 9. Security and Privacy Notes

- User accounts are managed through ASP.NET Core Identity.
- The app stores personal profile and workout history for the signed-in user.
- Database backups are created and restored by administrators only.
- The app does not provide a public training-data API.

## 10. Troubleshooting

### The app does not start

Check that the .NET 10 SDK is installed and that all NuGet packages restore successfully.

### User cannot access the admin portal

Confirm the user exists in the identity system and that their email is included in `Admin:Emails`. Then sign out and sign back in.

### A backup fails validation

The app validates backup files using SQLite integrity checks. If validation fails, the backup is not treated as trustworthy and should be investigated before restore.

## 11. Coaching Guidance

Cornerwork offers general boxing guidance and general training recommendations, not medical advice. Always train within your safe limits, use proper equipment, and seek formal coaching support for technical corrections or contact drills.

## 12. Quick Reference

| Area | What to do |
| --- | --- |
| Profile | Set goal, experience, days, and minutes |
| Recommendations | Review the top three plan matches |
| Workouts | Log actual time, effort, rating, and notes |
| History | Review past workouts for patterns |
| Admin | Use backup and restore functions carefully |

This manual covers the intended user journey from onboarding through recommendation and log review.
