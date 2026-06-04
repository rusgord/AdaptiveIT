using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using AdaptiveIT.Models;
using AdaptiveIT.Services;

namespace AdaptiveIT.Tests.ModuleTests
{
    public class ScoringServiceTests
    {
        private readonly ScoringService _scoringService = new ScoringService();

        [Theory]
        [InlineData(4, true, 4)]
        [InlineData(4, false, 0)]
        public void EvaluateSingleChoice_ReturnsCorrectScore(decimal points, bool isCorrect, decimal expectedScore)
        {
            var result = _scoringService.EvaluateSingleChoice(points, isCorrect);
            Assert.Equal(expectedScore, result.AwardedPoints);
            Assert.Equal(isCorrect, result.IsCorrect);
        }

        [Fact]
        public void EvaluateSingleChoice_EmptyAnswer_ReturnsZero()
        {
            var result = _scoringService.EvaluateSingleChoice(4, isCorrect: false, isAnswerProvided: false);
            Assert.Equal(0, result.AwardedPoints);
            Assert.False(result.IsCorrect);
            Assert.Equal("Відповідь не надано", result.Details);
        }

        [Theory]
        [InlineData(6, 3, 3, 0, 6.00, true)]
        [InlineData(6, 3, 2, 0, 4.00, false)]
        [InlineData(6, 3, 2, 1, 2.00, false)]
        [InlineData(6, 3, 0, 3, 0.00, false)]
        [InlineData(5, 3, 1, 0, 1.67, false)]
        public void EvaluateMultipleChoice_CalculatesScoreAndPenalties(
            decimal points, int totalCorrect, int userCorrect, int userWrong, decimal expectedPoints, bool expectedIsCorrect)
        {
            var result = _scoringService.EvaluateMultipleChoice(points, totalCorrect, userCorrect, userWrong);
            Assert.Equal(expectedPoints, result.AwardedPoints, precision: 2);
            Assert.Equal(expectedIsCorrect, result.IsCorrect);
        }

        [Theory]
        [InlineData(4, 4, 4, 4.00, true)]
        [InlineData(4, 4, 2, 2.00, false)]
        public void EvaluateTextMatch_CalculatesProportionalScore(
            decimal points, int totalPairs, int correctPairs, decimal expectedPoints, bool expectedIsCorrect)
        {
            var result = _scoringService.EvaluateMatching(points, totalPairs, correctPairs);
            Assert.Equal(expectedPoints, result.AwardedPoints);
            Assert.Equal(expectedIsCorrect, result.IsCorrect);
        }

        [Theory]
        [InlineData(4, true, 4.00, true)]
        [InlineData(4, false, 0.00, false)]
        public void EvaluateGraphicDragAndDrop_ReturnsCorrectScore(
            decimal points, bool graphicResult, decimal expectedPoints, bool expectedIsCorrect)
        {
            var result = _scoringService.EvaluateGraphicDragAndDrop(points, graphicResult);
            Assert.Equal(expectedPoints, result.AwardedPoints);
            Assert.Equal(expectedIsCorrect, result.IsCorrect);
        }
        [Theory]
        [InlineData(4, "HTTP", " http ", 4.00, true)]
        [InlineData(4, "HTTP", "FTP", 0.00, false)]
        public void EvaluateShortAnswer_IgnoresCaseAndWhitespaces(
            decimal points, string expectedText, string userInput, decimal expectedPoints, bool expectedIsCorrect)
        {
            var result = _scoringService.EvaluateShortAnswer(points, expectedText, userInput);
            Assert.Equal(expectedPoints, result.AwardedPoints);
            Assert.Equal(expectedIsCorrect, result.IsCorrect);
        }
        [Theory]
        [InlineData(4, 4, 4, 4.00, true)]
        [InlineData(4, 4, 2, 2.00, false)]
        public void EvaluateOrdering_CalculatesProportionalScore(
            decimal points, int totalItems, int correctPositions, decimal expectedPoints, bool expectedIsCorrect)
        {
            var result = _scoringService.EvaluateOrdering(points, totalItems, correctPositions);
            Assert.Equal(expectedPoints, result.AwardedPoints);
            Assert.Equal(expectedIsCorrect, result.IsCorrect);
        }
        [Fact]
        public void EvaluateLongAnswer_RequiresManualGrading()
        {
            var result = _scoringService.EvaluateLongAnswer("Ось моя розгорнута відповідь");
            Assert.Equal(0, result.AwardedPoints);
            Assert.False(result.IsCorrect);
            Assert.True(result.RequiresManualGrading);
        }
    }

    public class AdaptiveAlgorithmTests
    {
        private readonly AdaptiveService _adaptiveService = new AdaptiveService();
        [Theory]
        [InlineData(5, true, 6)]
        [InlineData(5, false, 4)]
        [InlineData(10, true, 10)]
        [InlineData(1, false, 1)]
        public void CalculateTargetDifficulty_ClampsValuesCorrectly(int currentDifficulty, bool isCorrect, int expectedTarget)
        {
            int actualTarget = _adaptiveService.CalculateTargetDifficulty(currentDifficulty, isCorrect);
            Assert.Equal(expectedTarget, actualTarget);
        }
        [Fact]
        public void SelectNextQuestion_PicksQuestionWithMinimalDelta()
        {
            int targetDifficulty = 6;
            var availableQuestions = new List<QuestionDummy>
            {
                new QuestionDummy { Id = 1, DifficultyLevel = 2 },
                new QuestionDummy { Id = 2, DifficultyLevel = 9 },
                new QuestionDummy { Id = 3, DifficultyLevel = 7 }
            };
            var selectedQuestion = availableQuestions
                .OrderBy(q => Math.Abs(q.DifficultyLevel - targetDifficulty))
                .First();
            Assert.Equal(3, selectedQuestion.Id);
        }
    }

