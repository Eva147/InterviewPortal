namespace InterviewPortal.Models;
public class Answer
{
    public int Id { get; set; }

    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    [Required]
    [StringLength(250)]
    public string AnswerText { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }
}