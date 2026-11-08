using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models;

public class EventFilterModel
{
    [Display(Name = "Search")]
    [StringLength(100)]
    public string? Search { get; set; }

    [Display(Name = "Event type")]
    public EventType? Type { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "From")]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "To")]
    public DateTime? To { get; set; }

    public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || Type is not null || From is not null || To is not null;

    public EventFilter ToApiFilter() => new()
    {
        Search = Search,
        Type = Type,
        From = From,
        To = To,
        UpcomingOnly = true
    };
}

public class EventListViewModel
{
    public EventFilterModel Filter { get; set; } = new();
    public List<EventDto> Events { get; set; } = new();
}
