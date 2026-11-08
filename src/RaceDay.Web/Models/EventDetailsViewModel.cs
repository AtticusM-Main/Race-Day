using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models;

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = new();
    public EnrolmentDto? MyEnrolment { get; set; }
}
