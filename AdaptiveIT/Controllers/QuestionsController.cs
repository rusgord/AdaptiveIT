using AdaptiveIT.Data;
using AdaptiveIT.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdaptiveIT.Helpers;

namespace AdaptiveIT.Controllers
{
    [Authorize(Roles = "Teacher, Admin")]
    public class QuestionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public QuestionsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index(int testId)
        {
            var test = await _context.Tests.FindAsync(testId);
            if (test == null) return NotFound();

            ViewBag.TestTitle = test.Title;
            ViewBag.TestId = testId;

            var questions = await _context.Questions
                .Where(q => q.TestId == testId)
                .ToListAsync();

            return View(questions);
        }

        public IActionResult Create(int testId)
        {
            var model = new Question { TestId = testId, DifficultyLevel = 1 };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TestId,Text,Type,DifficultyLevel,Points")] Question question, IFormFile? uploadFile)
        {
            if (ModelState.IsValid)
            {
                if (uploadFile != null && uploadFile.Length > 0)
                {
                    question.ImageUrl = await ImageHelper.ProcessAndSaveImageAsync(uploadFile, _webHostEnvironment.WebRootPath, "questions", 800);
                }
                _context.Add(question);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index), new { testId = question.TestId });
            }
            return View(question);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var question = await _context.Questions.FindAsync(id);
            if (question == null) return NotFound();

            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,TestId,Text,Type,DifficultyLevel,ImageUrl,Points")] Question question, IFormFile? uploadFile)
        {
            if (id != question.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        var existingQuestion = await _context.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == question.Id);
                        ImageHelper.DeleteImageFile(existingQuestion?.ImageUrl, _webHostEnvironment.WebRootPath);
                        question.ImageUrl = await ImageHelper.ProcessAndSaveImageAsync(uploadFile, _webHostEnvironment.WebRootPath, "questions", 800);
                    }
                    _context.Update(question);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Questions.Any(e => e.Id == question.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index), new { testId = question.TestId });
            }
            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var question = await _context.Questions.FindAsync(id);
            if (question != null)
            {
                int testId = question.TestId;
                ImageHelper.DeleteImageFile(question.ImageUrl, _webHostEnvironment.WebRootPath);
                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { testId = testId });
            }
            return RedirectToAction("Index", "Tests");
        }


        public async Task<IActionResult> ManageAnswers(int id)
        {
            var question = await _context.Questions
                .Include(q => q.AnswerOptions)
                .Include(q => q.GraphicMatchZones)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return NotFound();

            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAnswerOption(int questionId, string text, bool isCorrect, string? matchTarget)
        {
            var answer = new AnswerOption
            {
                QuestionId = questionId,
                Text = text,
                IsCorrect = isCorrect,
                MatchTarget = matchTarget
            };

            _context.AnswerOptions.Add(answer);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ManageAnswers), new { id = questionId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAnswerOption(int id, int questionId)
        {
            var answer = await _context.AnswerOptions.FindAsync(id);
            if (answer != null)
            {
                _context.AnswerOptions.Remove(answer);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageAnswers), new { id = questionId });
        }


        [HttpPost]
        public async Task<IActionResult> AddGraphicZone(int questionId, double positionX, double positionY, string correctFragment)
        {
            var zone = new GraphicMatchZone
            {
                QuestionId = questionId,
                PositionX = Math.Round(positionX, 2),
                PositionY = Math.Round(positionY, 2),
                Width = 15,
                Height = 10,
                CorrectFragment = correctFragment
            };

            _context.GraphicMatchZones.Add(zone);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ManageAnswers), new { id = questionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAnswerOption(AnswerOption answerOption)
        {
            if (ModelState.IsValid)
            {
                var question = await _context.Questions
                    .Include(q => q.AnswerOptions)
                    .FirstOrDefaultAsync(q => q.Id == answerOption.QuestionId);

                if (question == null) return NotFound();
                if (question.Type == QuestionType.SingleChoice && answerOption.IsCorrect)
                {
                    foreach (var opt in question.AnswerOptions!)
                    {
                        if (opt.Id != answerOption.Id)
                        {
                            opt.IsCorrect = false;
                        }
                    }
                }

                if (answerOption.Id == 0)
                {
                    _context.AnswerOptions.Add(answerOption);
                }
                else
                {
                    var existingOption = question.AnswerOptions!.FirstOrDefault(a => a.Id == answerOption.Id);
                    if (existingOption != null)
                    {
                        existingOption.Text = answerOption.Text;
                        existingOption.IsCorrect = answerOption.IsCorrect;
                        existingOption.MatchTarget = answerOption.MatchTarget;
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ManageAnswers), new { id = answerOption.QuestionId });
            }
            return RedirectToAction(nameof(ManageAnswers), new { id = answerOption.QuestionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGraphicZone(GraphicMatchZone zone)
        {
            if (ModelState.IsValid)
            {
                zone.Width = zone.Width == 0 ? 15 : zone.Width;
                zone.Height = zone.Height == 0 ? 10 : zone.Height;

                if (zone.Id == 0)
                {
                    _context.GraphicMatchZones.Add(zone);
                }
                else
                {
                    _context.Update(zone);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ManageAnswers), new { id = zone.QuestionId });
            }
            return RedirectToAction(nameof(ManageAnswers), new { id = zone.QuestionId });
        }


        [HttpPost]
        public async Task<IActionResult> DeleteGraphicZone(int id, int questionId)
        {
            var zone = await _context.GraphicMatchZones.FindAsync(id);
            if (zone != null)
            {
                _context.GraphicMatchZones.Remove(zone);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageAnswers), new { id = questionId });
        }
    }
}