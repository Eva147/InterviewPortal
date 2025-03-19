using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using InterviewPortal.DbContexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InterviewPortal.Services
{
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

            try
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);

                if (!dbContext.Positions.Any())
                {
                    var users = await SeedUsersAsync(userManager);
                    var positions = await SeedPositionsAsync(dbContext);
                    var topics = await SeedTopicsAsync(dbContext);
                    await SeedPositionTopicsAsync(dbContext, positions, topics);
                    var questions = await SeedQuestionsAsync(dbContext, topics);
                    await SeedAnswersAsync(dbContext, users, questions);
                    await SeedResultsAsync(dbContext, users, positions);

                    Console.WriteLine("Database seeded successfully!");
                }
                else
                {
                    Console.WriteLine("Database already contains data.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during db initialisation: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("DatabaseInitializer - Initialization process completed");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task<List<User>> SeedUsersAsync(UserManager<User> userManager)
        {
            var users = new List<User>();

            var adminUser = new User
            {
                UserName = "admin@interviewportal.com",
                Email = "admin@interviewportal.com",
                EmailConfirmed = true,
                FirstName = "Admin",
                LastName = "Admin"
            };

            if (await userManager.FindByEmailAsync(adminUser.Email) == null)
            {
                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    users.Add(adminUser);
                    Console.WriteLine($"Created admin user: {adminUser.Email}");
                }
            }
            else
            {
                var existingUser = await userManager.FindByEmailAsync(adminUser.Email);
                if (existingUser != null)
                {
                    users.Add(existingUser);
                }
            }

            var candidateUser = new User
            {
                UserName = "candidate@example.com",
                Email = "candidate@example.com",
                EmailConfirmed = true,
                FirstName = "John",
                LastName = "Smith"
            };

            if (await userManager.FindByEmailAsync(candidateUser.Email) == null)
            {
                var result = await userManager.CreateAsync(candidateUser, "Candidate123!");
                if (result.Succeeded)
                {
                    users.Add(candidateUser);
                    Console.WriteLine($"Created test user: {candidateUser.Email}");
                }
            }
            else
            {
                var existingUser = await userManager.FindByEmailAsync(candidateUser.Email);
                if (existingUser != null)
                {
                    users.Add(existingUser);
                }

            }

            return users;
        }

        private async Task<List<Position>> SeedPositionsAsync(InterviewPortalDbContext dbContext)
        {
            var positions = new List<Position>
            {
                new Position
                {
                    Name = "Software Engineer",
                    PassScore = 0,
                },
                new Position
                {
                    Name = "Product Manager",
                    PassScore = 0,
                }
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
                new Topic
                {
                    Name = "C# Fundamentals",
                    Description = "Basic C# programming concepts and syntax"
                },
                new Topic
                {
                    Name = "ASP.NET Core",
                    Description = "Web development with ASP.NET Core framework"
                }
            };

            dbContext.Topics.AddRange(topics);
            await dbContext.SaveChangesAsync();

            Console.WriteLine($"Added {topics.Count} topics");
            return topics;
        }

        private async Task SeedPositionTopicsAsync(
            InterviewPortalDbContext dbContext,
            List<Position> positions,
            List<Topic> topics)
        {
            Position FindPosition(string name) => positions.First(p => p.Name == name);

            var positionTopics = new List<PositionTopic>
            {
                new PositionTopic
                {
                    Position = FindPosition("Software Engineer"),
                    TopicId = 1,
                },
                new PositionTopic
                {
                    Position = FindPosition("Product Manager"),
                    TopicId = 2,
                }
            };

            dbContext.PositionTopics.AddRange(positionTopics);
            await dbContext.SaveChangesAsync();

            Console.WriteLine($"Added {positionTopics.Count} position-topic relationships");
        }

        private async Task<List<Question>> SeedQuestionsAsync(
            InterviewPortalDbContext dbContext,
            List<Topic> topics)
        {
            Topic FindTopic(string name) => topics.First(t => t.Name == name);

            var questions = new List<Question>
            {
                new Question
                {
                    QuestionText = "What is the difference between value types and reference types in C#?",
                    CorrectAnswer = "Value types (int, float, structs) contain their data directly and are stored on the stack. Reference types (classes, interfaces) store a reference to their data on the heap.",
                    Topic = FindTopic("C# Fundamentals"),
                    Score = 1,
                    Difficulty = QuestionDifficultyLevel.Easy,
                },
                new Question
                {
                    QuestionText = "What is middleware in ASP.NET Core and how is it configured?",
                    CorrectAnswer = "Middleware are components that form a pipeline to handle requests and responses. They're configured in the Program.cs file using the Use/Run/Map methods.",
                    Topic = FindTopic("ASP.NET Core"),
                    Score = 2,
                    Difficulty = QuestionDifficultyLevel.Medium,
                   
                }
            };

            dbContext.Questions.AddRange(questions);
            await dbContext.SaveChangesAsync();

            Console.WriteLine($"Added {questions.Count} questions");
            return questions;
        }

        private async Task SeedAnswersAsync(
            InterviewPortalDbContext dbContext,
            List<User> users,
            List<Question> questions)
        {
            var candidateUser = users.FirstOrDefault(u => u.Email == "candidate@example.com");
            if (candidateUser == null) return;

            var question = questions.FirstOrDefault();
            if (question == null) return;

            var answer = new Answer
            {
                UserAnswer = "Value types store their data directly in memory allocated on the stack, while reference types store a reference (memory address) to the actual data which is stored on the heap.",
                IsCorrect = true,
                AnsweredAt = DateTime.Now.AddDays(-3),
                UserId = candidateUser.Id,
                QuestionId = question.Id,
            };

            dbContext.Answers.AddRange(answer);
            await dbContext.SaveChangesAsync();

            Console.WriteLine("Added a sample answer");
        }

        private async Task SeedResultsAsync(
            InterviewPortalDbContext dbContext,
            List<User> users,
            List<Position> positions)
        {
            var candidateUser = users.FirstOrDefault(u => u.Email == "candidate@example.com");
            if (candidateUser == null) return;

            var position = positions.FirstOrDefault(p => p.Name == "Software Engineer");
            if (position == null) return;

            var result = new Result
            {
                FinalScore = 8,
                UserId = candidateUser.Id,
                PositionId = position.Id,
            };

            dbContext.Results.Add(result);
            await dbContext.SaveChangesAsync();

            Console.WriteLine("Added a sample interview result");
        }
    }
}