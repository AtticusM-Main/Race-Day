using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models;

public class ProfileViewModel
{
    public UserProfile Profile { get; set; } = new();
    public UpdateProfileRequest Form { get; set; } = new();
}
