using AdaptiveIT.Data;
using AdaptiveIT.Models;
using AdaptiveIT.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdaptiveIT.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context, IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailSender = emailSender;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    string userRole = (model.Role == "Teacher") ? "Teacher" : "Student";
                    await _userManager.AddToRoleAsync(user, userRole);
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var confirmationLink = Url.Action("ConfirmEmail", "Account",
                        new { userId = user.Id, token = token }, Request.Scheme);

                    string emailBody = $@"
                        <div style='font-family: sans-serif; padding: 40px; background-color: #0f172a; color: #f8fafc; border-radius: 16px; border: 1px solid #334155; text-align: center; max-width: 600px; margin: 0 auto;'>
                            <h2 style='color: #f8fafc; margin-bottom: 10px;'>👋 Вітаємо в AdaptiveIT!</h2>
                            <p style='color: #94a3b8; font-size: 16px; margin-bottom: 30px; line-height: 1.5;'>
                                Дякуємо за реєстрацію, {model.FullName}. Щоб завершити створення акаунта та отримати доступ до тестувань, будь ласка, підтвердіть свою електронну пошту.
                            </p>
                            <a href='{confirmationLink}' style='display: inline-block; background-color: #6366f1; color: #ffffff; padding: 14px 32px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; box-shadow: 0 4px 6px rgba(99, 102, 241, 0.2);'>
                                Підтвердити пошту
                            </a>
                            <p style='color: #64748b; font-size: 12px; margin-top: 40px;'>
                                Якщо ви не реєструвалися на платформі AdaptiveIT, просто проігноруйте цей лист.
                            </p>
                        </div>";

                    await _emailSender.SendEmailAsync(user.Email, "Підтвердження реєстрації - AdaptiveIT", emailBody);

                    TempData["SuccessMessage"] = "Реєстрація майже завершена! На вашу пошту відправлено лист із посиланням для підтвердження.";
                    return RedirectToAction("Login", "Account");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && !await _userManager.IsEmailConfirmedAsync(user))
                {
                    ModelState.AddModelError(string.Empty, "Будь ласка, підтвердіть вашу пошту перед входом. Перевірте вхідні повідомлення.");
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Невірний логін або пароль.");
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null) return RedirectToAction("Index", "Home");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var result = await _userManager.ConfirmEmailAsync(user, token);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Пошту успішно підтверджено! Тепер ви можете увійти до системи.";
            }
            else
            {
                TempData["ErrorMessage"] = "Помилка підтвердження пошти. Можливо, посилання застаріло.";
            }

            return RedirectToAction("Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    TempData["SuccessMessage"] = "Якщо ваш email зареєстрований, ви отримаєте лист із посиланням для скидання пароля.";
                    return RedirectToAction(nameof(Login));
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetLink = Url.Action("ResetPassword", "Account", new { email = model.Email, token = token }, Request.Scheme);

                string emailBody = $@"
            <div style='font-family: sans-serif; padding: 40px; background-color: #0f172a; color: #f8fafc; border-radius: 16px; border: 1px solid #334155; text-align: center; max-width: 600px; margin: 0 auto;'>
                <div style='font-size: 40px; margin-bottom: 20px;'>🔐</div>
                <h2 style='color: #f8fafc; margin-bottom: 10px;'>Відновлення пароля</h2>
                <p style='color: #94a3b8; font-size: 16px; margin-bottom: 30px; line-height: 1.5;'>
                    Ми отримали запит на скидання пароля для вашого акаунта. Натисніть кнопку нижче, щоб встановити новий пароль.
                </p>
                <a href='{resetLink}' style='display: inline-block; background-color: #6366f1; color: #ffffff; padding: 14px 32px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; box-shadow: 0 4px 6px rgba(99, 102, 241, 0.2);'>
                    Скинути пароль
                </a>
                <p style='color: #64748b; font-size: 12px; margin-top: 40px;'>
                    Якщо ви не робили цього запиту, просто проігноруйте цей лист. Ваш поточний пароль залишиться без змін.
                </p>
            </div>";

                await _emailSender.SendEmailAsync(model.Email, "Відновлення пароля - AdaptiveIT", emailBody);

                TempData["SuccessMessage"] = "Якщо ваш email зареєстрований, ви отримаєте лист із посиланням для скидання пароля.";
                return RedirectToAction(nameof(Login));
            }
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
            {
                TempData["ErrorMessage"] = "Недійсний токен для скидання пароля. Спробуйте ще раз.";
                return RedirectToAction("Index", "Home");
            }

            var model = new ResetPasswordViewModel { Token = token, Email = email };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                TempData["SuccessMessage"] = "Ваш пароль успішно змінено!";
                return RedirectToAction(nameof(Login));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Ваш пароль успішно змінено! Тепер ви можете увійти.";
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
    }
}