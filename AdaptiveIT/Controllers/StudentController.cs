using AdaptiveIT.Data;
using AdaptiveIT.Models;
using AdaptiveIT.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdaptiveIT.Controllers
{
    [Authorize]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var availableTests = await _context.Tests
            .Where(t => t.AccessType == AccessType.All ||
                        (t.AccessType == AccessType.ByGroup && _context.TestGroupAccesses.Any(ga => ga.TestId == t.Id && ga.GroupId == user.GroupId)) ||
                        ((t.AccessType == AccessType.ByStudentList || t.AccessType == AccessType.ByCode) && _context.TestStudentAccesses.Any(sa => sa.TestId == t.Id && sa.StudentId == user.Id)))
            .ToListAsync();

            var studentAttempts = await _context.TestAttempts
            .Include(a => a.Answers!)
                .ThenInclude(ans => ans.Question)
            .Where(a => a.StudentId == user.Id)
            .ToListAsync();

            var model = availableTests.Select(t => {
                var testAttempts = studentAttempts.Where(a => a.TestId == t.Id).ToList();
                var completedAttempts = testAttempts.Where(a => a.IsCompleted).ToList();
                var unfinished = testAttempts.FirstOrDefault(a => !a.IsCompleted);

                TestAttempt? bestAttempt = null;
                if (completedAttempts.Any())
                {
                    bestAttempt = completedAttempts.OrderByDescending(a => a.FinalScore).First();
                }

                bool isAvailable = true;
                string dateMsg = "";
                if (t.HasDateLimit)
                {
                    var now = DateTime.Now;
                    if (t.StartDate.HasValue && now < t.StartDate)
                    {
                        isAvailable = false;
                        dateMsg = $"Відкриється: {t.StartDate.Value:dd.MM.yyyy HH:mm}";
                    }
                    else if (t.EndDate.HasValue && now > t.EndDate)
                    {
                        isAvailable = false;
                        dateMsg = "Термін проходження завершився";
                    }
                    else if (t.EndDate.HasValue)
                    {
                        dateMsg = $"Доступний до: {t.EndDate.Value:dd.MM.yyyy HH:mm}";
                    }
                }

                return new StudentTestListViewModel
                {
                    TestId = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    MaxAttempts = t.MaxAttempts,
                    AttemptsUsed = testAttempts.Count,
                    IsCompleted = completedAttempts.Any(),
                    BestScore = bestAttempt?.FinalScore,
                    BestAttemptId = bestAttempt?.Id,
                    BestAwardedPoints = bestAttempt?.Answers?.Sum(a => a.AwardedPoints),
                    BestMaxPoints = bestAttempt?.Answers?.Sum(a => a.Question?.Points ?? 0),
                    ShowAnswersOnFinish = t.ShowAnswersOnFinish,
                    UnfinishedAttemptId = unfinished?.Id,
                    IsAvailableByDate = isAvailable,
                    DateLimitMessage = dateMsg,
                    TimeLimitMessage = t.HasTimeLimit ? $"⏳ Час: {t.TimeLimitMinutes} хв." : "Без обмеження часу"
                };
            }).ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> TestHistory(int testId)
        {
            var user = await _userManager.GetUserAsync(User);
            var test = await _context.Tests.FindAsync(testId);

            if (test == null) return NotFound();

            var attempts = await _context.TestAttempts
                .Include(a => a.Answers!)
                    .ThenInclude(ans => ans.Question)
                .Where(a => a.StudentId == user!.Id && a.TestId == testId && a.IsCompleted)
                .OrderByDescending(a => a.EndTime)
                .ToListAsync();

            ViewBag.TestTitle = test.Title;
            ViewBag.ShowAnswers = test.ShowAnswersOnFinish;

            return View(attempts);
        }

        [HttpPost]
        public async Task<IActionResult> StartTest(int testId)
        {
            var user = await _userManager.GetUserAsync(User);

            var unfinishedAttempt = await _context.TestAttempts.FirstOrDefaultAsync(a => a.TestId == testId && a.StudentId == user!.Id && !a.IsCompleted);

            if (unfinishedAttempt != null)
            {
                return RedirectToAction(nameof(TakeTest), new { attemptId = unfinishedAttempt.Id });
            }

            var test = await _context.Tests.FindAsync(testId);
            var attemptsUsed = await _context.TestAttempts.CountAsync(a => a.TestId == testId && a.StudentId == user!.Id);

            if (test == null || attemptsUsed >= test.MaxAttempts)
            {
                return RedirectToAction(nameof(Index));
            }

            var attempt = new TestAttempt
            {
                TestId = testId,
                StudentId = user!.Id,
                StartTime = DateTime.UtcNow,
                IsCompleted = false
            };

            _context.TestAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(TakeTest), new { attemptId = attempt.Id });
        }

        public async Task<IActionResult> TakeTest(int attemptId)
        {
            var attempt = await _context.TestAttempts
                .Include(a => a.Answers)
                .Include(a => a.Test)
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null || attempt.IsCompleted) return RedirectToAction(nameof(Index));

            var test = attempt.Test!;
            var allQuestions = await _context.Questions
                .Include(q => q.AnswerOptions)
                .Include(q => q.GraphicMatchZones)
                .Where(q => q.TestId == test.Id)
                .ToListAsync();

            var answeredIds = attempt.Answers?.Select(a => a.QuestionId).ToList() ?? new List<int>();
            var remainingQuestions = allQuestions.Where(q => !answeredIds.Contains(q.Id)).ToList();

            int limit = test.QuestionsPerAttempt.HasValue ? Math.Min(test.QuestionsPerAttempt.Value, allQuestions.Count) : allQuestions.Count;

            if (answeredIds.Count >= limit || !remainingQuestions.Any())
            {
                return RedirectToAction(nameof(FinishTest), new { attemptId = attempt.Id });
            }

            Question nextQuestion;

            if (test.IsAdaptive)
            {
                int targetDifficulty = 5;
                if (attempt.Answers != null && attempt.Answers.Any())
                {
                    var lastAnswer = attempt.Answers.OrderByDescending(a => a.Id).First();
                    var lastQuestion = allQuestions.First(q => q.Id == lastAnswer.QuestionId);
                    targetDifficulty = lastAnswer.IsCorrect ? lastQuestion.DifficultyLevel + 1 : lastQuestion.DifficultyLevel - 1;
                    targetDifficulty = Math.Clamp(targetDifficulty, 1, 10);
                }
                nextQuestion = remainingQuestions.OrderBy(q => Math.Abs(q.DifficultyLevel - targetDifficulty)).First();
            }
            else
            {
                if (test.ShuffleQuestions)
                    nextQuestion = remainingQuestions.OrderBy(q => Guid.NewGuid()).First();
                else
                    nextQuestion = remainingQuestions.OrderBy(q => q.Id).First();
            }

            if (test.HasTimeLimit)
            {
                var timePassed = (DateTime.UtcNow - attempt.StartTime).TotalMinutes;
                var minutesLeft = test.TimeLimitMinutes!.Value - timePassed;
                if (minutesLeft <= 0) return RedirectToAction(nameof(FinishTest), new { attemptId = attempt.Id });
                ViewBag.SecondsLeft = (int)(minutesLeft * 60);
            }
            else { ViewBag.SecondsLeft = -1; }

            ViewBag.AttemptId = attemptId;
            ViewBag.TotalQuestions = limit;
            ViewBag.CurrentQuestionIndex = answeredIds.Count + 1;

            return View(nextQuestion);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitAnswer(int attemptId, int questionId, IFormCollection form)
        {
            var question = await _context.Questions
                .Include(q => q.AnswerOptions)
                .Include(q => q.GraphicMatchZones)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null) return NotFound();

            bool isCorrect = false;
            string answerDetails = "";
            double awardedPoints = 0;
            bool isManuallyGraded = false;

            if (question.Type == QuestionType.SingleChoice)
            {
                var selectedOptionsValue = form["selectedOptions"];
                if (!string.IsNullOrEmpty(selectedOptionsValue))
                {
                    var selectedId = int.Parse(selectedOptionsValue.First());
                    var correctIds = question.AnswerOptions!.Where(a => a.IsCorrect).Select(a => a.Id).ToList();

                    isCorrect = correctIds.Contains(selectedId);
                    answerDetails = question.AnswerOptions!.First(a => a.Id == selectedId).Text;
                    awardedPoints = isCorrect ? question.Points : 0;
                }
                else { answerDetails = "Відповідь не надано"; }
            }
            else if (question.Type == QuestionType.MultipleChoice)
            {
                var selectedOptionsValue = form["selectedOptions"];
                if (!string.IsNullOrEmpty(selectedOptionsValue))
                {
                    var selectedIds = selectedOptionsValue.Select(int.Parse).ToList();
                    var correctIds = question.AnswerOptions!.Where(a => a.IsCorrect).Select(a => a.Id).ToList();

                    isCorrect = selectedIds.Count == correctIds.Count && !selectedIds.Except(correctIds).Any();
                    answerDetails = string.Join(", ", question.AnswerOptions!.Where(a => selectedIds.Contains(a.Id)).Select(a => a.Text));
                    if (correctIds.Any())
                    {
                        double pointPerCorrect = (double)question.Points / correctIds.Count;
                        int correctMatches = correctIds.Intersect(selectedIds).Count();
                        int incorrectMatches = selectedIds.Except(correctIds).Count();

                        awardedPoints = (pointPerCorrect * correctMatches) - (pointPerCorrect * incorrectMatches); // Штраф за зайві галочки
                        if (awardedPoints < 0) awardedPoints = 0;
                    }
                }
                else { answerDetails = "Відповідь не надано"; }
            }
            else if (question.Type == QuestionType.TextMatch)
            {
                int correctMatches = 0;
                var matchResults = new List<string>();

                foreach (var opt in question.AnswerOptions!)
                {
                    string submittedTarget = form[$"match_{opt.Id}"];
                    if (submittedTarget == opt.MatchTarget) correctMatches++;
                    string targetText = string.IsNullOrEmpty(submittedTarget) ? "Не обрано" : submittedTarget;
                    matchResults.Add($"{opt.Text} ➔ {targetText}");
                }

                int totalPairs = question.AnswerOptions.Count;
                isCorrect = totalPairs > 0 && correctMatches == totalPairs;
                awardedPoints = totalPairs > 0 ? ((double)question.Points / totalPairs) * correctMatches : 0;
                answerDetails = string.Join(" | ", matchResults);
            }
            else if (question.Type == QuestionType.GraphicDragAndDrop)
            {
                string rawResult = form["graphicResult"];
                isCorrect = (rawResult == "true");
                answerDetails = isCorrect ? "Всі фрагменти розставлено правильно" : "Є помилки у розстановці фрагментів на зображенні";
                awardedPoints = isCorrect ? question.Points : 0;
            }
            else if (question.Type == QuestionType.ShortAnswer)
            {
                string studentText = form["shortAnswerText"].ToString().Trim();
                var correctAnswers = question.AnswerOptions!.Where(a => a.IsCorrect).Select(a => a.Text.ToLower()).ToList();

                isCorrect = correctAnswers.Contains(studentText.ToLower());
                answerDetails = string.IsNullOrEmpty(studentText) ? "Відповідь не надано" : studentText;
                awardedPoints = isCorrect ? question.Points : 0;
            }
            else if (question.Type == QuestionType.Ordering)
            {
                var rawOrder = form["orderedIds"].ToString();
                if (!string.IsNullOrEmpty(rawOrder))
                {
                    var submittedOrder = rawOrder.Split(',').Select(int.Parse).ToList();
                    var correctOrder = question.AnswerOptions!.OrderBy(a => a.Id).Select(a => a.Id).ToList();

                    isCorrect = submittedOrder.SequenceEqual(correctOrder);
                    var submittedTexts = submittedOrder.Select(id => question.AnswerOptions!.First(o => o.Id == id).Text);
                    answerDetails = string.Join(" ➔ ", submittedTexts);
                    int correctPositions = 0;
                    for (int i = 0; i < submittedOrder.Count; i++)
                    {
                        if (i < correctOrder.Count && submittedOrder[i] == correctOrder[i]) correctPositions++;
                    }
                    awardedPoints = correctOrder.Any() ? ((double)question.Points / correctOrder.Count) * correctPositions : 0;
                }
                else
                {
                    isCorrect = false;
                    answerDetails = "Відповідь не надано";
                }
            }
            else if (question.Type == QuestionType.LongAnswer)
            {
                answerDetails = form["longAnswerText"].ToString();
                isCorrect = false;
                awardedPoints = 0;
                isManuallyGraded = false;
            }

            awardedPoints = Math.Round(awardedPoints, 2);

            var studentAnswer = new StudentAnswer
            {
                TestAttemptId = attemptId,
                QuestionId = questionId,
                IsCorrect = isCorrect,
                AnswerDetails = answerDetails,
                AwardedPoints = awardedPoints,
                IsManuallyGraded = isManuallyGraded
            };

            _context.StudentAnswers.Add(studentAnswer);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(TakeTest), new { attemptId = attemptId });
        }

        public async Task<IActionResult> FinishTest(int attemptId)
        {
            var userId = _userManager.GetUserId(User);
            var attempt = await _context.TestAttempts
            .Include(a => a.Test)
            .Include(a => a.Answers!)
                .ThenInclude(ans => ans.Question)
                    .ThenInclude(q => q.AnswerOptions)
            .Include(a => a.Answers!)
                .ThenInclude(ans => ans.Question)
                    .ThenInclude(q => q.GraphicMatchZones)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null) return NotFound();

            if (attempt.StudentId != userId)
            {
                return Forbid();
            }

            if (attempt != null && !attempt.IsCompleted)
            {
                attempt.IsCompleted = true;
                attempt.EndTime = DateTime.UtcNow;

                double totalMaxPoints = attempt.Answers!.Sum(a => a.Question!.Points);
                double totalAwarded = attempt.Answers!.Sum(a => a.AwardedPoints);

                if (attempt.Answers!.Any(a => a.Question!.Type == QuestionType.LongAnswer))
                {
                    attempt.RequiresManualGrading = true;
                }

                attempt.FinalScore = totalMaxPoints > 0 ? Math.Round((totalAwarded / totalMaxPoints) * 100, 2) : 0;

                _context.Update(attempt);
                await _context.SaveChangesAsync();
            }

            return View("TestResult", attempt);
        }

        [HttpPost]
        public async Task<IActionResult> JoinByCode(string accessCode)
        {
            if (string.IsNullOrWhiteSpace(accessCode)) return RedirectToAction(nameof(Index));

            var test = await _context.Tests.FirstOrDefaultAsync(t => t.AccessType == AccessType.ByCode && t.AccessCode == accessCode);

            if (test != null)
            {
                var user = await _userManager.GetUserAsync(User);
                var alreadyJoined = await _context.TestStudentAccesses.AnyAsync(sa => sa.TestId == test.Id && sa.StudentId == user!.Id);
                if (!alreadyJoined)
                {
                    _context.TestStudentAccesses.Add(new TestStudentAccess { TestId = test.Id, StudentId = user!.Id });
                    await _context.SaveChangesAsync();
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}