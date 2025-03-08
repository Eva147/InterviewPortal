namespace InterviewPortal.Models;
public class User : IdentityUser
{
    public string FirstName { get; set; } = String.Empty;
    public string LastName { get; set; } = String.Empty;   

    // Navigation Properties
    public ICollection<Result>? Results { get; set; }
    public ICollection<Answer>? Answers { get; set; }
}