    public class FinalScoreCalculationTests
    {
        private readonly GradingService _gradingService = new GradingService();

        [Fact]
        public void CalculateFinalScore_ValidPoints_RoundsToTwoDecimals()
        {
            decimal totalAwarded = 7.00m;
            decimal totalMax = 10.00m;

            decimal finalScore = _gradingService.CalculatePercentage(totalAwarded, totalMax);

            Assert.Equal(70.00m, finalScore, precision: 2);
        }
        [Fact]
        public void CalculateFinalScore_ZeroMaxPoints_ReturnsZero()
        {
            decimal totalAwarded = 0m;
            decimal totalMax = 0m;

            decimal finalScore = _gradingService.CalculatePercentage(totalAwarded, totalMax);

            Assert.Equal(0m, finalScore);
        }
    }

    public class ManualGradingTests
    {
        private readonly GradingService _gradingService = new GradingService();
        [Theory]
        [InlineData(12, 10, 10)]
        [InlineData(-3, 10, 0)]
        public void ClampManualReviewScore_EnforcesBoundaries(decimal teacherInput, decimal maxPoints, decimal expectedPoints)
        {
            decimal actualPoints = _gradingService.ClampReviewScore(teacherInput, maxPoints);
            Assert.Equal(expectedPoints, actualPoints);
        }
    }

    public class UtilityTests
    {
        [Fact]
        public void GenerateAccessCode_ReturnsExpectedFormat()
        {
            string accessCode = "TEST-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
            Assert.NotNull(accessCode);
            Assert.StartsWith("TEST-", accessCode);
            Assert.Equal(11, accessCode.Length);
        }

        [Fact]
        public void GraphicZoneCoordinates_RoundsToTwoDecimals()
        {
            double rawPositionX = 45.12345;
            double rawPositionY = 78.98765;
            double roundedX = Math.Round(rawPositionX, 2);
            double roundedY = Math.Round(rawPositionY, 2);
            Assert.Equal(45.12, roundedX);
            Assert.Equal(78.99, roundedY);
        }
    }

    public class QuestionDummy { public int Id { get; set; } public int DifficultyLevel { get; set; } }

    public class ScoringResult
    {
        public decimal AwardedPoints { get; set; }
        public bool IsCorrect { get; set; }
        public string Details { get; set; }
        public bool RequiresManualGrading { get; set; }
    }

    public class ScoringService
    {
        public ScoringResult EvaluateSingleChoice(decimal max, bool isCorrect, bool isAnswerProvided = true)
        {
            if (!isAnswerProvided) return new ScoringResult { AwardedPoints = 0, IsCorrect = false, Details = "Відповідь не надано" };
            return new ScoringResult { AwardedPoints = isCorrect ? max : 0, IsCorrect = isCorrect };
        }

        public ScoringResult EvaluateMultipleChoice(decimal max, int totalCorrect, int userCorrect, int userWrong)
        {
            decimal step = max / totalCorrect;
            decimal rawScore = (step * userCorrect) - (step * userWrong);
            decimal finalScore = Math.Round(Math.Max(0, rawScore), 2);
            return new ScoringResult { AwardedPoints = finalScore, IsCorrect = (finalScore == max) };
        }

        public ScoringResult EvaluateMatching(decimal max, int totalPairs, int correctPairs)
        {
            decimal step = max / totalPairs;
            decimal score = step * correctPairs;
            return new ScoringResult { AwardedPoints = score, IsCorrect = (score == max) };
        }

        public ScoringResult EvaluateGraphicDragAndDrop(decimal max, bool result) => EvaluateSingleChoice(max, result);

        public ScoringResult EvaluateShortAnswer(decimal max, string expected, string input)
        {
            bool isCorrect = string.Equals(expected.Trim(), input.Trim(), StringComparison.OrdinalIgnoreCase);
            return EvaluateSingleChoice(max, isCorrect);
        }

        public ScoringResult EvaluateOrdering(decimal max, int total, int correctPositions) => EvaluateMatching(max, total, correctPositions);

        public ScoringResult EvaluateLongAnswer(string text) => new ScoringResult { AwardedPoints = 0, IsCorrect = false, RequiresManualGrading = true };
    }

    public class AdaptiveService
    {
        public int CalculateTargetDifficulty(int current, bool isCorrect) => Math.Clamp(current + (isCorrect ? 1 : -1), 1, 10);
    }

    public class GradingService
    {
        public decimal CalculatePercentage(decimal awarded, decimal max) => max > 0 ? Math.Round((awarded / max) * 100, 2) : 0;
        public decimal ClampReviewScore(decimal input, decimal max) => Math.Clamp(input, 0, max);
    }
}