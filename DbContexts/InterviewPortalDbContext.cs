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

        modelBuilder.Entity<Answer>()
            .HasOne(a => a.User)
            .WithMany(u => u.Answers)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Result>()
            .HasOne(r => r.User)
            .WithMany(u => u.Results)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}