namespace RaceDay.Api.Models;

public enum EnrolmentStatus
{
    Pending,
    Confirmed
}

/// <summary>
/// Links a participant to a category, and through the category to the event.
/// </summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.Now;
    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Pending;

    public User Participant { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Result? Result { get; set; }
}
