using System.Collections.Generic;

namespace AdaptiveIT.Models
{
    public class Test
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int MaxAttempts { get; set; } = 1;
        public AccessType AccessType { get; set; } = AccessType.All;

        public string TeacherId { get; set; } = string.Empty;
        public ApplicationUser? Teacher { get; set; }

        public bool HasDateLimit { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public bool HasTimeLimit { get; set; }
        public int? TimeLimitMinutes { get; set; }

        public bool ShowAnswersOnFinish { get; set; } = true;
        public string? AccessCode { get; set; } = "TEST-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();

        public int? QuestionsPerAttempt { get; set; }

        public bool IsAdaptive { get; set; } = true;

        public bool ShuffleQuestions { get; set; } = true;

        public bool ShuffleAnswers { get; set; } = true;

        public ICollection<Question>? Questions { get; set; }
        public ICollection<TestGroupAccess>? AllowedGroups { get; set; }
        public ICollection<TestStudentAccess>? AllowedStudents { get; set; }
    }
}