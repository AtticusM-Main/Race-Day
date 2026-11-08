using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

/// <summary>
/// Converts entities to response DTOs. Callers must Include() the navigation properties used here.
/// </summary>
public static class Mappings
{
    public static UserProfileResponse ToProfile(this User user) => new()
    {
        UserId = user.UserId,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role.RoleName,
        ProfilePictureUrl = user.ProfilePictureUrl,
        CreatedAt = user.CreatedAt
    };

    public static CategoryResponse ToResponse(this Category category) => new()
    {
        CategoryId = category.CategoryId,
        EventId = category.EventId,
        Name = category.Name,
        Distance = category.Distance,
        MaxParticipants = category.MaxParticipants,
        EnrolledCount = category.Enrolments.Count
    };

    public static EventResponse ToResponse(this Event ev) => new()
    {
        EventId = ev.EventId,
        Name = ev.Name,
        Description = ev.Description,
        EventDate = ev.EventDate,
        Location = ev.Location,
        Distance = ev.Distance,
        EventType = ev.EventType,
        BannerImageUrl = ev.BannerImageUrl,
        OrganiserId = ev.OrganiserId,
        OrganiserName = ev.Organiser.FullName,
        CreatedAt = ev.CreatedAt,
        Categories = ev.Categories.OrderBy(c => c.CategoryId).Select(c => c.ToResponse()).ToList()
    };

    public static EnrolmentResponse ToResponse(this Enrolment enrolment) => new()
    {
        EnrolmentId = enrolment.EnrolmentId,
        ParticipantId = enrolment.ParticipantId,
        ParticipantName = enrolment.Participant.FullName,
        ParticipantEmail = enrolment.Participant.Email,
        EventId = enrolment.Category.EventId,
        EventName = enrolment.Category.Event.Name,
        EventDate = enrolment.Category.Event.EventDate,
        CategoryId = enrolment.CategoryId,
        CategoryName = enrolment.Category.Name,
        EnrolmentDate = enrolment.EnrolmentDate,
        Status = enrolment.Status,
        ResultId = enrolment.Result?.ResultId,
        FinishTime = enrolment.Result?.FinishTime,
        Position = enrolment.Result?.Position
    };

    public static ResultResponse ToResponse(this Result result, int totalFinishers = 0) => new()
    {
        ResultId = result.ResultId,
        EnrolmentId = result.EnrolmentId,
        ParticipantId = result.Enrolment.ParticipantId,
        ParticipantName = result.Enrolment.Participant.FullName,
        EventId = result.Enrolment.Category.EventId,
        EventName = result.Enrolment.Category.Event.Name,
        EventDate = result.Enrolment.Category.Event.EventDate,
        CategoryId = result.Enrolment.CategoryId,
        CategoryName = result.Enrolment.Category.Name,
        FinishTime = result.FinishTime,
        Position = result.Position,
        TotalFinishers = totalFinishers,
        RecordedAt = result.RecordedAt
    };
}
