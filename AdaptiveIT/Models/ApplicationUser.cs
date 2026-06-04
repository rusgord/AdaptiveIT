using Microsoft.AspNetCore.Identity;

namespace AdaptiveIT.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        public int? CurrentSkillLevel { get; set; } = 1;

        public int? GroupId { get; set; }
        public string? AvatarUrl { get; set; } = "/uploads/avatars/default-avatar.png";
        public Group? Group { get; set; }
    }
}