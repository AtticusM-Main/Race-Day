using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models.Api;

public class RegisterRequest
{
    [Required(ErrorMessage = "Enter your full name."), StringLength(100, MinimumLength = 2, ErrorMessage = "Your name must be 2 to 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Use an uppercase letter, a lowercase letter and a digit.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password."), Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose how you will use RaceDay.")]
    [Display(Name = "I am a")]
    public string Role { get; set; } = "Participant";
}

public class EventRequest
{
    [Required(ErrorMessage = "Enter the event name."), StringLength(150, MinimumLength = 3, ErrorMessage = "The name must be 3 to 150 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "The description can be at most 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose the date and start time.")]
    [Display(Name = "Date and start time")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Enter the location."), StringLength(150, MinimumLength = 2, ErrorMessage = "The location must be 2 to 150 characters.")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the main distance in km."), Range(0.1, 999.99, ErrorMessage = "The distance must be between 0.1 and 999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? Distance { get; set; }

    [Required(ErrorMessage = "Choose an event type.")]
    [Display(Name = "Event type")]
    public EventType? EventType { get; set; }
}

public class CategoryRequest
{
    [Required(ErrorMessage = "Enter a category name."), StringLength(100, ErrorMessage = "The name can be at most 100 characters.")]
    [Display(Name = "Category name")]
    public string Name { get; set; } = string.Empty;

    [Range(0.1, 999.99, ErrorMessage = "The distance must be between 0.1 and 999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? Distance { get; set; }

    [Range(1, 100000, ErrorMessage = "The limit must be at least 1.")]
    [Display(Name = "Maximum participants")]
    public int? MaxParticipants { get; set; }
}

public class UpdateProfileRequest
{
    [Required(ErrorMessage = "Enter your full name."), StringLength(100, MinimumLength = 2, ErrorMessage = "Your name must be 2 to 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(150)]
    public string Email { get; set; } = string.Empty;
}

public class ResultRequest
{
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }

    [Required(ErrorMessage = "Enter the finish time as hh:mm:ss.")]
    [Display(Name = "Finish time")]
    public TimeSpan? FinishTime { get; set; }

    [Range(1, 100000, ErrorMessage = "The position must be 1 or higher.")]
    public int? Position { get; set; }
}
