using System;
using System.Collections.Generic;

namespace AdaptiveIT.Models
{
    public class TestAttempt
    {
        public int Id { get; set; }

        public int TestId { get; set; }
        public Test? Test { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }

        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }

        public double FinalScore { get; set; }

        public bool IsCompleted { get; set; }
        public bool RequiresManualGrading { get; set; }

        public ICollection<StudentAnswer>? Answers { get; set; }
    }
}