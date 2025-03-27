namespace InterviewPortal.Models;
public class Question
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = String.Empty;
    public QuestionDifficultyLevel Difficulty { get; set; }
    public int TopicId { get; set; }
    public Topic? Topic { get; set; }

    public ICollection<Answer> Answers { get; set; } = [];
    public ICollection<UserAnswer>? UserAnswers { get; set; } 
}

public enum QuestionDifficultyLevel
{
    Easy,
    Medium,
    Hard
}