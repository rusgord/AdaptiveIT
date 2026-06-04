namespace AdaptiveIT.Models
{
    public class GraphicMatchZone
    {
        public int Id { get; set; }
        public int QuestionId { get; set; }
        public Question? Question { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string CorrectFragment { get; set; } = string.Empty;
    }
}