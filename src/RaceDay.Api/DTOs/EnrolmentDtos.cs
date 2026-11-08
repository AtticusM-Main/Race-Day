using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

public class EnrolmentRequest
{
    /// <summary>The category (and therefore event) to enter.</summary>
    /// <example>1</example>
    [Required, Range(1, int.MaxValue)]
    public int? CategoryId { get; set; }
}

public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string ParticipantEmail { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime EnrolmentDate { get; set; }

    /// <summary>Pending until the Organiser confirms the entry.</summary>
    public EnrolmentStatus Status { get; set; }

    /// <summary>Set once the Organiser has recorded a result for this enrolment.</summary>
    public int? ResultId { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
}

public class UpdateEnrolmentStatusRequest
{
    /// <summary>One of: Pending, Confirmed.</summary>
    /// <example>Confirmed</example>
    [Required]
    public EnrolmentStatus? Status { get; set; }
}
