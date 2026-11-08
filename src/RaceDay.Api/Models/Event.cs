namespace RaceDay.Api.Models;

public enum EventType
{
    Run,
    Walk,
    Cycle
}

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;

    // Main distance of the event in km (individual categories can have their own distance).
    public decimal Distance { get; set; }
    public EventType EventType { get; set; }
    public string? BannerImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public User Organiser { get; set; } = null!;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
