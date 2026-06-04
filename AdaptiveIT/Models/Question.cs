using System.Collections.Generic;

namespace AdaptiveIT.Models
{
    public class Question
    {
        public int Id { get; set; }
        public int TestId { get; set; }
        public Test? Test { get; set; }

        public string Text { get; set; } = string.Empty;
        public QuestionType Type { get; set; }
        public int Points { get; set; } = 1;

        public int DifficultyLevel { get; set; }

        public string? ImageUrl { get; set; }

        public ICollection<AnswerOption>? AnswerOptions { get; set; }

        public ICollection<GraphicMatchZone>? GraphicMatchZones { get; set; }
    }
}