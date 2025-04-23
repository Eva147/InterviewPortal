namespace InterviewPortal.Controllers;

[Authorize]
public class TopicController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<TopicController> _logger;

    /// <summary>
    /// Constructor for TopicController
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userManager"></param>
    /// <param name="logger"></param>
    public TopicController(
        InterviewPortalDbContext context,
        UserManager<User> userManager,
        ILogger<TopicController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Create a new topic and assign it to a position
    /// </summary>
    /// <param name="Name"></param>
    /// <param name="PositionId"></param>
    /// <param name="Questions"></param>
    /// <param name="Difficulty"></param>
    /// <param name="Answers"></param>
    /// <param name="IsCorrect"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> CreateTopic(
       string Name,
       int PositionId,
       Dictionary<int, string> Questions,
       Dictionary<int, int> Difficulty,
       Dictionary<int, Dictionary<int, string>> Answers,
       Dictionary<int, Dictionary<int, string>> IsCorrect)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                TempData["Error"] = "Topic name is required.";
                return RedirectToAction("Index", "Position");
            }

            if (Questions == null || !Questions.Any())
            {
                TempData["Error"] = "At least one question is required.";
                return RedirectToAction("Index", "Position");
            }

            var position = await _context.Positions
                .Include(p => p.PositionTopics)
                .FirstOrDefaultAsync(p => p.Id == PositionId);

            if (position == null)
            {
                _logger.LogWarning("CreateTopic: Position with ID {PositionId} not found", PositionId);
                TempData["Error"] = "Selected position does not exist.";
                return RedirectToAction("Index", "Position");
            }

            var newTopic = new Topic
            {
                Name = Name.Trim(),
                Questions = new List<Question>()
            };

            foreach (var questionEntry in Questions)
            {
                var questionText = questionEntry.Value?.Trim();
                if (string.IsNullOrWhiteSpace(questionText)) continue;

                QuestionDifficultyLevel difficultyLevel = QuestionDifficultyLevel.Easy;
                if (Difficulty != null && Difficulty.TryGetValue(questionEntry.Key, out var difficultyValue))
                {
                    if (!Enum.IsDefined(typeof(QuestionDifficultyLevel), difficultyValue))
                    {
                        difficultyLevel = QuestionDifficultyLevel.Easy;
                    }
                    else
                    {
                        difficultyLevel = (QuestionDifficultyLevel)difficultyValue;
                    }
                }

                var newQuestion = new Question
                {
                    QuestionText = questionText,
                    Difficulty = difficultyLevel,
                    Answers = new List<Answer>()
                };

                if (Answers.TryGetValue(questionEntry.Key, out var answers))
                {
                    foreach (var answerEntry in answers)
                    {
                        var answerText = answerEntry.Value?.Trim();
                        if (!string.IsNullOrWhiteSpace(answerText))
                        {
                            bool isCorrect = false;

                            if (IsCorrect != null &&
                                IsCorrect.TryGetValue(questionEntry.Key, out var correctAnswers) &&
                                correctAnswers.ContainsKey(answerEntry.Key))
                            {
                                isCorrect = true;
                            }

                            newQuestion.Answers.Add(new Answer
                            {
                                AnswerText = answerText,
                                IsCorrect = isCorrect
                            });
                        }
                    }
                }

                // Validate at least one correct answer
                if (!newQuestion.Answers.Any(a => a.IsCorrect))
                {
                    TempData["Error"] = $"Question '{questionText}' must have at least one correct answer.";
                    return RedirectToAction("Index", "Position");
                }

                newTopic.Questions.Add(newQuestion);
            }

            // Validate at least one question was added
            if (!newTopic.Questions.Any())
            {
                TempData["Error"] = "At least one valid question is required.";
                return RedirectToAction("Index", "Position");
            }

            await _context.Topics.AddAsync(newTopic);
            await _context.SaveChangesAsync();

            position.PositionTopics.Add(new PositionTopic
            {
                TopicId = newTopic.Id,
                PositionId = position.Id
            });

            await _context.SaveChangesAsync();

            TempData["Message"] = "Topic created successfully and assigned to position!";
            return RedirectToAction("Index", "Position");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating topic for position ID {PositionId}", PositionId);
            TempData["Error"] = "An error occurred while creating the topic.";
            return RedirectToAction("Index", "Position");
        }
    }

    /// <summary>
    /// Edit the topics properties alongised their questions and answers
    /// </summary>
    /// <param name="id"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> EditTopic(int id, TopicEditViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                _logger.LogWarning("EditTopic: Model validation failed with errors: {Errors}", errors);

                TempData["Error"] = "Please check your inputs and try again.";
                return RedirectToAction("Index", "Position");
            }

            var topic = await _context.Topics
                .Include(t => t.Questions)
                    .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topic == null)
            {
                _logger.LogWarning("EditTopic: Topic with ID {TopicId} not found", id);
                return NotFound();
            }

            // Update topic name
            topic.Name = model.Name?.Trim() ?? string.Empty;

            // Validate questions
            if (model.Questions == null || !model.Questions.Any())
            {
                TempData["Error"] = "Topic must contain at least one question.";
                return RedirectToAction("Index", "Position");
            }

            // Update existing questions
            foreach (var questionModel in model.Questions)
            {
                if (string.IsNullOrWhiteSpace(questionModel.QuestionText?.Trim()))
                {
                    TempData["Error"] = "Question text cannot be empty.";
                    return RedirectToAction("Index", "Position");
                }

                if (questionModel.Id > 0) 
                {
                    var question = topic.Questions.FirstOrDefault(q => q.Id == questionModel.Id);
                    if (question != null)
                    {
                        question.QuestionText = questionModel.QuestionText.Trim();
                        question.Difficulty = questionModel.Difficulty;

                        // Validate answers
                        if (questionModel.Answers == null || !questionModel.Answers.Any())
                        {
                            TempData["Error"] = $"Question '{questionModel.QuestionText}' must have at least one answer.";
                            return RedirectToAction("Index", "Position");
                        }

                        // Handle answers
                        foreach (var answerModel in questionModel.Answers)
                        {
                            if (string.IsNullOrWhiteSpace(answerModel.AnswerText?.Trim()))
                            {
                                TempData["Error"] = "Answer text cannot be empty.";
                                return RedirectToAction("Index", "Position");
                            }

                            if (answerModel.Id > 0) // Existing answer
                            {
                                var answer = question.Answers.FirstOrDefault(a => a.Id == answerModel.Id);
                                if (answer != null)
                                {
                                    answer.AnswerText = answerModel.AnswerText.Trim();
                                    answer.IsCorrect = answerModel.IsCorrect;
                                }
                            }
                            else // New answer
                            {
                                question.Answers.Add(new Answer
                                {
                                    AnswerText = answerModel.AnswerText.Trim(),
                                    IsCorrect = answerModel.IsCorrect
                                });
                            }
                        }

                        // Validate at least one correct answer
                        if (!questionModel.Answers.Any(a => a.IsCorrect))
                        {
                            TempData["Error"] = $"Question '{questionModel.QuestionText}' must have at least one correct answer.";
                            return RedirectToAction("Index", "Position");
                        }

                        // Remove answers that aren't in the model
                        var answerIdsToKeep = questionModel.Answers.Where(a => a.Id > 0).Select(a => a.Id).ToList();
                        foreach (var answer in question.Answers.ToList())
                        {
                            if (!answerIdsToKeep.Contains(answer.Id))
                            {
                                _context.Answers.Remove(answer);
                            }
                        }
                    }
                }
                else // New question
                {
                    if (questionModel.Answers == null || !questionModel.Answers.Any())
                    {
                        TempData["Error"] = $"Question '{questionModel.QuestionText}' must have at least one answer.";
                        return RedirectToAction("Index", "Position");
                    }

                    // Validate at least one correct answer
                    if (!questionModel.Answers.Any(a => a.IsCorrect))
                    {
                        TempData["Error"] = $"Question '{questionModel.QuestionText}' must have at least one correct answer.";
                        return RedirectToAction("Index", "Position");
                    }

                    var newQuestion = new Question
                    {
                        QuestionText = questionModel.QuestionText.Trim(),
                        Difficulty = questionModel.Difficulty,
                        Answers = questionModel.Answers.Select(a => new Answer
                        {
                            AnswerText = a.AnswerText?.Trim() ?? string.Empty,
                            IsCorrect = a.IsCorrect
                        }).ToList()
                    };
                    topic.Questions.Add(newQuestion);
                }
            }

            // Remove questions that aren't in the model
            var questionIdsToKeep = model.Questions.Where(q => q.Id > 0).Select(q => q.Id).ToList();
            foreach (var question in topic.Questions.ToList())
            {
                if (!questionIdsToKeep.Contains(question.Id))
                {
                    _context.Questions.Remove(question);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Message"] = "Topic updated successfully!";
            return RedirectToAction("Index", "Position");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing topic with ID {TopicId}", id);
            TempData["Error"] = "An error occurred while updating the topic.";
            return RedirectToAction("Index", "Position");
        }
    }
}

public class TopicEditViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Topic name is required.")]
    [StringLength(100, ErrorMessage = "Topic name cannot exceed 100 characters.")]
    public string? Name { get; set; }

    public List<QuestionEditViewModel> Questions { get; set; } = new List<QuestionEditViewModel>();
}

public class QuestionEditViewModel
{
    public int Id { get; set; } // 0 for new questions

    [Required(ErrorMessage = "Question text is required.")]
    [StringLength(500, ErrorMessage = "Question text cannot exceed 500 characters.")]
    public string? QuestionText { get; set; }

    [Required]
    public QuestionDifficultyLevel Difficulty { get; set; }

    [MinLength(1, ErrorMessage = "At least one answer is required.")]
    public List<AnswerEditViewModel> Answers { get; set; } = new List<AnswerEditViewModel>();
}

public class AnswerEditViewModel
{
    public int Id { get; set; } // 0 for new answers

    [Required(ErrorMessage = "Answer text is required.")]
    [StringLength(200, ErrorMessage = "Answer text cannot exceed 200 characters.")]
    public string? AnswerText { get; set; }

    public bool IsCorrect { get; set; }
}