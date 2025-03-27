namespace InterviewPortal.DbContexts;
public class InterviewPortalDbContext : IdentityDbContext<User>
{
    public InterviewPortalDbContext(DbContextOptions<InterviewPortalDbContext> options) : base(options) { }

    public DbSet<Position> Positions { get; set; }
    public DbSet<Topic> Topics { get; set; }
    public DbSet<PositionTopic> PositionTopics { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<Answer> Answers { get; set; }
    public DbSet<UserAnswer> UserAnswers { get; set; }
    public DbSet<InterviewSession> InterviewSessions { get; set; }
    public DbSet<Result> Results { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PositionTopic>()
            .HasKey(pt => new { pt.PositionId, pt.TopicId });

        modelBuilder.Entity<PositionTopic>()
            .HasOne(pt => pt.Position)
            .WithMany(p => p.PositionTopics)
            .HasForeignKey(pt => pt.PositionId);

        modelBuilder.Entity<PositionTopic>()
            .HasOne(pt => pt.Topic)
            .WithMany(t => t.PositionTopics)
            .HasForeignKey(pt => pt.TopicId);

        modelBuilder.Entity<Result>()
            .HasOne(r => r.User)
            .WithMany(u => u.Results)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict); 

        modelBuilder.Entity<UserAnswer>()
            .HasOne(ua => ua.User)
            .WithMany(u => u.UserAnswers)
            .HasForeignKey(ua => ua.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserAnswer>()
            .HasOne(ua => ua.Question)
            .WithMany(q => q.UserAnswers)
            .HasForeignKey(ua => ua.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Answer>()
            .HasOne(a => a.Question)
            .WithMany(q => q.Answers)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InterviewSession>()
            .HasOne(isession => isession.User)
            .WithMany(u => u.InterviewSessions)
            .HasForeignKey(isession => isession.UserId)
            .OnDelete(DeleteBehavior.Restrict); 
    }
}