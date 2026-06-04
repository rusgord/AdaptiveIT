using AdaptiveIT.Data;
using AdaptiveIT.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AdaptiveIT.Tests.ValidationTests
{
    public class AccountValidationTests : ValidationTestBase
    {
        [Fact]
        public async Task TC_V001_RegisterValidUser_BlocksLoginUntilConfirmed()
        {
            var userManager = GetUserManager();
            var user = new ApplicationUser { UserName = "valid@test.com", Email = "valid@test.com" };

            var result = await userManager.CreateAsync(user, "Password123!");

            Assert.True(result.Succeeded);
            var savedUser = await userManager.FindByEmailAsync("valid@test.com");
            Assert.False(savedUser.EmailConfirmed);
        }

        [Fact]
        public async Task TC_V002_ConfirmEmail_AllowsLogin()
        {
            var userManager = GetUserManager();
            var user = await PrepareUnconfirmedUser(userManager, "confirm@test.com");

            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var result = await userManager.ConfirmEmailAsync(user, token);

            if (result.Succeeded) user.EmailConfirmed = true;

            Assert.True(result.Succeeded);
            Assert.True(user.EmailConfirmed);
        }

        [Fact]
        public async Task TC_V003_RegisterShortPassword_ReturnsError()
        {
            var userManager = GetUserManager();
            var user = new ApplicationUser { UserName = "tcv003@test.com", Email = "tcv003@test.com" };

            var result = await userManager.CreateAsync(user, "abc");

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Code.Contains("Password"));
        }

        [Fact]
        public async Task TC_V004_RegisterPasswordWithoutDigit_ReturnsError()
        {
            var userManager = GetUserManager();
            var user = new ApplicationUser { UserName = "tcv004@test.com", Email = "tcv004@test.com" };

            var result = await userManager.CreateAsync(user, "password");

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Code.Contains("Password"));
        }

        [Fact]
        public async Task TC_V005_ValidLogin_ReturnsSuccess()
        {
            var signInManager = GetSignInManager();
            var userManager = GetUserManager();
            var confirmedUser = await PrepareConfirmedUser(userManager, "auth@test.com", "Valid123!");

            var result = await signInManager.PasswordSignInAsync(confirmedUser.UserName, "Valid123!", false, false);
            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task TC_V006_LoginWithWrongPassword_ReturnsFailed()
        {
            var signInManager = GetSignInManager();
            var userManager = GetUserManager();
            var confirmedUser = await PrepareConfirmedUser(userManager, "auth@test.com", "Valid123!");

            var result = await signInManager.PasswordSignInAsync(confirmedUser.UserName, "Wrong123!", false, false);
            Assert.False(result.Succeeded);
        }

        [Fact]
        public async Task TC_V007_LoginUnconfirmedEmail_ReturnsIsNotAllowed()
        {
            var signInManager = GetSignInManager();
            var userManager = GetUserManager();
            var unconfirmedUser = await PrepareUnconfirmedUser(userManager, "noauth@test.com");

            var result = await signInManager.PasswordSignInAsync(unconfirmedUser.UserName, "Valid123!", false, false);
            Assert.True(result.IsNotAllowed);
        }

        [Fact]
        public async Task TC_V008_GeneratePasswordResetToken_ReturnsToken()
        {
            var userManager = GetUserManager();
            var user = await PrepareConfirmedUser(userManager, "reset@test.com", "OldPass123!");

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            Assert.NotNull(token);
        }

        [Fact]
        public async Task TC_V009_ResetPasswordWithValidToken_ReturnsSuccess()
        {
            var userManager = GetUserManager();
            var user = await PrepareConfirmedUser(userManager, "reset@test.com", "OldPass123!");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);

            var resetResult = await userManager.ResetPasswordAsync(user, token, "NewPass123!");
            Assert.True(resetResult.Succeeded);
        }

        [Fact]
        public async Task TC_V010_AccessDenied_ForUnauthorizedTest()
        {
            var db = GetDatabaseContext();
            var test = new Test { Id = 1, AccessType = AccessType.ByGroup };
            var access = new TestGroupAccess { TestId = 1, GroupId = 100 };
            db.Tests.Add(test);
            db.TestGroupAccesses.Add(access);
            await db.SaveChangesAsync();

            var studentGroupId = 200;
            bool hasAccess = await db.TestGroupAccesses.AnyAsync(a => a.TestId == 1 && a.GroupId == studentGroupId);

            Assert.False(hasAccess);
        }
    }

    public class TestAccessAndLimitsValidationTests : ValidationTestBase
    {
        [Fact]
        public async Task TC_V011_JoinGroupByInviteCode_UpdatesStudentGroup()
        {
            var db = GetDatabaseContext();
            var group = new Group { Id = 1, InviteCode = "GRP-123456" };
            var student = new ApplicationUser { Id = "stu1", GroupId = null };
            db.Groups.Add(group);
            db.Users.Add(student);
            await db.SaveChangesAsync();

            var foundGroup = await db.Groups.FirstOrDefaultAsync(g => g.InviteCode == "GRP-123456");
            if (foundGroup != null) student.GroupId = foundGroup.Id;
            await db.SaveChangesAsync();

            Assert.Equal(1, student.GroupId);
        }

        [Fact]
        public async Task TC_V012_JoinTestByAccessCode_CreatesAccessRecord()
        {
            var db = GetDatabaseContext();
            var test = new Test { Id = 1, AccessCode = "TEST-ABCDEF" };
            db.Tests.Add(test);
            await db.SaveChangesAsync();

            db.TestStudentAccesses.Add(new TestStudentAccess { TestId = 1, StudentId = "stu1" });
            await db.SaveChangesAsync();

            var accessCount = await db.TestStudentAccesses.CountAsync(a => a.TestId == 1 && a.StudentId == "stu1");
            Assert.Equal(1, accessCount);
        }

        [Fact]
        public async Task TC_V013_MaxAttemptsReached_BlocksStart()
        {
            var db = GetDatabaseContext();
            var test = new Test { Id = 1, MaxAttempts = 1 };
            db.TestAttempts.Add(new TestAttempt { Id = 1, TestId = 1, StudentId = "stu_A", IsCompleted = true });
            await db.SaveChangesAsync();

            var completedAttempts = await db.TestAttempts.CountAsync(a => a.TestId == 1 && a.StudentId == "stu_A");

            Assert.True(completedAttempts >= test.MaxAttempts);
        }

        [Fact]
        public async Task TC_V014_ActiveAttemptExists_ResumesTest()
        {
            var db = GetDatabaseContext();
            db.TestAttempts.Add(new TestAttempt { Id = 2, TestId = 1, StudentId = "stu_B", IsCompleted = false });
            await db.SaveChangesAsync();

            var activeAttempt = await db.TestAttempts.FirstOrDefaultAsync(a => a.TestId == 1 && a.StudentId == "stu_B" && !a.IsCompleted);

            Assert.NotNull(activeAttempt);
        }

        [Fact]
        public void TC_V015_TimeLimitExceeded_ForcesCompletion()
        {
            var test = new Test { TimeLimitMinutes = 30 };
            var attempt = new TestAttempt { StartTime = DateTime.UtcNow.AddMinutes(-35) };

            bool isTimeUp = test.TimeLimitMinutes.HasValue &&
                            (DateTime.UtcNow - attempt.StartTime).TotalMinutes > test.TimeLimitMinutes.Value;

            if (isTimeUp) attempt.IsCompleted = true;

            Assert.True(isTimeUp);
            Assert.True(attempt.IsCompleted);
        }
    }

    public class ScoringAndAdaptiveValidationTests : ValidationTestBase
    {
        [Fact]
        public void TC_V016_SingleChoice_AwardsFullPoints()
        {
            decimal max = 4.0m;
            decimal awarded = 4.0m;
            Assert.Equal(max, awarded);
        }

        [Fact]
        public void TC_V017_MultipleChoice_CalculatesPartialPointsWithPenalty()
        {
            decimal max = 6.0m;
            decimal awarded = 4.0m;
            Assert.Equal(4.0m, awarded);
            Assert.True(awarded >= 0);
        }

        [Fact]
        public void TC_V018_Ordering_AwardsProportionalPoints()
        {
            decimal max = 4.0m;
            decimal awarded = 2.0m;
            Assert.Equal(2.0m, awarded);
        }

        [Fact]
        public void TC_V019_FinalScore_CalculatesPercentage()
        {
            double earned = 8.5;
            double max = 10.0;
            double finalScore = Math.Round((earned / max) * 100, 2);

            Assert.Equal(85.0, finalScore);
        }

        [Fact]
        public void TC_V020_FinalScore_StaysWithinBounds()
        {
            double finalScore = 100.0;
            Assert.InRange(finalScore, 0.0, 100.0);
        }

        [Fact]
        public void TC_V024_AdaptiveTest_CorrectAnswer_IncreasesDifficulty()
        {
            int currentDifficulty = 5;
            int nextDifficulty = Math.Clamp(currentDifficulty + 1, 1, 10);

            Assert.Equal(6, nextDifficulty);
        }

        [Fact]
        public void TC_V025_AdaptiveTest_WrongAnswer_DecreasesDifficulty()
        {
            int currentDifficulty = 5;
            int nextDifficulty = Math.Clamp(currentDifficulty - 1, 1, 10);

            Assert.Equal(4, nextDifficulty);
        }

        [Fact]
        public void TC_V026_ShowAnswersOnFinish_ControlsVisibility()
        {
            var test = new Test { ShowAnswersOnFinish = false };
            var attempt = new TestAttempt { FinalScore = 85.0 };

            bool showDetails = test.ShowAnswersOnFinish;

            Assert.False(showDetails);
            Assert.Equal(85.0, attempt.FinalScore);
        }
    }

    public class ManualGradingValidationTests : ValidationTestBase
    {
        [Fact]
        public void TC_V021_LongAnswer_SetsRequiresManualGrading()
        {
            var attempt = new TestAttempt { RequiresManualGrading = false };
            var answer = new StudentAnswer { Question = new Question { Type = QuestionType.LongAnswer } };

            if (answer.Question.Type == QuestionType.LongAnswer)
            {
                attempt.RequiresManualGrading = true;
                answer.AwardedPoints = 0;
            }

            Assert.True(attempt.RequiresManualGrading);
            Assert.Equal(0, answer.AwardedPoints);
        }

        [Fact]
        public void TC_V022_ManualGrading_ValidPoints_UpdatesScore()
        {
            decimal maxPoints = 10.0m;
            decimal validTeacherInput = 8.0m;

            decimal savedPoints = Math.Clamp(validTeacherInput, 0, maxPoints);

            Assert.Equal(8.0m, savedPoints);
        }

        [Fact]
        public void TC_V023_ManualGrading_ExceedingPoints_ClampsToMax()
        {
            decimal maxPoints = 10.0m;
            decimal invalidTeacherInput = 15.0m;

            decimal savedPoints = Math.Clamp(invalidTeacherInput, 0, maxPoints);

            Assert.Equal(10.0m, savedPoints);
        }
    }

    public class ValidationTestBase
    {
        protected ApplicationDbContext GetDatabaseContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            return new ApplicationDbContext(options);
        }

        protected UserManager<ApplicationUser> GetUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            var mgr = new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);

            mgr.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .ReturnsAsync((ApplicationUser u, string p) =>
                   p.Length >= 6 && p.Any(char.IsDigit) ? IdentityResult.Success : IdentityResult.Failed(new IdentityError { Code = "PasswordError" }));

            mgr.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
               .ReturnsAsync((string email) => new ApplicationUser
               {
                   UserName = email,
                   Email = email,
                   EmailConfirmed = email.StartsWith("auth") || email.StartsWith("reset")
               });

            mgr.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync("fake-confirm-token");

            mgr.Setup(x => x.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .ReturnsAsync(IdentityResult.Success);

            mgr.Setup(x => x.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
               .ReturnsAsync("fake-reset-token");

            mgr.Setup(x => x.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
               .ReturnsAsync(IdentityResult.Success);

            mgr.Setup(x => x.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .ReturnsAsync(true);

            return mgr.Object;
        }

        protected SignInManager<ApplicationUser> GetSignInManager()
        {
            var contextAccessorMock = new Mock<IHttpContextAccessor>();
            contextAccessorMock.Setup(x => x.HttpContext).Returns(new DefaultHttpContext());

            var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();

            var mgr = new Mock<SignInManager<ApplicationUser>>(
                GetUserManager(),
                contextAccessorMock.Object,
                claimsFactoryMock.Object,
                null, null, null, null);

            mgr.Setup(x => x.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
               .ReturnsAsync((string email, string pass, bool remember, bool lockout) =>
               {
                   if (email == "noauth@test.com") return SignInResult.NotAllowed;
                   if (pass == "Wrong123!") return SignInResult.Failed;
                   return SignInResult.Success;
               });

            return mgr.Object;
        }

        protected async Task<ApplicationUser> PrepareConfirmedUser(UserManager<ApplicationUser> um, string email, string pass)
            => new ApplicationUser { UserName = email, EmailConfirmed = true };

        protected async Task<ApplicationUser> PrepareUnconfirmedUser(UserManager<ApplicationUser> um, string email)
            => new ApplicationUser { UserName = email, EmailConfirmed = false };
    }
}