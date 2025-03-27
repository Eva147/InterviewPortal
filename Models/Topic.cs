namespace InterviewPortal.Models;
public class Topic
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    // do we need it here?
    public ICollection<PositionTopic> PositionTopics { get; set; } = new List<PositionTopic>();
    // do we need it here?
    public ICollection<Question> Questions { get; set; } = new List<Question>();
}