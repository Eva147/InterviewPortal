using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
namespace InterviewPortal.DbContexts;
public class InterviewPortalDbContext : IdentityDbContext<User>
{
    public InterviewPortalDbContext(DbContextOptions<InterviewPortalDbContext> options) : base(options) { }

    public DbSet<Position> Positions { get; set; }
    public DbSet<Topic> Topics { get; set; }
    public DbSet<PositionTopic> PositionTopics { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<Answer> Answers { get; set; }
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
    }
}