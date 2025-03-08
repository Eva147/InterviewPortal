namespace InterviewPortal.Models;
public class PositionTopic
{
    public int PositionId { get; set; }
    public Position? Position { get; set; }

    public int TopicId { get; set; }
    public Topic? Topic { get; set; }
}