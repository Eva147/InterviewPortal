namespace InterviewPortal.Models;
public class Topic
{
    public int Id { get; set; }
    public string Name { get; set; } = String.Empty;
    public string Description { get; set; } = String.Empty;

    // Navigation Properties
    public ICollection<PositionTopic>? PositionTopics { get; set; }
    public ICollection<Question>? Questions { get; set; }
}