namespace InterviewPortal.Models;
public class User : IdentityUser
{
    public string FirstName { get; set; } = String.Empty;
    public string LastName { get; set; } = String.Empty;

    public ICollection<Result>? Results { get; set; }
    public ICollection<UserAnswer>? UserAnswers { get; set; }  
    public ICollection<InterviewSession>? InterviewSessions { get; set; } 