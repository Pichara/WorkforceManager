namespace ElevatorMaintenanceSystem.Infrastructure;

public class WindowsUserContext : IUserContext
{
    public string GetCurrentUser()
    {
        if (!string.IsNullOrWhiteSpace(Environment.UserDomainName)
            && !string.Equals(Environment.UserDomainName, Environment.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return $"{Environment.UserDomainName}\\{Environment.UserName}";
        }

        return Environment.UserName;
    }
}
