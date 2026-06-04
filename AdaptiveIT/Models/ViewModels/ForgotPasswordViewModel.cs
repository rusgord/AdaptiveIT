using System.ComponentModel.DataAnnotations;

namespace AdaptiveIT.Models.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Введіть Email")]
        [EmailAddress(ErrorMessage = "Невірний формат Email")]
        public string Email { get; set; } = string.Empty;
    }
}   