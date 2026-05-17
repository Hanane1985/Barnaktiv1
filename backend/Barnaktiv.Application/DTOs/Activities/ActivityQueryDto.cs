namespace Barnaktiv.Application.DTOs.Activities;

public sealed class ActivityQueryDto
{
    public string? Search { get; set; }

    public string? City { get; set; }

    public string? Organizer { get; set; }

    public string? Sport { get; set; }

    public string? Category { get; set; }

    public int? MinAge { get; set; }

    public int? MaxAge { get; set; }

    public string? Price { get; set; }

    public string? Sort { get; set; }

    public int? Skip { get; set; }

    public int? Take { get; set; }
}
