using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models;

public class OrganiserDashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();

    public int TotalEvents => Events.Count;
    public int TotalEnrolments => Events.Sum(e => e.TotalEnrolments);
    public List<EventDto> Upcoming => Events.Where(e => !e.HasPassed).OrderBy(e => e.EventDate).ToList();
    public EventDto? NextEvent => Upcoming.FirstOrDefault();
}

public class EventFormViewModel
{
    public int? EventId { get; set; }
    public string? CurrentBannerUrl { get; set; }
    public EventRequest Form { get; set; } = new();
    public IFormFile? Banner { get; set; }

    public bool IsEdit => EventId is not null;
}

public class CategoriesViewModel
{
    public EventDto Event { get; set; } = new();
    public CategoryRequest NewCategory { get; set; } = new();
}

public class CategoryEditViewModel
{
    public EventDto Event { get; set; } = new();
    public int CategoryId { get; set; }
    public CategoryRequest Form { get; set; } = new();
}

public class EventEnrolmentsViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}
