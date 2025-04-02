using InterviewPortal.Models;

namespace InterviewPortal.Services;
public class DatabaseInit : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public DatabaseInit(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InterviewPortalDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        try
        {

            bool resetDatabase = _configuration.GetValue<bool>("DatabaseSettings:ResetOnStartup", false);

            if (resetDatabase)
            {
                // First check and delete any sessions with invalid user references
                if (dbContext.InterviewSessions != null)
                {
                    // Find all user IDs referenced in sessions
                    var sessionUserIds = await dbContext.InterviewSessions
                        .Select(s => s.UserId)
                        .Distinct()
                        .ToListAsync(cancellationToken);

                    // Find user IDs that don't exist in AspNetUsers
                    var invalidUserIds = new List<string>();
                    foreach (var userId in sessionUserIds)
                    {
                        var userExists = await dbContext.Users.AnyAsync(u => u.Id == userId, cancellationToken);
                        if (!userExists)
                        {
                            invalidUserIds.Add(userId);
                        }
                    }

                    // Delete sessions with invalid user IDs
                    if (invalidUserIds.Any())
                    {
                        var sessionsToDelete = await dbContext.InterviewSessions
                            .Where(s => invalidUserIds.Contains(s.UserId))
                            .ToListAsync(cancellationToken);

                        dbContext.InterviewSessions.RemoveRange(sessionsToDelete);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        Console.WriteLine($"Deleted {sessionsToDelete.Count} interview sessions with invalid user references.");
                    }
                }

                // Delete and recreate the database if reset is enabled
                await dbContext.Database.EnsureDeletedAsync(cancellationToken);
                Console.WriteLine("Database deleted for reset.");

                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                Console.WriteLine("Database recreated.");

                // Seed everything fresh
                await SeedRolesAsync(roleManager);
                await SeedUsersAsync(userManager);
                await AssignRolesToUsersAsync(userManager);

                await dbContext.SaveChangesAsync(cancellationToken);

                // Seed Positions, Topics, PositionTopics, Questions, and Answers
                var positions = await SeedPositionsAsync(dbContext);
                var topics = await SeedTopicsAsync(dbContext);
                await SeedPositionTopicsAsync(dbContext, positions, topics);
                var questions = await SeedQuestionsAsync(dbContext, topics);
                await SeedAnswersAsync(dbContext, questions);

                Console.WriteLine("Database seeded successfully!");
            }
            else
            {
                //await dbContext.Database.EnsureDeletedAsync(cancellationToken);
                bool dbCreated = await dbContext.Database.EnsureCreatedAsync(cancellationToken);

                if (dbCreated)
                {
                    // Users and Roles
                    await SeedRolesAsync(roleManager);
                    await SeedUsersAsync(userManager);
                    await AssignRolesToUsersAsync(userManager);

                    await dbContext.SaveChangesAsync(cancellationToken);

                    // Seed Positions, Topics, PositionTopics, Questions, and Answers
                    var positions = await SeedPositionsAsync(dbContext);
                    var topics = await SeedTopicsAsync(dbContext);
                    await SeedPositionTopicsAsync(dbContext, positions, topics);
                    var questions = await SeedQuestionsAsync(dbContext, topics);
                    await SeedAnswersAsync(dbContext, questions);

                    Console.WriteLine("Database seeded successfully!");
                }
                else
                {
                    Console.WriteLine("Database already exists.");
                }
            }        
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during DB initialization: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine("DatabaseInitializer - Initialization process completed");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedUsersAsync(UserManager<User> userManager)
    {
        async Task CreateUserIfNotExists(string email, string password, string firstName, string lastName)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new User
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    Console.WriteLine($"Created user: {email}");
                }
                else
                {
                    Console.WriteLine($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine($"User already exists: {email}");
            }
        }

        await CreateUserIfNotExists("admin@interviewportal.com", "Admin123!", "Admin", "Admin");
        await CreateUserIfNotExists("hr@interviewportal.com", "Hr123!", "Bob", "Perkins");
        await CreateUserIfNotExists("candidate@interviewportal.com", "Candidate123!", "John", "Smith");
    }

    // Seed Positions
    private async Task<List<Position>> SeedPositionsAsync(InterviewPortalDbContext dbContext)
    {
        var positions = new List<Position>
        {
            new Position { Name = "Software Engineer" },
            new Position { Name = "Product Manager" },
            new Position { Name = "UX Designer" }
        };

        foreach (var position in positions)
        {
            if (!await dbContext.Positions.AnyAsync(p => p.Name == position.Name))
            {
                dbContext.Positions.Add(position);
                Console.WriteLine($"Added position: {position.Name}");
            }
            else
            {
                Console.WriteLine($"Position already exists: {position.Name}");
            }
        }

        await dbContext.SaveChangesAsync();
        return positions;
    }

    private async Task<List<Topic>> SeedTopicsAsync(InterviewPortalDbContext dbContext)
    {
        var topics = new List<Topic>
        {
            new Topic { Name = "C# Fundamentals", Description = "Basic C# programming concepts and syntax" },
            new Topic { Name = "ASP.NET Core", Description = "Web development with ASP.NET Core framework" },
            new Topic { Name = "Algorithm Design", Description = "Understanding and designing algorithms" },
            new Topic { Name = "Product Development", Description = "From idea to product launch" },
            new Topic { Name = "Market Research", Description = "Understanding market dynamics and customer needs" },
            new Topic { Name = "UX Design Principles", Description = "User experience design best practices" },
            new Topic { Name = "Wireframing", Description = "Creating wireframes for websites and apps" },
            new Topic { Name = "Prototyping", Description = "Building prototypes for user feedback" }
        };

        foreach (var topic in topics)
        {
            if (!await dbContext.Topics.AnyAsync(t => t.Name == topic.Name))
            {
                dbContext.Topics.Add(topic);
                Console.WriteLine($"Added topic: {topic.Name}");
            }
            else
            {
                Console.WriteLine($"Topic already exists: {topic.Name}");
            }
        }

        await dbContext.SaveChangesAsync();
        return topics;
    }

    private async Task SeedPositionTopicsAsync(InterviewPortalDbContext dbContext, List<Position> positions, List<Topic> topics)
    {
        var positionTopics = new Dictionary<string, List<string>>
        {
            { "Software Engineer", new List<string> { "C# Fundamentals", "ASP.NET Core", "Algorithm Design" } },
            { "Product Manager", new List<string> { "Product Development", "Market Research", "ASP.NET Core" } },
            { "UX Designer", new List<string> { "UX Design Principles", "Wireframing", "Prototyping" } }
        };

        foreach (var position in positions)
        {
            if (positionTopics.ContainsKey(position.Name))
            {
                var assignedTopics = positionTopics[position.Name];

                foreach (var topicName in assignedTopics)
                {
                    var topic = await dbContext.Topics.FirstOrDefaultAsync(t => t.Name == topicName);
                    if (topic != null)
                    {
                        var existingEntry = await dbContext.PositionTopics
                            .FirstOrDefaultAsync(pt => pt.PositionId == position.Id && pt.TopicId == topic.Id);

                        if (existingEntry == null)
                        {
                            dbContext.PositionTopics.Add(new PositionTopic
                            {
                                PositionId = position.Id,
                                TopicId = topic.Id
                            });
                            Console.WriteLine($"Added Position-Topic relationship: {position.Name} - {topic.Name}");
                        }
                        else
                        {
                            Console.WriteLine($"Position-Topic relationship already exists: {position.Name} - {topic.Name}");
                        }
                    }
                }
            }
        }
        await dbContext.SaveChangesAsync();
    }

    private async Task<List<Question>> SeedQuestionsAsync(InterviewPortalDbContext dbContext, List<Topic> topics)
    {
        Topic FindTopic(string name) => topics.First(t => t.Name == name);

        var questions = new List<Question>
            {
                new Question
                {
                    QuestionText = "What is the difference between value types and reference types in C#?",
                    TopicId = FindTopic("C# Fundamentals").Id,
                    Difficulty = QuestionDifficultyLevel.Easy
                },
                new Question
                {
                    QuestionText = "What is middleware in ASP.NET Core and how is it configured?",
                    TopicId = FindTopic("ASP.NET Core").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                },
                new Question
                {
                    QuestionText = "What is the purpose of the \"using\" statement in C#?",
                    TopicId = FindTopic("C# Fundamentals").Id,
                    Difficulty = QuestionDifficultyLevel.Easy
                },
                new Question
                {
                    QuestionText = "What is the difference between a product manager and a project manager?",
                    TopicId = FindTopic("Product Development").Id,
                    Difficulty = QuestionDifficultyLevel.Easy
                },
                new Question
                {
                    QuestionText = "Explain the difference between abstract classes and interfaces in C#.",
                    TopicId = FindTopic("C# Fundamentals").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                }
            };

        dbContext.Questions.AddRange(questions);
        await dbContext.SaveChangesAsync();

        Console.WriteLine($"Added {questions.Count} questions");
        return questions;
    }

    private async Task SeedAnswersAsync(InterviewPortalDbContext dbContext, List<Question> questions)
    {
        var question1 = questions.FirstOrDefault(q => q.QuestionText.Contains("value types"));
        var question2 = questions.FirstOrDefault(q => q.QuestionText.Contains("middleware"));
        var question3 = questions.FirstOrDefault(q => q.QuestionText.Contains("using"));
        var question4 = questions.FirstOrDefault(q => q.QuestionText.Contains("interfaces"));

        if (question1 != null)
        {
            var answersForQ1 = new List<Answer>
                {
                    new Answer
                    {
                        QuestionId = question1.Id,
                        AnswerText = "Value types store data directly on the stack, while reference types store a reference on the heap.",
                        IsCorrect = true
                    },
                    new Answer
                    {
                        QuestionId = question1.Id,
                        AnswerText = "Value types store a reference, while reference types store data directly.",
                        IsCorrect = false
                    },
                    new Answer
                    {
                        QuestionId = question3.Id,
                        AnswerText = "The \"using\" statement ensures that IDisposable objects are properly disposed of when they go out of scope, even if exceptions occur.",
                        IsCorrect = true,
                    },
                    new Answer
                    {
                        QuestionId = question3.Id,
                        AnswerText = "The \"using\" statement is only for importing namespaces and has no effect on resource management.",
                        IsCorrect = false,
                    },
                    new Answer
                    {
                        QuestionId = question4.Id,
                        AnswerText = "Abstract classes can contain implementation details, constructors, and fields, while interfaces primarily define contracts. A class can inherit from only one abstract class but can implement multiple interfaces.",
                        IsCorrect = true,
                    },
                    new Answer
                    {
                        QuestionId = question4.Id,
                        AnswerText = "Abstract classes and interfaces are interchangeable in C#, with interfaces supporting default implementations in all versions and providing better performance than inheritance.",
                        IsCorrect = false,
                    }
                };
            dbContext.Answers.AddRange(answersForQ1);
        }

        if (question2 != null)
        {
            var answersForQ2 = new List<Answer>
                {
                    new Answer
                    {
                        QuestionId = question2.Id,
                        AnswerText = "Middleware are components that handle requests and responses; they are configured in Program.cs.",
                        IsCorrect = true
                    },
                    new Answer
                    {
                        QuestionId = question2.Id,
                        AnswerText = "Middleware is used solely for logging and cannot handle responses.",
                        IsCorrect = false
                    }
                };
            dbContext.Answers.AddRange(answersForQ2);
        }

        await dbContext.SaveChangesAsync();
        Console.WriteLine("Added sample answers for questions");
    }

    private async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        async Task CreateRoleIfNotExists(string roleName)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                role = new IdentityRole(roleName);
                var result = await roleManager.CreateAsync(role);
                if (result.Succeeded)
                {
                    Console.WriteLine($"Created role: {roleName}");
                }
                else
                {
                    Console.WriteLine($"Failed to create role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine($"Role already exists: {roleName}");
            }
        }

        await CreateRoleIfNotExists("Candidate");
        await CreateRoleIfNotExists("Admin");
        await CreateRoleIfNotExists("HR");
        await CreateRoleIfNotExists("Owner");
    }

    private async Task AssignRolesToUsersAsync(UserManager<User> userManager)
    {
        var adminUser = await userManager.FindByEmailAsync("admin@interviewportal.com");
        if (adminUser != null)
        {
            var isAdmin = await userManager.IsInRoleAsync(adminUser, "Admin");
            if (!isAdmin)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Console.WriteLine("Assigned Admin role to admin user.");
            }
        }

        var candidateUser = await userManager.FindByEmailAsync("candidate@interviewportal.com");
        if (candidateUser != null)
        {
            var isCandidate = await userManager.IsInRoleAsync(candidateUser, "Candidate");
            if (!isCandidate)
            {
                await userManager.AddToRoleAsync(candidateUser, "Candidate");
                Console.WriteLine("Assigned Candidate role to candidate user.");
            }
        }
    }
}