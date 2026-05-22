namespace MotionStudio.Core.Security;

public sealed class AuthService
{
    private readonly Dictionary<UserRole, string?> _passwords = new()
    {
        [UserRole.Operator] = null,
        [UserRole.Engineer] = "123456",
        [UserRole.Administrator] = "admin123"
    };

    public event EventHandler? RoleChanged;

    public UserRole CurrentRole { get; private set; } = UserRole.Engineer; 

    public bool Login(UserRole role, string? password)
    {
        if (!_passwords.TryGetValue(role, out var expectedPassword))
        {
            return false;
        }

        if (expectedPassword is not null && !string.Equals(expectedPassword, password, StringComparison.Ordinal))
        {
            return false;
        }

        if (CurrentRole != role)
        {
            CurrentRole = role;
            RoleChanged?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    public void Logout()
    {
        if (CurrentRole != UserRole.Operator)
        {
            CurrentRole = UserRole.Operator;
            RoleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool HasPermission(Permission permission)
    {
        return CurrentRole switch
        {
            UserRole.Administrator => true,
            UserRole.Engineer => permission is not Permission.ManagePermissions,
            UserRole.Operator => permission is Permission.StartProcess
                                 or Permission.StopProcess
                                 or Permission.EmergencyStop
                                 or Permission.ViewLogs,
            _ => false
        };
    }
}
