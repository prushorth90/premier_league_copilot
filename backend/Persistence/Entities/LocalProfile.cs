namespace Backend.Persistence.Entities;

public sealed class LocalProfile
{
    public Guid Id { get; set; }

    public required string DisplayName { get; set; }

    public int? SelectedFplTeamId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ApplicationSetting> Settings { get; set; } = [];
}