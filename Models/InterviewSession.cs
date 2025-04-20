namespace InterviewPortal.Models;
public class InterviewSession
{
    public int Id { get; set; }
    
    [Required]
    public required string UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
    
    [Required]
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
    
    [Required]
    public bool IsMock { get; set; }
    
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? DurationInSeconds { get; set; }

    public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
}