namespace Backend.Persistence.Entities;

public sealed class ApplicationSetting
{
    public long Id { get; set; }

    public Guid ProfileId { get; set; }

    public required string Key { get; set; }

    public required string ValueJson { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public LocalProfile Profile { get; set; } = null!;
}