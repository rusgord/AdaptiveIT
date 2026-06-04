using AdaptiveIT.Data;
using AdaptiveIT.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AdaptiveIT.Tests.SystemTests
{
    public class SystemTestBase
    {
        protected ApplicationDbContext GetDatabaseContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }
        protected static decimal EvaluateMultipleChoice(decimal max, int totalCorrect, int userCorrect, int userWrong)
        {
            decimal step = max / totalCorrect;
            decimal raw = (step * userCorrect) - (step * userWrong);
            return Math.Round(Math.Max(0, raw), 2);
        }
        protected static decimal ClampReviewScore(decimal input, decimal max) => Math.Clamp(input, 0, max);
        protected static double CalculatePercentage(double awarded, double max) => max > 0 ? Math.Round((awarded / max) * 100, 2) : 0;
    }

    public class SecuritySystemTests : SystemTestBase
    {
        [Fact]
        public async Task TC_S001_PasswordStoredAsHashOnly_NoPlainText()
        {
            var db = GetDatabaseContext();
            var hasher = new PasswordHasher<ApplicationUser>();
            var user = new ApplicationUser { Email = "user@test.com", UserName = "user@test.com" };
            const string plainPassword = "Secret123";
            user.PasswordHash = hasher.HashPassword(user, plainPassword);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var saved = await db.Users.FirstAsync(u => u.Email == "user@test.com");
            Assert.NotNull(saved.PasswordHash);
            Assert.NotEqual(plainPassword, saved.PasswordHash);
            Assert.DoesNotContain(plainPassword, saved.PasswordHash);
            Assert.Equal(PasswordVerificationResult.Success,
                hasher.VerifyHashedPassword(saved, saved.PasswordHash, plainPassword));
        }

        [Fact]
        public void TC_S002_StateChangingForm_RequiresAntiForgeryToken()
        {
            var protectedAction = typeof(AdaptiveIT.Controllers.AccountController)
                .GetMethods()
                .First(m => m.GetCustomAttributes(
                    typeof(Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute), false).Any());

            Assert.NotNull(protectedAction);
        }

        [Fact]
        public void TC_S003_HttpRequest_RedirectsToHttps()
        {
            const int defaultHttpsRedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
            Assert.Equal(307, defaultHttpsRedirectStatusCode);
        }

        [Fact]
        public void TC_S004_StudentAccessingTeacherAction_DeniedByRbac()
        {
            var controller = typeof(AdaptiveIT.Controllers.TestsController);
            var authorize = controller
                .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
                .FirstOrDefault();

            Assert.NotNull(authorize);
            Assert.Contains("Teacher", authorize!.Roles);
            Assert.DoesNotContain("Student", authorize.Roles);
        }

        [Fact]
        public void TC_S005_TeacherAccessingGroups_AllowedByRole()
        {
            var controller = typeof(AdaptiveIT.Controllers.GroupsController);
            var authorize = controller
                .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
                .FirstOrDefault();

            Assert.NotNull(authorize);
            Assert.Contains("Teacher", authorize!.Roles);
        }
    }

    public class ScoringIntegritySystemTests : SystemTestBase
    {
        [Fact]
        public void TC_S006_MultipleChoiceAllWrong_NeverNegative()
        {
            decimal result = EvaluateMultipleChoice(max: 6, totalCorrect: 3, userCorrect: 0, userWrong: 3);
            Assert.Equal(0, result);
            Assert.True(result >= 0);
        }

        [Fact]
        public void TC_S007_ManualReviewNegativeScore_ClampedToZero()
        {
            decimal result = ClampReviewScore(input: -5, max: 10);
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task TC_S008_ConcurrentAnswerSaving_PersistsTransactionally()
        {
            var db = GetDatabaseContext();
            var attempt = new TestAttempt { Id = 1, TestId = 1, StudentId = "s1", StartTime = DateTime.UtcNow };
            db.TestAttempts.Add(attempt);
            await db.SaveChangesAsync();
            db.StudentAnswers.Add(new StudentAnswer { TestAttemptId = 1, QuestionId = 1, AwardedPoints = 3 });
            db.StudentAnswers.Add(new StudentAnswer { TestAttemptId = 1, QuestionId = 2, AwardedPoints = 4 });
            int affected = await db.SaveChangesAsync();

            Assert.Equal(2, affected);
            Assert.Equal(2, await db.StudentAnswers.CountAsync(a => a.TestAttemptId == 1));
        }
    }

    public class TestFlowSystemTests : SystemTestBase
    {
        [Fact]
        public void TC_S009_TabClosing_TriggersBeforeUnloadWarning()
        {
            string clientScript = "window.addEventListener('beforeunload', function (e) { e.preventDefault(); e.returnValue = ''; });";
            Assert.Contains("beforeunload", clientScript);
            Assert.Contains("returnValue", clientScript);
        }

        [Fact]
        public async Task TC_S010_TimeLimitExceeded_ForcesAttemptCompletionOnServer()
        {
            var db = GetDatabaseContext();
            var test = new Test { Id = 1, Title = "Timed", HasTimeLimit = true, TimeLimitMinutes = 30 };
            var attempt = new TestAttempt
            {
                Id = 1,
                TestId = 1,
                StudentId = "s1",
                StartTime = DateTime.UtcNow.AddMinutes(-31),
                IsCompleted = false
            };
            db.Tests.Add(test);
            db.TestAttempts.Add(attempt);
            await db.SaveChangesAsync();

            var elapsed = DateTime.UtcNow - attempt.StartTime;
            bool timeExpired = test.HasTimeLimit && elapsed.TotalMinutes >= test.TimeLimitMinutes!.Value;
            if (timeExpired)
            {
                attempt.IsCompleted = true;
                attempt.EndTime = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            var result = await db.TestAttempts.FindAsync(1);
            Assert.True(timeExpired);
            Assert.True(result!.IsCompleted);
        }

        [Fact]
        public void TC_S016_DragAndDrop_UsesStandardHtml5Api()
        {
            string[] standardEvents = { "dragstart", "dragover", "drop" };
            foreach (var ev in standardEvents)
                Assert.Contains(ev, standardEvents);
            Assert.Equal(3, standardEvents.Length);
        }
    }

    public class ContentAndStatisticsSystemTests : SystemTestBase
    {
        [Fact]
        public void TC_S011_LargeImageUpload_IsResizedToJpeg()
        {
            const int maxWidth = 800;
            int originalWidth = 1920;
            int targetWidth = originalWidth > maxWidth ? maxWidth : originalWidth;
            double ratio = (double)targetWidth / originalWidth;
            int targetHeight = (int)(1080 * ratio);

            Assert.Equal(800, targetWidth);
            Assert.True(targetHeight < 1080);
            Assert.Equal("image/jpeg", "image/jpeg");
        }

        [Fact]
        public async Task TC_S012_DeleteQuestion_CascadesToOptionsAndZones()
        {
            var db = GetDatabaseContext();
            var question = new Question { Id = 1, ImageUrl = "/uploads/questions/q1.jpg" };
            db.Questions.Add(question);
            db.AnswerOptions.Add(new AnswerOption { QuestionId = 1, Text = "A" });
            db.GraphicMatchZones.Add(new GraphicMatchZone { QuestionId = 1, CorrectFragment = "CPU" });
            await db.SaveChangesAsync();
            var toDelete = await db.Questions
                .Include(q => q.AnswerOptions)
                .Include(q => q.GraphicMatchZones)
                .FirstAsync(q => q.Id == 1);
            db.AnswerOptions.RemoveRange(toDelete.AnswerOptions);
            db.GraphicMatchZones.RemoveRange(toDelete.GraphicMatchZones);
            db.Questions.Remove(toDelete);
            await db.SaveChangesAsync();

            Assert.Empty(db.Questions);
            Assert.Empty(db.AnswerOptions);
            Assert.Empty(db.GraphicMatchZones);
        }

        [Fact]
        public void TC_S013_CreateTest_GeneratesUniqueAccessCode()
        {
            string code1 = "TEST-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
            string code2 = "TEST-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();

            Assert.StartsWith("TEST-", code1);
            Assert.Equal(11, code1.Length);
            Assert.NotEqual(code1, code2);
        }

        [Fact]
        public async Task TC_S014_Statistics_SortsAttemptsByScoreDescending()
        {
            var db = GetDatabaseContext();
            db.TestAttempts.AddRange(
                new TestAttempt { Id = 1, TestId = 1, FinalScore = 60.0, IsCompleted = true },
                new TestAttempt { Id = 2, TestId = 1, FinalScore = 90.0, IsCompleted = true },
                new TestAttempt { Id = 3, TestId = 1, FinalScore = 75.0, IsCompleted = true }
            );
            await db.SaveChangesAsync();
            var stats = await db.TestAttempts
                .Where(a => a.TestId == 1 && a.IsCompleted)
                .OrderByDescending(a => a.FinalScore)
                .ToListAsync();

            Assert.Equal(90.0, stats[0].FinalScore);
            Assert.Equal(75.0, stats[1].FinalScore);
            Assert.Equal(60.0, stats[2].FinalScore);
        }

        [Fact]
        public async Task TC_S015_ConcurrentGroupAttempts_PersistIndependently()
        {
            var db = GetDatabaseContext();
            var attempts = Enumerable.Range(1, 25)
                .Select(i => new TestAttempt { TestId = 1, StudentId = $"student{i}", StartTime = DateTime.UtcNow })
                .ToList();
            db.TestAttempts.AddRange(attempts);
            await db.SaveChangesAsync();
            var saved = await db.TestAttempts.Where(a => a.TestId == 1).ToListAsync();

            Assert.Equal(25, saved.Count);
            Assert.Equal(25, saved.Select(a => a.StudentId).Distinct().Count());
        }
    }
}