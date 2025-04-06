namespace InterviewPortal.Models;
public class Position
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<PositionTopic> PositionTopics { get; set; } = new List<PositionTopic>();
}