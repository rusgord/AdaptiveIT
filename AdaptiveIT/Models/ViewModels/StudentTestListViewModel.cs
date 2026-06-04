namespace AdaptiveIT.Models.ViewModels
{
    public class StudentTestListViewModel
    {
        public int TestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int MaxAttempts { get; set; }
        public int AttemptsUsed { get; set; }

        public int AttemptsLeft => MaxAttempts - AttemptsUsed;

        public bool IsCompleted { get; set; }
        public double? BestScore { get; set; }

        public double? BestAwardedPoints { get; set; }
        public double? BestMaxPoints { get; set; }
        public int? BestAttemptId { get; set; }
        public bool ShowAnswersOnFinish { get; set; }
        public bool IsAvailableByDate { get; set; } = true;
        public string DateLimitMessage { get; set; } = string.Empty;
        public string TimeLimitMessage { get; set; } = string.Empty;
        public int? UnfinishedAttemptId { get; set; }
    }
}