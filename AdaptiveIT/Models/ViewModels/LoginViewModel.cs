using System.ComponentModel.DataAnnotations;

namespace AdaptiveIT.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Введіть Email")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть пароль")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Запам'ятати мене?")]
        public bool RememberMe { get; set; }
    }
}