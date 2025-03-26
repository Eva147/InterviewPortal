namespace InterviewPortal.Models;
public class Result
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    public int InterviewSessionId { get; set; }
    public InterviewSession InterviewSession { get; set; } = null!;

    [Required]
    [Range(0, 100)] 
    public int FinalScore { get; set; }

    [StringLength(500)]
    public string? Feedback { get; set; }
}