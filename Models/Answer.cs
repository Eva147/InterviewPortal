namespace InterviewPortal.Models;
public class Answer
{
    public int Id { get; set; }
    public string UserAnswer { get; set; } = String.Empty;
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; }
    public int UserId { get; set; }

    public User? User { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }
}