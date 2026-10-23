using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BoxingCoach.Web.Components;
using BoxingCoach.Web.Components.Account;
using BoxingCoach.Web.Data;
using BoxingCoach.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// The composition root is intentionally explicit so that the startup sequence remains easy to read,
// reason about, and maintain. This application follows the standard ASP.NET Core pattern of:
// 1. registering services and their lifetimes,
// 2. configuring the HTTP pipeline,
// 3. executing database bootstrap work before requests are served.

// Presentation: interactive server-side Razor components own rendering and user interaction.
// Blazor Server is chosen here because it allows a familiar component-based UI while keeping
// data and identity in the same ASP.NET Core host and database.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Security: authentication state flows into components, while ASP.NET Core Identity owns users,
// roles, cookies, token generation, and persistence in the shared application database.
// The Identity system is configured as a cookie-based app so that protected pages can authorize
// users without additional custom token infrastructure.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
// Persistence: components and services request short-lived contexts from the factory. This avoids
// retaining a non-thread-safe DbContext for the longer lifetime of a Blazor Server circuit.
// The factory pattern is important here because a Blazor Server circuit can survive across user
// actions, while EF Core contexts must be created and disposed for each operation.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Application and infrastructure services: recommendation policy is scoped to the user circuit;
// backup operations are singleton-scoped so their in-process lock serializes all callers.
// A singleton backup manager is appropriate because the lock governs file-system and SQLite backup
// actions that should not overlap within a single host. The recommendation service is scoped so
// it can read the user-specific profile data associated with the active circuit.
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddScoped<CoachRecommendationService>();
builder.Services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// HTTP pipeline: production errors are handled by a user-facing route, while development exposes
// EF Core migration diagnostics. Antiforgery protects interactive component form submissions.
// The custom "not-found" status page ensures friendly responses for route mismatches without
// exposing raw server error text to end users.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Endpoint composition: Identity contributes account-management endpoints and health checks provide
// an anonymous infrastructure probe without exposing application or user data.
// Anonymous health checks allow infrastructure monitoring such as load balancers and uptime
// checks to confirm the service is responding without requiring a user session.
app.MapAdditionalIdentityEndpoints();
app.MapHealthChecks("/healthz").AllowAnonymous();

// Startup gate: schema migration and baseline-data reconciliation complete before the host accepts
// requests, ensuring every request observes a database compatible with the running application.
// This pattern prevents the app from serving requests against an empty or partially initialized schema.
await SeedData.InitializeAsync(app.Services);
await app.RunAsync();
