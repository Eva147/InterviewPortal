namespace InterviewPortal.Models;
public class Answer
{
    public int Id { get; set; }
    public string UserAnswer { get; set; } = String.Empty;
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; }
    public string UserId { get; set; } = string.Empty;

    public User? User { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }
}