namespace HaberPlatform.Api.Entities;

public class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

