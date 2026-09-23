namespace Acme.Server.Domain;

/// <summary>
/// Application role. Persisted as the upper-case name in <c>app_user_roles.role</c>.
///
/// The constants carry no <c>ROLE_</c> prefix -- that was a Spring Security convention
/// applied when building authorities, and it does not survive the port. Authorization
/// here is expressed as the two policies in <c>Security/AuthorizationPolicies</c>.
/// </summary>
public enum Role
{
    /// <summary>May change anything.</summary>
    Admin,

    /// <summary>May read everything, and create or edit orders.</summary>
    Staff,
}

public static class RoleExtensions
{
    public static string ToDbValue(this Role role) => role.ToString().ToUpperInvariant();

    public static string Label(this Role role) => role switch
    {
        Role.Admin => "Administrator",
        Role.Staff => "Staff",
        _ => role.ToString(),
    };

    public static Role FromDbValue(string value) => Enum.Parse<Role>(value, ignoreCase: true);
}
