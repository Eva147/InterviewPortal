namespace InterviewPortal.Models;
public class UserAnswer
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    public int AnswerId { get; set; }
    public Answer Answer { get; set; } = null!;
}