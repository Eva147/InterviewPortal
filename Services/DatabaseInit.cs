using InterviewPortal.Models;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;

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
                await SeedInterviewResultsAsync(dbContext, userManager);

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
        await CreateUserIfNotExists("candidate1@interviewportal.com", "Candidate123!", "Jane", "Doe");
        await CreateUserIfNotExists("candidate2@interviewportal.com", "Candidate123!", "Alice", "Johnson");
        await CreateUserIfNotExists("candidate3@interviewportal.com", "Candidate123!", "Bob", "Brown");
        await CreateUserIfNotExists("candidate4@interviewportal.com", "Candidate123!", "Charlie", "Davis");
        await CreateUserIfNotExists("candidate5@interviewportal.com", "Candidate123!", "David", "Wilson");
        await CreateUserIfNotExists("candidate6@interviewportal.com", "Candidate123!", "Eve", "Garcia");
        await CreateUserIfNotExists("candidate7@interviewportal.com", "Candidate123!", "Frank", "Martinez");
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
            new Topic { Name = "Database Design", Description = "Principles of database design and normalisation"}
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
            { "Product Manager", new List<string> { "Product Development", "Market Research", "Database design" } },
            { "UX Designer", new List<string> { "UX Design Principles", "Market Research", "Product Development" } }
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
                // C# Fundamentals
                new Question
                {
                    QuestionText = "What is the difference between value types and reference types in C#?",
                    TopicId = FindTopic("C# Fundamentals").Id,
                    Difficulty = QuestionDifficultyLevel.Easy
                },
                new Question
                {
                    QuestionText = "What is the purpose of the \"using\" statement in C#?",
                    TopicId = FindTopic("C# Fundamentals").Id,
                    Difficulty = QuestionDifficultyLevel.Easy
                },
                new Question
                {
                    QuestionText = "Explain the difference between abstract classes and interfaces in C#.",
                    TopicId = FindTopic("C# Fundamentals").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                },
                // ASP.NET
                new Question
                {
                    QuestionText = "Explain dependency injection in ASP.NET Core and how services are registered in the service container.",
                    TopicId = FindTopic("ASP.NET Core").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                },

                new Question
                {
                    QuestionText = "What is the difference between IActionResult and ActionResult in ASP.NET Core MVC controllers?",
                    TopicId = FindTopic("ASP.NET Core").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                },
                new Question
                {
                    QuestionText = "What is middleware in ASP.NET Core and how is it configured?",
                    TopicId = FindTopic("ASP.NET Core").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                },
                //Algorithm Design
                new Question
                {
                    QuestionText = "What is the time complexity of a basic binary search algorithm?",
                    TopicId = FindTopic("Algorithm Design").Id,
                    Difficulty = QuestionDifficultyLevel.Easy
                },
                new Question
                {
                    QuestionText = "What is the main difference between greedy algorithms and dynamic programming?",
                    TopicId = FindTopic("Algorithm Design").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                },
                new Question
                {
                    QuestionText = "When implementing a solution to the traveling salesman problem, which approach would be most efficient for exactly solving the problem with 30 cities?",
                    TopicId = FindTopic("Algorithm Design").Id,
                    Difficulty = QuestionDifficultyLevel.Hard
                },
                // Product Development
                new Question
                {
                    QuestionText = "What is the difference between a product manager and a project manager?",
                    TopicId = FindTopic("Product Development").Id,
                    Difficulty = QuestionDifficultyLevel.Easy
                },
                 new Question
                {
                    QuestionText = "In agile product development, what is the purpose of a sprint retrospective?",
                    TopicId = FindTopic("Product Development").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                },
                  new Question
                {
                    QuestionText = "How should product managers prioritize features in a situation where stakeholders have conflicting requests and development resources are limited?",
                    TopicId = FindTopic("Product Development").Id,
                    Difficulty = QuestionDifficultyLevel.Hard
                },
                //Market Research
                 new Question
                {
                    QuestionText = "What is the primary purpose of market research?",
                    TopicId = FindTopic("Market Research").Id,
                    Difficulty = QuestionDifficultyLevel.Easy
                },
                  new Question
                {
                    QuestionText = "What is the difference between qualitative and quantitative market research?",
                    TopicId = FindTopic("Market Research").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                },
                   new Question
                {
                    QuestionText = "How can a company effectively use conjoint analysis in their market research strategy?",
                    TopicId = FindTopic("Market Research").Id,
                    Difficulty = QuestionDifficultyLevel.Hard
                },
                //UX Design Principles
                 new Question
                {
                    QuestionText = "What does the UX design principle of 'visibility' mean?",
                    TopicId = FindTopic("UX Design Principles").Id,
                    Difficulty = QuestionDifficultyLevel.Easy
                },
                  new Question
                {
                    QuestionText = "How does the concept of cognitive load apply to UX design?",
                    TopicId = FindTopic("UX Design Principles").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                },
                   new Question
                {
                    QuestionText = "In the context of inclusive UX design, how would you approach designing a system that balances accessibility for users with visual impairments while maintaining an engaging visual experience for sighted users?",
                    TopicId = FindTopic("UX Design Principles").Id,
                    Difficulty = QuestionDifficultyLevel.Hard
                },
                //Database Design
                 new Question
                {
                    QuestionText = "What is the purpose of normalization in database design?",
                    TopicId = FindTopic("Database Design").Id,
                    Difficulty = QuestionDifficultyLevel.Easy
                },
                  new Question
                {
                    QuestionText = "What is the difference between a clustered and non-clustered index in a relational database?",
                    TopicId = FindTopic("Database Design").Id,
                    Difficulty = QuestionDifficultyLevel.Medium
                },
                   new Question
                {
                    QuestionText = "When designing a high-throughput distributed database system that needs to handle millions of transactions per second, which trade-offs would you make regarding the CAP theorem?",
                    TopicId = FindTopic("Database Design").Id,
                    Difficulty = QuestionDifficultyLevel.Hard
                },
            };

        dbContext.Questions.AddRange(questions);
        await dbContext.SaveChangesAsync();

        Console.WriteLine($"Added {questions.Count} questions");
        return questions;
    }

    private async Task SeedAnswersAsync(InterviewPortalDbContext dbContext, List<Question> questions)
    {
        // C# Fundamentals
        var question1 = questions.FirstOrDefault(q => q.QuestionText.Contains("What is the difference between value types and reference types in C#?"));
        var question2 = questions.FirstOrDefault(q => q.QuestionText.Contains("What is the purpose of the \"using\" statement in C#?"));
        var question3 = questions.FirstOrDefault(q => q.QuestionText.Contains("Explain the difference between abstract classes and interfaces in C#"));

        // ASP.NET Core
        var question4 = questions.FirstOrDefault(q => q.QuestionText.Contains("Explain dependency injection in ASP.NET Core and how services are registered"));
        var question5 = questions.FirstOrDefault(q => q.QuestionText.Contains("What is the difference between IActionResult and ActionResult in ASP.NET"));
        var question6 = questions.FirstOrDefault(q => q.QuestionText.Contains("What is middleware in ASP.NET Core and how is it configured"));

        // Algorithm Design
        var question7 = questions.FirstOrDefault(q => q.QuestionText.Contains("time complexity of a basic binary search algorithm"));
        var question8 = questions.FirstOrDefault(q => q.QuestionText.Contains("greedy algorithms and dynamic programming"));
        var question9 = questions.FirstOrDefault(q => q.QuestionText.Contains("the traveling salesman problem, which approach would be most efficient for exactly solving the problem with 30 cities"));

        // Product Development
        var question10 = questions.FirstOrDefault(q => q.QuestionText.Contains("product manager and a project manager"));
        var question11 = questions.FirstOrDefault(q => q.QuestionText.Contains("In agile product development, what is the purpose of a sprint retrospective"));
        var question12 = questions.FirstOrDefault(q => q.QuestionText.Contains("product managers prioritize features in a situation where stakeholders have conflicting requests and development"));

        // Market Research
        var question13 = questions.FirstOrDefault(q => q.QuestionText.Contains("primary purpose of market research"));
        var question14 = questions.FirstOrDefault(q => q.QuestionText.Contains("difference between qualitative and quantitative market research"));
        var question15 = questions.FirstOrDefault(q => q.QuestionText.Contains("conjoint analysis in their market research strategy"));

        // UX Design Principles
        var question16 = questions.FirstOrDefault(q => q.QuestionText.Contains("UX design principle of 'visibility'"));
        var question17 = questions.FirstOrDefault(q => q.QuestionText.Contains("concept of cognitive load apply to UX design"));
        var question18 = questions.FirstOrDefault(q => q.QuestionText.Contains("balances accessibility for users with visual impairments"));

        // Database Design
        var question19 = questions.FirstOrDefault(q => q.QuestionText.Contains("purpose of normalization in database design"));
        var question20 = questions.FirstOrDefault(q => q.QuestionText.Contains("difference between a clustered and non-clustered index"));
        var question21 = questions.FirstOrDefault(q => q.QuestionText.Contains("CAP theorem"));

        var allAnswers = new List<Answer>();

        // C# Fundamentals Questions
        if (question1 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question1.Id,
                    AnswerText = "Value types store data directly on the stack, while reference types store a reference on the stack that points to data on the heap.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question1.Id,
                    AnswerText = "Value types store a reference, while reference types store data directly.",
                    IsCorrect = false
                }
            });
        }

        if (question2 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question2.Id,
                    AnswerText = "The 'using' statement ensures that disposable objects are properly disposed of when they go out of scope, even if an exception occurs.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question2.Id,
                    AnswerText = "The 'using' statement is only for importing namespaces and has no effect on resource management.",
                    IsCorrect = false
                }
            });
        }

        if (question3 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question3.Id,
                    AnswerText = "Abstract classes can contain implementation and state, while interfaces only declare methods/properties. Classes can inherit from one abstract class but implement multiple interfaces.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question3.Id,
                    AnswerText = "Abstract classes and interfaces are identical in functionality but interfaces are deprecated in modern C#.",
                    IsCorrect = false
                }
            });
        }

        // ASP.NET Core Questions
        if (question4 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question4.Id,
                   AnswerText = "DI in ASP.NET Core provides objects with dependencies rather than creating them. Services are registered in ConfigureServices using AddTransient, AddScoped, or AddSingleton.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question4.Id,
                    AnswerText = "Dependency injection is when you manually create new instances of objects throughout your code. Services are registered by adding them to a global static registry.",
                    IsCorrect = false
                }
            });
        }

        if (question5 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question5.Id,
                    AnswerText = "IActionResult is an interface that ActionResult implements. It allows more flexibility and enables returning different result types from controller actions.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question5.Id,
                    AnswerText = "ActionResult is used for synchronous operations, while IActionResult is specifically for asynchronous controller methods.",
                    IsCorrect = false
                }
            });
        }

        if (question6 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question6.Id,
                    AnswerText = "Middleware handles requests and responses. It's configured in Configure method using app.UseXxx methods, forming a pipeline where each component processes requests sequentially.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question6.Id,
                    AnswerText = "Middleware in ASP.NET Core refers to third-party packages that can only be configured through XML configuration files and cannot be customized for specific applications.",
                    IsCorrect = false
                }
            });
        }

        // Algorithm Design Questions
        if (question7 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question7.Id,
                    AnswerText = "O(log n)",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question7.Id,
                    AnswerText = "O(n^2)",
                    IsCorrect = false
                }
            });
        }

        if (question8 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question8.Id,
                    AnswerText = "Greedy algorithms make locally optimal choices at each step, while dynamic programming solves overlapping subproblems and stores their solutions.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question8.Id,
                    AnswerText = "Greedy algorithms are always slower than dynamic programming but more memory efficient.",
                    IsCorrect = false
                }
            });
        }

        if (question9 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question9.Id,
                    AnswerText = "Branch and bound with problem-specific heuristics",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question9.Id,
                    AnswerText = "A basic brute force algorithm that evaluates all possible permutations",
                    IsCorrect = false
                }
            });
        }

        // Product Development Questions
        if (question10 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question10.Id,
                    AnswerText = "A product manager focuses on what product to build and why, while a project manager focuses on how and when to build it.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question10.Id,
                    AnswerText = "A product manager is responsible for coding the product, while a project manager creates the user interfaces.",
                    IsCorrect = false
                }
            });
        }

        if (question11 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question11.Id,
                    AnswerText = "To reflect on the past sprint and identify opportunities for improving processes in future sprints.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question11.Id,
                    AnswerText = "To plan feature development for the next product release and assign tasks to team members.",
                    IsCorrect = false
                }
            });
        }

        if (question12 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question12.Id,
                    AnswerText = "By evaluating each feature's impact on key metrics, engineering effort required, strategic alignment, and using a quantitative scoring system like RICE (Reach, Impact, Confidence, Effort).",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question12.Id,
                    AnswerText = "By implementing all features requested by the highest-ranking stakeholder first, regardless of effort or impact.",
                    IsCorrect = false
                }
            });
        }

        // Market Research Questions
        if (question13 != null)
        {
                allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question13.Id,
                    AnswerText = "To gather and analyze information about target markets and customers to inform business decisions.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question13.Id,
                    AnswerText = "To create advertisements that will appeal to all potential customers.",
                    IsCorrect = false
                }
            });
        }

        if (question14 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question14.Id,
                   AnswerText = "Qualitative research explores attitudes through focus groups, while quantitative research collects numerical data through surveys and statistical analysis.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question14.Id,
                    AnswerText = "Qualitative research is always more accurate than quantitative research because it involves direct customer interviews.",
                    IsCorrect = false
                }
            });
        }

        if (question15 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question15.Id,
                    AnswerText = "By determining the importance of different product attributes and price points by asking consumers to make trade-offs between different product configurations.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question15.Id,
                    AnswerText = "By analyzing competitors' marketing campaigns and replicating their most successful strategies with minimal modifications.",
                    IsCorrect = false
                }
            });
        }

        // UX Design Principles Questions
        if (question16 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question16.Id,
                    AnswerText = "Making system status and available actions clear to users so they know what's happening and what they can do.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question16.Id,
                    AnswerText = "Using bright colors and large fonts to make the interface more noticeable.",
                    IsCorrect = false
                }
            });
        }

        if (question17 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question17.Id,
                    AnswerText = "Designers should minimize mental effort required by users by simplifying interfaces, chunking information, and using familiar patterns.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question17.Id,
                    AnswerText = "Designers should challenge users with complex interfaces to keep them engaged and prevent boredom.",
                    IsCorrect = false
                }
            });
        }

        if (question18 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question18.Id,
                   AnswerText = "Implement equivalent experiences through different modalities using ARIA attributes, keyboard navigation, screen reader support, and maintaining sufficient contrast ratios.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question18.Id,
                    AnswerText = "Create a separate 'accessibility mode' that users must explicitly enable, which simplifies the interface at the expense of features available to sighted users.",
                    IsCorrect = false
                }
            });
        }

        // Database Design Questions
        if (question19 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question19.Id,
                    AnswerText = "To organize data to reduce redundancy and improve data integrity.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question19.Id,
                    AnswerText = "To increase the speed of all database queries by adding more indexes.",
                    IsCorrect = false
                }
            });
        }

        if (question20 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question20.Id,
                    AnswerText = "A clustered index determines physical data order and is limited to one per table. Non-clustered indexes create separate reference structures and multiple can exist per table.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question20.Id,
                    AnswerText = "A clustered index can only be applied to numeric columns, while non-clustered indexes work with any data type.",
                    IsCorrect = false
                }
            });
        }

        if (question21 != null)
        {
            allAnswers.AddRange(new List<Answer>
            {
                new Answer
                {
                    QuestionId = question21.Id,
                    AnswerText = "Sacrifice strong consistency for availability and partition tolerance, implementing eventual consistency models with appropriate conflict resolution strategies.",
                    IsCorrect = true
                },
                new Answer
                {
                    QuestionId = question21.Id,
                    AnswerText = "Always prioritize strong consistency regardless of performance implications, as data integrity must be preserved at all costs in any database system.",
                    IsCorrect = false
                }
            });
        }

        dbContext.Answers.AddRange(allAnswers);
        await dbContext.SaveChangesAsync();

        Console.WriteLine($"Added {allAnswers.Count} answers");
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
    }


    private async Task AssignRolesToUsersAsync(UserManager<User> userManager)
    {
        // Define the exact role mapping
        var userRoleMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "admin@interviewportal.com", "Admin" },
        { "hr@interviewportal.com", "HR" }
    };
        var allUsers = await userManager.Users.ToListAsync();

        foreach (var user in allUsers)
        {
            var roles = await userManager.GetRolesAsync(user);
            Console.WriteLine($"User: {user.Email}, Current Roles: [{string.Join(", ", roles)}]");
        }

        foreach (var user in allUsers)
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            if (currentRoles.Any())
            {
                Console.WriteLine($"  - Removing roles: [{string.Join(", ", currentRoles)}]");
                var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);

                if (removeResult.Succeeded)
                {
                    Console.WriteLine("  - Successfully removed all roles");
                }
                else
                {
                    Console.WriteLine($"  - Failed to remove roles: {string.Join(", ", removeResult.Errors.Select(e => e.Description))}");
                }

                var rolesAfterRemoval = await userManager.GetRolesAsync(user);
                Console.WriteLine($"  - Roles after removal: [{string.Join(", ", rolesAfterRemoval)}]");
            }
            else
            {
                Console.WriteLine("  - User has no roles to remove");
            }

            if (userRoleMapping.ContainsKey(user.Email))
            {
                // Admin or HR user
                string roleName = userRoleMapping[user.Email];
                Console.WriteLine($"  - Adding role: {roleName}");

                var addResult = await userManager.AddToRoleAsync(user, roleName);
                if (addResult.Succeeded)
                {
                    Console.WriteLine($"  - Successfully added {roleName} role");
                }
                else
                {
                    Console.WriteLine($"  - Failed to add role: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
                }
            }
            else if (user.Email.Contains("candidate", StringComparison.OrdinalIgnoreCase))
            {
                // Candidate
                var addResult = await userManager.AddToRoleAsync(user, "Candidate");
                if (addResult.Succeeded)
                {
                    Console.WriteLine("  - Successfully added Candidate role");
                }
                else
                {
                    Console.WriteLine($"  - Failed to add role: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine("  - No matching role pattern, user left without roles");
            }

            // Verify final roles
            var finalRoles = await userManager.GetRolesAsync(user);
        }

        foreach (var user in allUsers)
        {
            var roles = await userManager.GetRolesAsync(user);
            Console.WriteLine($"User: {user.Email}, Final Roles: [{string.Join(", ", roles)}]");
        }
    }

    private async Task SeedInterviewResultsAsync(InterviewPortalDbContext dbContext, UserManager<User> userManager)
    {
        try
        {
            // Get all candidates
            var candidates = await userManager.GetUsersInRoleAsync("Candidate");
            if (!candidates.Any())
            {
                Console.WriteLine("No candidate users found. Skipping interview results seeding.");
                return;
            }

            // Get all positions with topics
            var positions = await dbContext.Positions
                .Include(p => p.PositionTopics)
                    .ThenInclude(pt => pt.Topic)
                .Where(p => p.IsActive)
                .ToListAsync();

            if (!positions.Any())
            {
                Console.WriteLine("No positions found. Skipping interview results seeding.");
                return;
            }

            var random = new Random();

            // For each candidate
            foreach (var candidate in candidates)
            {
                Console.WriteLine($"Creating interview results for {candidate.Email}...");

                // For each position
                foreach (var position in positions)
                {
                    // Check if the position has topics
                    if (!position.PositionTopics.Any())
                    {
                        Console.WriteLine($"Position {position.Name} has no topics. Skipping.");
                        continue;
                    }

                    // Skip if a session already exists for this candidate and position
                    if (await dbContext.InterviewSessions.AnyAsync(s => s.UserId == candidate.Id && s.PositionId == position.Id))
                    {
                        Console.WriteLine($"Session already exists for {candidate.Email} for position {position.Name}. Skipping.");
                        continue;
                    }

                    Console.WriteLine($"Processing position: {position.Name}");

                    var daysAgo = random.Next(1, 10);
                    var completionTime = random.Next(30, 120);
                    var startDate = DateTime.Now.AddDays(-daysAgo);

                    var session = new InterviewSession
                    {
                        UserId = candidate.Id,
                        PositionId = position.Id,
                        StartedAt = startDate,
                        CompletedAt = startDate.AddMinutes(completionTime),
                        DurationInSeconds = completionTime * 60,
                        IsMock = false,
                        UserAnswers = new List<UserAnswer>()
                    };

                    dbContext.InterviewSessions.Add(session);
                    await dbContext.SaveChangesAsync();

                    Console.WriteLine($"Created interview session for {candidate.Email} for position {position.Name}");

                    int totalQuestions = 0;
                    int totalCorrect = 0;

                    // Add answers for each topic in this position
                    foreach (var positionTopic in position.PositionTopics)
                    {
                        // Get questions for this topic
                        var questions = await dbContext.Questions
                            .Include(q => q.Answers)
                            .Where(q => q.TopicId == positionTopic.TopicId)
                            .ToListAsync();

                        // Add answers for each question
                        foreach (var question in questions)
                        {
                            totalQuestions++;

                            double correctnessFactor = 0.7;

                            if (candidate.Email.Contains("1"))
                                correctnessFactor = 0.9;
                            else if (candidate.Email.Contains("2"))
                                correctnessFactor = 0.85;
                            else if (candidate.Email.Contains("3"))
                                correctnessFactor = 0.8;
                            else if (candidate.Email.Contains("4"))
                                correctnessFactor = 0.7;
                            else if (candidate.Email.Contains("5"))
                                correctnessFactor = 0.65;
                            else if (candidate.Email.Contains("6"))
                                correctnessFactor = 0.55;
                            else if (candidate.Email.Contains("7"))
                                correctnessFactor = 0.45;

                            if (question.Difficulty == QuestionDifficultyLevel.Easy)
                                correctnessFactor += 0.1;
                            else if (question.Difficulty == QuestionDifficultyLevel.Hard)
                                correctnessFactor -= 0.15;

                            bool isCorrectAnswer = random.NextDouble() < correctnessFactor;

                            var answer = isCorrectAnswer
                                ? question.Answers.FirstOrDefault(a => a.IsCorrect)
                                : question.Answers.FirstOrDefault(a => !a.IsCorrect);

                            answer ??= question.Answers.FirstOrDefault();

                            if (answer != null)
                            {
                                // Create user answer
                                var userAnswer = new UserAnswer
                                {
                                    UserId = candidate.Id,
                                    QuestionId = question.Id,
                                    AnswerId = answer.Id,
                                    AnsweredAt = session.StartedAt.AddMinutes(random.Next(1, completionTime)),
                                    InterviewSessionId = session.Id
                                };

                                dbContext.UserAnswers.Add(userAnswer);

                                if (answer.IsCorrect)
                                    totalCorrect++;
                            }
                        }
                    }

                    await dbContext.SaveChangesAsync();
                    Console.WriteLine($"Created {totalQuestions} answers for session, {totalCorrect} correct");

                    // Create a final result
                    double percentage = totalQuestions > 0 ? (double)totalCorrect / totalQuestions * 100 : 0;

                    var result = new Result
                    {
                        UserId = candidate.Id,
                        InterviewSessionId = session.Id,
                        FinalScore = (int)Math.Round(percentage),
                    };

                    dbContext.Results.Add(result);
                    await dbContext.SaveChangesAsync();

                    Console.WriteLine($"Created result for {candidate.Email} with score {percentage:F1}%");
                }
            }

            Console.WriteLine("Interview results seeded successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error seeding interview results: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }
}