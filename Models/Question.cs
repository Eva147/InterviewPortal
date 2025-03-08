namespace InterviewPortal.Models;
public class Question
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = String.Empty;
    public string CorrectAnswer { get; set; } = String.Empty;
    public int Score { get; set; }
    public QuestionDifficultyLevel Difficulty { get; set; }
    public ICollection<Answer> Answers { get; set; } = [];

    public int TopicId { get; set; }
    public Topic? Topic { get; set; }
}

public enum QuestionDifficultyLevel
{
    Easy,
    Medium,
    Hard
}