using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AdaptiveIT.Models.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Ім'я не може бути порожнім")]
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int? GroupId { get; set; }

        public string? AvatarUrl { get; set; }
        public Group? Group { get; set; }

        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "Пароль має бути не менше {2} символів.", MinimumLength = 6)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Паролі не збігаються.")]
        public string? ConfirmPassword { get; set; }

        public IFormFile? AvatarFile { get; set; }
    }
}