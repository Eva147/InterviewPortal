namespace InterviewPortal.Services;

public class DatabaseInit : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public DatabaseInit(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InterviewPortalDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);

            //Users and Roles
            await SeedRolesAsync(roleManager);
            await SeedUsersAsync(userManager);
            await AssignRolesToUsersAsync(userManager);

            var positions = await SeedPositionsAsync(dbContext);
            var topics = await SeedTopicsAsync(dbContext);
            await SeedPositionTopicsAsync(dbContext, positions, topics);
            var questions = await SeedQuestionsAsync(dbContext, topics);
            await SeedAnswersAsync(dbContext, questions);
            await SeedResultsAsync(dbContext, positions);

            Console.WriteLine("Database seeded successfully!");
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
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new User
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
        await CreateUserIfNotExists("candidate@interviewportal.com", "Candidate123!", "John", "Smith");
    }

    private async Task<List<Position>> SeedPositionsAsync(InterviewPortalDbContext dbContext)
    {
        var positions = new List<Position>
        {
            new Position { Name = "Software Engineer", PassScore = 0 },
            new Position { Name = "Product Manager", PassScore = 0 }
        };

        dbContext.Positions.AddRange(positions);
        await dbContext.SaveChangesAsync();

        Console.WriteLine($"Added {positions.Count} positions");
        return positions;
    }

    private async Task<List<Topic>> SeedTopicsAsync(InterviewPortalDbContext dbContext)
    {
        var topics = new List<Topic>
        {
            new Topic { Name = "C# Fundamentals", Description = "Basic C# programming concepts and syntax" },
            new Topic { Name = "ASP.NET Core", Description = "Web development with ASP.NET Core framework" }
        };

        dbContext.Topics.AddRange(topics);
        await dbContext.SaveChangesAsync();

        Console.WriteLine($"Added {topics.Count} topics");
        return topics;
    }

    private async Task SeedPositionTopicsAsync(InterviewPortalDbContext dbContext, List<Position> positions, List<Topic> topics)
    {
        Position FindPosition(string name) => positions.First(p => p.Name == name);

        var positionTopics = new List<PositionTopic>
        {
            new PositionTopic { Position = FindPosition("Software Engineer"), TopicId = topics.First(t => t.Name == "C# Fundamentals").Id },
            new PositionTopic { Position = FindPosition("Product Manager"), TopicId = topics.First(t => t.Name == "ASP.NET Core").Id }
        };

        dbContext.PositionTopics.AddRange(positionTopics);
        await dbContext.SaveChangesAsync();

        Console.WriteLine($"Added {positionTopics.Count} position-topic relationships");
    }

    private async Task<List<Question>> SeedQuestionsAsync(InterviewPortalDbContext dbContext, List<Topic> topics)
    {
        Topic FindTopic(string name) => topics.First(t => t.Name == name);

        var questions = new List<Question>
        {
            new Question
            {
                QuestionText = "What is the difference between value types and reference types in C#?",
                CorrectAnswer = "Value types store data directly on the stack, while reference types store a reference to their data on the heap.",
                Topic = FindTopic("C# Fundamentals"),
                Score = 1,
                Difficulty = QuestionDifficultyLevel.Easy
            },
            new Question
            {
                QuestionText = "What is middleware in ASP.NET Core and how is it configured?",
                CorrectAnswer = "Middleware are components forming a pipeline to handle requests and responses. They are configured in Program.cs using Use/Run/Map methods.",
                Topic = FindTopic("ASP.NET Core"),
                Score = 2,
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
        var candidateUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == "candidate@example.com");
        if (candidateUser == null)
        {
            Console.WriteLine("Candidate user not found, skipping answer seeding.");
            return;
        }

        var question = questions.FirstOrDefault();
        if (question == null) return;

        var answer = new Answer
        {
            UserAnswer = "Value types store their data directly in memory allocated on the stack, while reference types store a reference (memory address) to the actual data which is stored on the heap.",
            IsCorrect = true,
            AnsweredAt = DateTime.Now.AddDays(-3),
            UserId = candidateUser.Id,
            QuestionId = question.Id
        };

        dbContext.Answers.Add(answer);
        await dbContext.SaveChangesAsync();

        Console.WriteLine("Added a sample answer");
    }

    private async Task SeedResultsAsync(InterviewPortalDbContext dbContext, List<Position> positions)
    {
        var candidateUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == "candidate@example.com");
        if (candidateUser == null)
        {
            Console.WriteLine("Candidate user not found, skipping result seeding.");
            return;
        }

        var position = positions.FirstOrDefault(p => p.Name == "Software Engineer");
        if (position == null) return;

        var result = new Result
        {
            FinalScore = 8,
            UserId = candidateUser.Id,
            PositionId = position.Id
        };

        dbContext.Results.Add(result);
        await dbContext.SaveChangesAsync();

        Console.WriteLine("Added a sample interview result");
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