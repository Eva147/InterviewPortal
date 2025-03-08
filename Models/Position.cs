namespace InterviewPortal.Models;
public class Position
{
    public int Id { get; set; }
    public string Name { get; set; } = String.Empty;
    public int PassScore { get; set; } 

    // Navigation Properties
    public ICollection<PositionTopic>? PositionTopics { get; set; }
    public ICollection<Result>? Results { get; set; }
}