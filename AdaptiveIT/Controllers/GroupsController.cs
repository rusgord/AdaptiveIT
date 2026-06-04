using AdaptiveIT.Data;
using AdaptiveIT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdaptiveIT.Controllers
{
    [Authorize(Roles = "Admin, Teacher")]
    public class GroupsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public GroupsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Groups.Include(g => g.Students).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Create(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _context.Groups.Add(new Group { Name = name });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var group = await _context.Groups
                .Include(g => g.Students)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group != null)
            {
                foreach (var student in group.Students)
                {
                    student.GroupId = null;
                }
                _context.Groups.Remove(group);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var group = await _context.Groups
                .Include(g => g.Students)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();

            return View(group);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveStudent(int groupId, string studentId)
        {
            var student = await _userManager.FindByIdAsync(studentId);

            if (student != null && student.GroupId == groupId)
            {
                student.GroupId = null;
                await _userManager.UpdateAsync(student);
            }

            return RedirectToAction(nameof(Details), new { id = groupId });
        }
    }
}