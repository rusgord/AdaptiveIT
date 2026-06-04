using AdaptiveIT.Data;
using AdaptiveIT.Helpers;
using AdaptiveIT.Models;
using AdaptiveIT.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AdaptiveIT.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProfileController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var user = await _context.Users
                .Include(u => u.Group)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            ViewBag.Groups = await _context.Groups.ToListAsync();

            ViewBag.History = await _context.TestAttempts
                .Include(a => a.Test)
                .Include(a => a.Answers)
                    .ThenInclude(ans => ans.Question)
                .Where(a => a.StudentId == user.Id && a.IsCompleted)
                .OrderByDescending(a => a.EndTime)
                .ToListAsync();

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                GroupId = user.GroupId,
                Group = user.Group,
                AvatarUrl = string.IsNullOrEmpty(user.AvatarUrl) ? "/uploads/avatars/default-avatar.png" : user.AvatarUrl
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInfo(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            ModelState.Remove("CurrentPassword");
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmPassword");

            if (ModelState.IsValid)
            {
                user.FullName = model.FullName;
                if (model.AvatarFile != null && model.AvatarFile.Length > 0)
                {
                    if (!string.IsNullOrEmpty(user.AvatarUrl) && !user.AvatarUrl.Contains("default"))
                    {
                        ImageHelper.DeleteImageFile(user.AvatarUrl, _webHostEnvironment.WebRootPath);
                    }
                    user.AvatarUrl = await ImageHelper.ProcessAndSaveImageAsync(model.AvatarFile, _webHostEnvironment.WebRootPath, "avatars", 400);
                }

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["ProfileSuccess"] = "Особисті дані успішно оновлено!";
                    return RedirectToAction(nameof(Index), new { activeTab = "info" });
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.Groups = await _context.Groups.ToListAsync();
            ViewBag.History = await _context.TestAttempts
                .Include(a => a.Test)
                .Include(a => a.Answers!)
                    .ThenInclude(ans => ans.Question)
                .Where(a => a.StudentId == user.Id && a.IsCompleted)
                .OrderByDescending(a => a.EndTime)
                .ToListAsync();

            return View("Index", model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                await PopulateProfileDataAsync(model, user);
                ViewBag.ActiveTab = "password";
                return View("Index", model);
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                await PopulateProfileDataAsync(model, user);
                ViewBag.ActiveTab = "password";
                return View("Index", model);
            }

            TempData["PasswordSuccess"] = "Пароль успішно змінено!";
            return RedirectToAction("Index", new { activeTab = "password" });
        }

        [HttpPost]
        public async Task<IActionResult> JoinGroup(string inviteCode)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var cleanCode = inviteCode?.Trim();

            var group = await _context.Groups.FirstOrDefaultAsync(g => g.InviteCode == cleanCode);
            if (group == null)
            {
                TempData["ProfileError"] = "Групу з таким кодом не знайдено!";
                return RedirectToAction("Index", new { activeTab = "info" });
            }

            user.GroupId = group.Id;
            await _userManager.UpdateAsync(user);

            TempData["ProfileSuccess"] = $"Ви успішно приєдналися до групи {group.Name}!";
            return RedirectToAction("Index", new { activeTab = "info" });
        }

        [HttpPost]
        public async Task<IActionResult> LeaveGroup()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                user.GroupId = null;
                await _userManager.UpdateAsync(user);
                TempData["ProfileSuccess"] = "Ви успішно покинули групу.";
            }
            return RedirectToAction("Index", new { activeTab = "info" });
        }
        private async Task PopulateProfileDataAsync(ProfileViewModel model, ApplicationUser user)
        {
            model.FullName = user.FullName ?? "Користувач";
            model.Email = user.Email ?? string.Empty;
            model.GroupId = user.GroupId;
            model.AvatarUrl = string.IsNullOrEmpty(user.AvatarUrl) ? "/uploads/avatars/default-avatar.png" : user.AvatarUrl;

            if (user.GroupId.HasValue)
            {
                model.Group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == user.GroupId);
            }

            ViewBag.History = await _context.TestAttempts
                .Include(a => a.Test)
                .Include(a => a.Answers)
                    .ThenInclude(ans => ans.Question)
                .Where(a => a.StudentId == user.Id && a.IsCompleted)
                .OrderByDescending(a => a.EndTime)
                .ToListAsync();
        }
    }
}