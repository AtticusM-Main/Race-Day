using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

public class EventRequest
{
    /// <example>Pretoria Jacaranda Walk</example>
    [Required, StringLength(150, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    /// <example>A relaxed community walk under the jacaranda trees.</example>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>Date and start time of the event.</summary>
    /// <example>2026-12-05T07:00:00</example>
    [Required]
    public DateTime? EventDate { get; set; }

    /// <example>Pretoria, South Africa</example>
    [Required, StringLength(150, MinimumLength = 2)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Main distance of the event in km.</summary>
    /// <example>8.5</example>
    [Required, Range(0.1, 999.99)]
    public decimal? Distance { get; set; }

    /// <summary>One of: Run, Walk, Cycle.</summary>
    /// <example>Walk</example>
    [Required]
    public EventType? EventType { get; set; }
}

public class EventResponse
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal Distance { get; set; }
    public EventType EventType { get; set; }

    /// <summary>Public URL of the banner image in Azure Blob Storage, if one was uploaded.</summary>
    public string? BannerImageUrl { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<CategoryResponse> Categories { get; set; } = new();
}
