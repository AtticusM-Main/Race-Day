using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class CreateResultRequest
{
    /// <summary>The enrolment (participant + category) this result belongs to.</summary>
    /// <example>2</example>
    [Required, Range(1, int.MaxValue)]
    public int? EnrolmentId { get; set; }

    /// <summary>Finish time in hh:mm:ss format.</summary>
    /// <example>00:48:30</example>
    [Required]
    public TimeSpan? FinishTime { get; set; }

    /// <summary>Finishing position in the category.</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int? Position { get; set; }
}

public class UpdateResultRequest
{
    /// <summary>Finish time in hh:mm:ss format.</summary>
    /// <example>00:47:55</example>
    [Required]
    public TimeSpan? FinishTime { get; set; }

    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int? Position { get; set; }
}

public class ResultResponse
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan FinishTime { get; set; }
    public int? Position { get; set; }

    /// <summary>How many participants have a recorded result in this category, e.g. 47th out of 312.</summary>
    public int TotalFinishers { get; set; }
    public DateTime RecordedAt { get; set; }
}
