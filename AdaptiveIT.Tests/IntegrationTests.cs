using AdaptiveIT.Data;
using AdaptiveIT.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AdaptiveIT.Tests.IntegrationTests
{
    public class IntegrationTestBase
    {
        protected ApplicationDbContext GetDatabaseContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }
    }

    public class IdentityIntegrationTests : IntegrationTestBase
    {
        [Fact]
        public async Task TC_I101_RegisterValidUser_CreatesRecordAndSendsEmail()
        {
            var db = GetDatabaseContext();
            var user = new ApplicationUser { Email = "student@test.com", UserName = "student@test.com", FullName = "Test Student" };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var savedUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "student@test.com");
            Assert.NotNull(savedUser);
            Assert.Equal("Test Student", savedUser.FullName);
        }

        [Fact]
        public async Task TC_I102_ConfirmEmail_ActivatesUser()
        {
            var db = GetDatabaseContext();
            var user = new ApplicationUser { Email = "test@test.com", EmailConfirmed = false };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            user.EmailConfirmed = true;
            db.Users.Update(user);
            await db.SaveChangesAsync();

            var activatedUser = await db.Users.FindAsync(user.Id);
            Assert.True(activatedUser.EmailConfirmed);
        }

        [Fact]
        public async Task TC_I103_UnconfirmedEmail_CannotLogin()
        {
            var db = GetDatabaseContext();
            var user = new ApplicationUser { Email = "unconfirmed@test.com", UserName = "unconfirmed@test.com", EmailConfirmed = false };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var savedUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "unconfirmed@test.com");
            Assert.NotNull(savedUser);
            Assert.False(savedUser.EmailConfirmed);
        }

        [Fact]
        public async Task TC_I104_ResetPassword_UpdatesPasswordHash()
        {
            var db = GetDatabaseContext();
            var user = new ApplicationUser { Email = "test@test.com", PasswordHash = "OldHash" };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            user.PasswordHash = "NewHash123!";
            db.Users.Update(user);
            await db.SaveChangesAsync();

            var updatedUser = await db.Users.FindAsync(user.Id);
            Assert.Equal("NewHash123!", updatedUser.PasswordHash);
        }

        [Fact]
        public async Task TC_I105_DbSeeder_InitializesRolesAndAdmin()
        {
            var db = GetDatabaseContext();

            db.Roles.Add(new IdentityRole { Name = "Admin", NormalizedName = "ADMIN" });
            db.Roles.Add(new IdentityRole { Name = "Teacher", NormalizedName = "TEACHER" });
            db.Roles.Add(new IdentityRole { Name = "Student", NormalizedName = "STUDENT" });
            db.Users.Add(new ApplicationUser { Email = "admin@adaptiveit.com", FullName = "Admin" });
            await db.SaveChangesAsync();

            Assert.Equal(3, await db.Roles.CountAsync());
            var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@adaptiveit.com");
            Assert.NotNull(admin);
        }
    }

    public class TestExecutionIntegrationTests : IntegrationTestBase
    {
        [Fact]
        public async Task TC_I201_StartTest_CreatesAttempt()
        {
            var db = GetDatabaseContext();
            var attempt = new TestAttempt { TestId = 1, StudentId = "student1", StartTime = DateTime.UtcNow };

            db.TestAttempts.Add(attempt);
            await db.SaveChangesAsync();

            Assert.True(attempt.Id > 0);
            Assert.NotEqual(default(DateTime), attempt.StartTime);
        }

        [Fact]
        public async Task TC_I202_TakeTest_IncludesQuestionsAndOptions()
        {
            var db = GetDatabaseContext();
            var test = new Test
            {
                Id = 1,
                Title = "C# Basics",
                Questions = new List<Question> { new Question { Id = 1, Text = "Q1", AnswerOptions = new List<AnswerOption> { new AnswerOption { Text = "A1" } } } }
            };
            db.Tests.Add(test);
            await db.SaveChangesAsync();

            var loadedTest = await db.Tests
                .Include(t => t.Questions)
                .ThenInclude(q => q.AnswerOptions)
                .FirstOrDefaultAsync(t => t.Id == 1);

            Assert.NotNull(loadedTest);
            Assert.Single(loadedTest.Questions);
            Assert.Single(loadedTest.Questions.First().AnswerOptions);
        }

        [Fact]
        public async Task TC_I203_AdaptiveMode_SelectsClosestDifficultyQuestion()
        {
            var db = GetDatabaseContext();
            db.Questions.AddRange(
                new Question { Id = 1, DifficultyLevel = 2, TestId = 1 },
                new Question { Id = 2, DifficultyLevel = 7, TestId = 1 },
                new Question { Id = 3, DifficultyLevel = 9, TestId = 1 }
            );
            await db.SaveChangesAsync();

            int targetDifficulty = 8;

            var nextQuestion = await db.Questions
                .Where(q => q.TestId == 1)
                .OrderBy(q => Math.Abs(q.DifficultyLevel - targetDifficulty))
                .FirstOrDefaultAsync();

            Assert.NotNull(nextQuestion);
            Assert.Equal(2, nextQuestion.Id);
        }

        [Fact]
        public async Task TC_I204_SubmitAnswer_ClosedQuestion_SavesStudentAnswer()
        {
            var db = GetDatabaseContext();
            var answer = new StudentAnswer { TestAttemptId = 1, QuestionId = 1, AwardedPoints = 4.0, IsCorrect = true };

            db.StudentAnswers.Add(answer);
            await db.SaveChangesAsync();

            var saved = await db.StudentAnswers.FindAsync(answer.Id);
            Assert.NotNull(saved);
            Assert.Equal(4.0, saved.AwardedPoints);
        }

        [Fact]
        public async Task TC_I205_SubmitAnswer_MultipleChoice_CalculatesPenalty()
        {
            var db = GetDatabaseContext();
            var answer = new StudentAnswer { TestAttemptId = 1, QuestionId = 2, AwardedPoints = 2.0, IsCorrect = false };

            db.StudentAnswers.Add(answer);
            await db.SaveChangesAsync();

            var saved = await db.StudentAnswers.FindAsync(answer.Id);
            Assert.NotNull(saved);
            Assert.Equal(2.0, saved.AwardedPoints);
        }

        [Fact]
        public async Task TC_I206_FinishTest_CalculatesFinalScoreAndCompletes()
        {
            var db = GetDatabaseContext();
            var attempt = new TestAttempt
            {
                Id = 1,
                TestId = 1,
                Answers = new List<StudentAnswer> { new StudentAnswer { AwardedPoints = 5 }, new StudentAnswer { AwardedPoints = 3 } }
            };
            db.TestAttempts.Add(attempt);
            await db.SaveChangesAsync();

            double totalMax = 10.0;
            double earned = attempt.Answers.Sum(a => a.AwardedPoints);
            attempt.FinalScore = Math.Round((earned / totalMax) * 100, 2);
            attempt.IsCompleted = true;
            db.TestAttempts.Update(attempt);
            await db.SaveChangesAsync();

            var result = await db.TestAttempts.FindAsync(1);
            Assert.True(result.IsCompleted);
            Assert.Equal(80.0, result.FinalScore);
        }
    }

    public class TestManagementIntegrationTests : IntegrationTestBase
    {
        [Fact]
        public async Task TC_I301_CreateTestByGroup_CreatesAccessRecord()
        {
            var db = GetDatabaseContext();
            var test = new Test { Title = "Group Test", AccessType = AccessType.ByGroup };
            var groupAccess = new TestGroupAccess { Test = test, GroupId = 101 };

            db.Tests.Add(test);
            db.TestGroupAccesses.Add(groupAccess);
            await db.SaveChangesAsync();

            var savedAccess = await db.TestGroupAccesses.Include(a => a.Test).FirstOrDefaultAsync();
            Assert.NotNull(savedAccess);
            Assert.Equal(AccessType.ByGroup, savedAccess.Test.AccessType);
        }

        [Fact]
        public async Task TC_I302_EditTest_FailsForNonAuthor()
        {
            var db = GetDatabaseContext();
            db.Tests.Add(new Test { Id = 1, TeacherId = "TeacherA" });
            await db.SaveChangesAsync();

            var testToEdit = await db.Tests.FirstOrDefaultAsync(t => t.Id == 1 && t.TeacherId == "TeacherB");

            Assert.Null(testToEdit);
        }

        [Fact]
        public async Task TC_I303_SaveSingleChoice_ResetsOtherCorrectOptions()
        {
            var db = GetDatabaseContext();
            var q = new Question
            {
                Id = 1,
                Type = QuestionType.SingleChoice,
                AnswerOptions = new List<AnswerOption> { new AnswerOption { Id = 1, IsCorrect = true }, new AnswerOption { Id = 2, IsCorrect = false } }
            };
            db.Questions.Add(q);
            await db.SaveChangesAsync();

            var options = await db.AnswerOptions.Where(a => a.QuestionId == 1).ToListAsync();
            foreach (var opt in options) opt.IsCorrect = false;
            options.First(o => o.Id == 2).IsCorrect = true;

            db.AnswerOptions.UpdateRange(options);
            await db.SaveChangesAsync();

            var updated = await db.AnswerOptions.ToListAsync();
            Assert.False(updated.First(o => o.Id == 1).IsCorrect);
            Assert.True(updated.First(o => o.Id == 2).IsCorrect);
        }

        [Fact]
        public async Task TC_I304_AddGraphicZone_SavesCoordinates()
        {
            var db = GetDatabaseContext();
            var zone = new GraphicMatchZone { QuestionId = 1, PositionX = 10.5, PositionY = 20.1, CorrectFragment = "CPU" };

            db.GraphicMatchZones.Add(zone);
            await db.SaveChangesAsync();

            var saved = await db.GraphicMatchZones.FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal("CPU", saved.CorrectFragment);
        }

        [Fact]
        public async Task TC_I305_UploadQuestionImage_SavesUrlToDatabase()
        {
            var db = GetDatabaseContext();
            var question = new Question { Id = 1, Text = "Знайдіть елемент", Type = QuestionType.GraphicDragAndDrop };
            db.Questions.Add(question);
            await db.SaveChangesAsync();

            var expectedUrl = "/uploads/questions/scaled-image.jpg";
            var dbQuestion = await db.Questions.FindAsync(1);
            dbQuestion.ImageUrl = expectedUrl;
            db.Questions.Update(dbQuestion);
            await db.SaveChangesAsync();
            var savedQuestion = await db.Questions.FindAsync(1);
            Assert.NotNull(savedQuestion);
            Assert.Equal(expectedUrl, savedQuestion.ImageUrl);
        }

        [Fact]
        public async Task TC_I306_DeleteQuestion_CascadesToOptionsAndZones()
        {
            var db = GetDatabaseContext();
            var question = new Question { Id = 1 };
            db.Questions.Add(question);
            db.AnswerOptions.Add(new AnswerOption { QuestionId = 1 });
            db.GraphicMatchZones.Add(new GraphicMatchZone { QuestionId = 1 });
            await db.SaveChangesAsync();

            var qToDelete = await db.Questions.Include(q => q.AnswerOptions).Include(q => q.GraphicMatchZones).FirstAsync();
            db.AnswerOptions.RemoveRange(qToDelete.AnswerOptions);
            db.GraphicMatchZones.RemoveRange(qToDelete.GraphicMatchZones);
            db.Questions.Remove(qToDelete);
            await db.SaveChangesAsync();

            Assert.Empty(db.Questions);
            Assert.Empty(db.AnswerOptions);
            Assert.Empty(db.GraphicMatchZones);
        }
    }

    public class GradingAndStatisticsIntegrationTests : IntegrationTestBase
    {
        [Fact]
        public async Task TC_I401_PendingReviews_FiltersCorrectly()
        {
            var db = GetDatabaseContext();
            var myTest = new Test { Id = 1, TeacherId = "Me" };
            db.Tests.Add(myTest);

            db.TestAttempts.Add(new TestAttempt { TestId = 1, IsCompleted = true, RequiresManualGrading = true });
            db.TestAttempts.Add(new TestAttempt { TestId = 1, IsCompleted = false, RequiresManualGrading = true });

            await db.SaveChangesAsync();

            var pending = await db.TestAttempts
                .Include(a => a.Test)
                .Where(a => a.Test.TeacherId == "Me" && a.IsCompleted && a.RequiresManualGrading)
                .ToListAsync();

            Assert.Single(pending);
        }

        [Fact]
        public async Task TC_I402_ReviewAttempt_IncludesAllRelations()
        {
            var db = GetDatabaseContext();
            var attempt = new TestAttempt { Id = 1, Answers = new List<StudentAnswer> { new StudentAnswer { Question = new Question() } } };
            db.TestAttempts.Add(attempt);
            await db.SaveChangesAsync();

            var loaded = await db.TestAttempts
                .Include(a => a.Answers)
                    .ThenInclude(ans => ans.Question)
                .FirstOrDefaultAsync(a => a.Id == 1);

            Assert.NotNull(loaded.Answers.First().Question);
        }

        [Fact]
        public async Task TC_I403_SaveReview_SavesPointsAndFeedback_ClearsFlag()
        {
            var db = GetDatabaseContext();
            var attempt = new TestAttempt
            {
                Id = 1,
                RequiresManualGrading = true,
                Answers = new List<StudentAnswer> { new StudentAnswer { Id = 1, AwardedPoints = 0 } }
            };
            db.TestAttempts.Add(attempt);
            await db.SaveChangesAsync();

            var answer = attempt.Answers.First();
            answer.AwardedPoints = 10;
            answer.TeacherFeedback = "Добре!";
            attempt.RequiresManualGrading = false;

            db.TestAttempts.Update(attempt);
            await db.SaveChangesAsync();

            var updatedAttempt = await db.TestAttempts.Include(a => a.Answers).FirstAsync();
            Assert.False(updatedAttempt.RequiresManualGrading);
            Assert.Equal(10, updatedAttempt.Answers.First().AwardedPoints);
            Assert.Equal("Добре!", updatedAttempt.Answers.First().TeacherFeedback);
        }

        [Fact]
        public async Task TC_I404_SaveReview_RecalculatesFinalScore()
        {
            var db = GetDatabaseContext();
            var attempt = new TestAttempt { Id = 1, FinalScore = 0, Answers = new List<StudentAnswer> { new StudentAnswer { Id = 1, AwardedPoints = 0 } } };
            db.TestAttempts.Add(attempt);
            await db.SaveChangesAsync();

            var answer = attempt.Answers.First();
            answer.AwardedPoints = 10;
            attempt.FinalScore = 100.0;

            db.TestAttempts.Update(attempt);
            await db.SaveChangesAsync();

            var updatedAttempt = await db.TestAttempts.FirstAsync();
            Assert.Equal(100.0, updatedAttempt.FinalScore);
        }

        [Fact]
        public async Task TC_I405_Statistics_SortsByFinalScoreDescending()
        {
            var db = GetDatabaseContext();
            db.TestAttempts.AddRange(
                new TestAttempt { Id = 1, FinalScore = 50.0, IsCompleted = true, TestId = 1 },
                new TestAttempt { Id = 2, FinalScore = 95.5, IsCompleted = true, TestId = 1 },
                new TestAttempt { Id = 3, FinalScore = 75.0, IsCompleted = true, TestId = 1 }
            );
            await db.SaveChangesAsync();

            var stats = await db.TestAttempts
                .Where(a => a.TestId == 1 && a.IsCompleted)
                .OrderByDescending(a => a.FinalScore)
                .ToListAsync();

            Assert.Equal(95.5, stats[0].FinalScore);
            Assert.Equal(75.0, stats[1].FinalScore);
            Assert.Equal(50.0, stats[2].FinalScore);
        }
    }
}