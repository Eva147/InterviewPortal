namespace InterviewPortal.Models;
public class Result
{
    public int Id { get; set; }
    public int FinalScore { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int PositionId { get; set; }
    public Position? Position { get; set; }
}