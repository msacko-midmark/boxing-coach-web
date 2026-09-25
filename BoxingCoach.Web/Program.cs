using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BoxingCoach.Web.Components;
using BoxingCoach.Web.Components.Account;
using BoxingCoach.Web.Data;
using BoxingCoach.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Presentation: interactive server-side Razor components own rendering and user interaction.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Security: authentication state flows into components, while ASP.NET Core Identity owns users,
// roles, cookies, token generation, and persistence in the shared application database.
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
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddScoped<CoachRecommendationService>();
builder.Services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// HTTP pipeline: production errors are handled by a user-facing route, while development exposes
// EF Core migration diagnostics. Antiforgery protects interactive component form submissions.
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
app.MapAdditionalIdentityEndpoints();
app.MapHealthChecks("/healthz").AllowAnonymous();

// Startup gate: schema migration and baseline-data reconciliation complete before the host accepts
// requests, ensuring every request observes a database compatible with the running application.
await SeedData.InitializeAsync(app.Services);
await app.RunAsync();
