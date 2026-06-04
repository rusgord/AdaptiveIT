using System.ComponentModel.DataAnnotations;

namespace AdaptiveIT.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Введіть ПІБ")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть Email")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Підтвердіть пароль")]
        [Compare("Password", ErrorMessage = "Паролі не співпадають.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Оберіть роль")]
        public string Role { get; set; } = "Student";
    }
}