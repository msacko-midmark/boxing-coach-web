using Microsoft.AspNetCore.Identity;

namespace BoxingCoach.Web.Data;

/// <summary>
/// Provides the application-specific Identity user type used for authentication and authorization.
/// </summary>
/// <remarks>
/// Authentication data intentionally remains separate from coaching data. Athlete preferences
/// and workout history refer to the inherited Identity identifier through their <c>UserId</c>
/// properties, which keeps those domain records independent of sign-in implementation details
/// and avoids expanding the security-sensitive Identity schema.
/// </remarks>
public class ApplicationUser : IdentityUser
{
}

