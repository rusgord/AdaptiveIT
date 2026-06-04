namespace AdaptiveIT.Models
{
    public class TestStudentAccess
    {
        public int Id { get; set; }
        public int TestId { get; set; }
        public Test? Test { get; set; }

        public string StudentId { get; set; } = string.Empty;
        public ApplicationUser? Student { get; set; }
    }
}