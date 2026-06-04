namespace AdaptiveIT.Models
{
    public class StudentAnswer
    {
        public int Id { get; set; }

        public int TestAttemptId { get; set; }
        public TestAttempt? TestAttempt { get; set; }

        public int QuestionId { get; set; }
        public Question? Question { get; set; }

        public bool IsCorrect { get; set; }

        public string? AnswerDetails { get; set; }
        public double AwardedPoints { get; set; }
        public string? TeacherFeedback { get; set; }
        public bool IsManuallyGraded { get; set; }
    }
}