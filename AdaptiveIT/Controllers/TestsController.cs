using AdaptiveIT.Data;
using AdaptiveIT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdaptiveIT.Controllers
{
    [Authorize(Roles = "Teacher, Admin")]
    public class TestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TestsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var myTests = await _context.Tests
                .Where(t => t.TeacherId == user.Id)
                .ToListAsync();

            return View(myTests);
        }

        private async Task PopulateAccessData(int? testId = null)
        {
            ViewBag.Groups = await _context.Groups.ToListAsync();

            var students = await _userManager.GetUsersInRoleAsync("Student");
            ViewBag.Students = students.ToList();

            if (testId.HasValue)
            {
                ViewBag.SelectedGroups = await _context.TestGroupAccesses
                    .Where(ga => ga.TestId == testId.Value).Select(ga => ga.GroupId).ToListAsync();

                ViewBag.SelectedStudents = await _context.TestStudentAccesses
                    .Where(sa => sa.TestId == testId.Value).Select(sa => sa.StudentId).ToListAsync();
            }
            else
            {
                ViewBag.SelectedGroups = new List<int>();
                ViewBag.SelectedStudents = new List<string>();
            }
        }

        public async Task<IActionResult> Create()
        {
            var test = new Test();
            await PopulateAccessData();
            return View(test);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,TeacherId,Title,Description,MaxAttempts,AccessType,AccessCode,HasDateLimit,StartDate,EndDate,HasTimeLimit,TimeLimitMinutes,ShowAnswersOnFinish,QuestionsPerAttempt,IsAdaptive,ShuffleQuestions,ShuffleAnswers")] Test test, int[] selectedGroups, string[] selectedStudents)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                test.TeacherId = user!.Id;

                _context.Add(test);
                await _context.SaveChangesAsync();
                if (test.AccessType == AccessType.ByGroup && selectedGroups != null)
                {
                    foreach (var gId in selectedGroups)
                        _context.TestGroupAccesses.Add(new TestGroupAccess { TestId = test.Id, GroupId = gId });
                }
                else if (test.AccessType == AccessType.ByStudentList && selectedStudents != null)
                {
                    foreach (var sId in selectedStudents)
                        _context.TestStudentAccesses.Add(new TestStudentAccess { TestId = test.Id, StudentId = sId });
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await PopulateAccessData();
            return View(test);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var test = await _context.Tests.FirstOrDefaultAsync(t => t.Id == id && t.TeacherId == user!.Id);

            if (test == null) return NotFound();

            await PopulateAccessData(id);
            return View(test);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,TeacherId,Title,Description,MaxAttempts,AccessType,AccessCode,HasDateLimit,StartDate,EndDate,HasTimeLimit,TimeLimitMinutes,ShowAnswersOnFinish,QuestionsPerAttempt,IsAdaptive,ShuffleQuestions,ShuffleAnswers")] Test test, int[] selectedGroups, string[] selectedStudents)
        {
            if (id != test.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(test);

                    var oldGroups = _context.TestGroupAccesses.Where(g => g.TestId == test.Id);
                    _context.TestGroupAccesses.RemoveRange(oldGroups);

                    var oldStudents = _context.TestStudentAccesses.Where(s => s.TestId == test.Id);
                    _context.TestStudentAccesses.RemoveRange(oldStudents);

                    if (test.AccessType == AccessType.ByGroup && selectedGroups != null)
                    {
                        foreach (var gId in selectedGroups)
                            _context.TestGroupAccesses.Add(new TestGroupAccess { TestId = test.Id, GroupId = gId });
                    }
                    else if (test.AccessType == AccessType.ByStudentList && selectedStudents != null)
                    {
                        foreach (var sId in selectedStudents)
                            _context.TestStudentAccesses.Add(new TestStudentAccess { TestId = test.Id, StudentId = sId });
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Tests.Any(e => e.Id == test.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            await PopulateAccessData(id);
            return View(test);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var test = await _context.Tests.FirstOrDefaultAsync(t => t.Id == id && t.TeacherId == user!.Id);

            if (test != null)
            {
                _context.Tests.Remove(test);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Statistics(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var test = await _context.Tests.FirstOrDefaultAsync(t => t.Id == id && t.TeacherId == user!.Id);

            if (test == null) return NotFound();

            ViewBag.TestTitle = test.Title;

            var attempts = await _context.TestAttempts
                .Include(a => a.Student)
                .Where(a => a.TestId == id && a.IsCompleted)
                .OrderByDescending(a => a.FinalScore)
                .ToListAsync();

            return View(attempts);
        }

        public async Task<IActionResult> ReviewAttempt(int attemptId)
        {
            var attempt = await _context.TestAttempts
                .Include(a => a.Student)
                .Include(a => a.Test)
                .Include(a => a.Answers!)
                    .ThenInclude(ans => ans.Question)
                        .ThenInclude(q => q.AnswerOptions)
                .Include(a => a.Answers!)
                    .ThenInclude(ans => ans.Question)
                        .ThenInclude(q => q.GraphicMatchZones)
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null) return NotFound();

            return View(attempt);
        }

        [HttpPost]
        public async Task<IActionResult> SaveReview(int attemptId, IFormCollection form)
        {
            var attempt = await _context.TestAttempts
                .Include(a => a.Answers!)
                    .ThenInclude(ans => ans.Question)
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null) return NotFound();

            double totalMaxPoints = 0;
            double totalAwarded = 0;

            foreach (var answer in attempt.Answers!)
            {
                if (answer.Question == null) continue;
                totalMaxPoints += answer.Question.Points;
                if (form.TryGetValue($"points_{answer.Id}", out var pointValue))
                {
                    if (double.TryParse(pointValue.ToString().Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double awarded))
                    {
                        awarded = Math.Clamp(Math.Round(awarded, 2), 0, answer.Question.Points);
                        answer.AwardedPoints = awarded;
                        answer.IsCorrect = (awarded >= answer.Question.Points && answer.Question.Points > 0);
                        answer.IsManuallyGraded = true;
                    }
                }

                if (form.TryGetValue($"feedback_{answer.Id}", out var feedbackValue))
                {
                    string feedback = feedbackValue.ToString().Trim();
                    answer.TeacherFeedback = string.IsNullOrEmpty(feedback) ? null : feedback;
                }

                totalAwarded += answer.AwardedPoints;
            }

            attempt.FinalScore = totalMaxPoints > 0 ? Math.Round((totalAwarded / totalMaxPoints) * 100, 2) : 0;
            attempt.RequiresManualGrading = false;

            _context.Update(attempt);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Оцінки та коментарі успішно збережено!";

            return RedirectToAction(nameof(Statistics), new { id = attempt.TestId });
        }
        [HttpGet]
        public async Task<IActionResult> PendingReviews()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var pendingAttempts = await _context.TestAttempts
                .Include(a => a.Test)
                .Include(a => a.Student)
                .Where(a => a.Test!.TeacherId == user.Id && a.IsCompleted && a.RequiresManualGrading)
                .OrderBy(a => a.EndTime)
                .ToListAsync();

            return View(pendingAttempts);
        }
    }
}