using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;

namespace GabsHybridApp.Shared.Services;

public static class AuthRoleExtensions
{
    public const string RoleViewer = "viewer";
    public const string RoleViewOnly = "viewonly";

    /// <summary>
    /// Checks whether the user is in a read-only viewer role.
    /// Administrators always retain full write and edit capabilities.
    /// </summary>
    public static bool IsViewOnly(this ClaimsPrincipal? principal)
    {
        if (principal == null || principal.Identity?.IsAuthenticated != true)
            return false;

        // System administrators always have full editing/deleting rights
        if (principal.IsInRole("administrator") || principal.IsInRole("admin") ||
            principal.IsInRole("Administrator") || principal.IsInRole("Admin"))
        {
            return false;
        }

        return principal.IsInRole(RoleViewer) ||
               principal.IsInRole(RoleViewOnly) ||
               principal.HasClaim(c => c.Type == ClaimTypes.Role &&
                   (string.Equals(c.Value, RoleViewer, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Value, RoleViewOnly, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Value, "view-only", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Checks if the user is authorized to perform write operations (not view-only).
    /// </summary>
    public static bool CanWrite(this ClaimsPrincipal? principal) => !IsViewOnly(principal);

    /// <summary>
    /// Evaluates the cascading AuthenticationState task once to determine if the current user is a viewer.
    /// </summary>
    public static async Task<bool> CheckIsViewOnlyAsync(this Task<AuthenticationState>? authStateTask)
    {
        if (authStateTask == null) return false;
        try
        {
            var state = await authStateTask;
            return state.User.IsViewOnly();
        }
        catch
        {
            return false;
        }
    }
}
