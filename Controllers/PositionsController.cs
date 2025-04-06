namespace InterviewPortal.Controllers;

[Authorize]
public class PositionController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly UserManager<User> _userManager;

    public PositionController(InterviewPortalDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var positions = await _context.Positions
                .Where(p => p.IsActive)
                .Include(p => p.PositionTopics)
                    .ThenInclude(pt => pt.Topic)
                        .ThenInclude(t => t.Questions)
                            .ThenInclude(q => q.Answers)
                .ToListAsync();

            ViewBag.AllTopics = await _context.Topics
                .Include(t => t.Questions)
                    .ThenInclude(q => q.Answers)
                .ToListAsync();

            return View(positions);
        }
        catch
        {
            ModelState.AddModelError("", "An error occurred while retrieving positions.");
            ViewBag.AllTopics = new List<Topic>();
            return View(new List<Position>());
        }
    }

    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> CreatePosition(string Name, List<int> Topics)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError("", "Position name is required.");
            return RedirectToAction("Index");
        }

        var newPosition = new Position { Name = Name, PositionTopics = new List<PositionTopic>() };

        foreach (var topicId in Topics)
        {
            newPosition.PositionTopics.Add(new PositionTopic { TopicId = topicId });
        }

        _context.Positions.Add(newPosition);
        await _context.SaveChangesAsync();

        TempData["Message"] = "Position created successfully!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> EditPosition(int id, string Name, List<int> Topics)
    {
        var position = await _context.Positions
            .Include(p => p.PositionTopics)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (position == null) return NotFound();

        position.Name = Name;
        position.PositionTopics.Clear();

        foreach (var topicId in Topics)
        {
            position.PositionTopics.Add(new PositionTopic { TopicId = topicId });
        }

        await _context.SaveChangesAsync();
        TempData["Message"] = "Position updated successfully!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> EditTopic(
        int id,
        string Name,
        Dictionary<int, string> Questions,
        Dictionary<int, string> Answers,
        Dictionary<int, int> Difficulty,
        Dictionary<int, Dictionary<int, string>> NewAnswers,
        Dictionary<int, Dictionary<int, string>> IsCorrect)
    {
        var topic = await _context.Topics
            .Include(t => t.Questions)
                .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (topic == null) return NotFound();

        topic.Name = Name;

        Questions = Questions ?? new Dictionary<int, string>();
        Answers = Answers ?? new Dictionary<int, string>();
        Difficulty = Difficulty ?? new Dictionary<int, int>();
        NewAnswers = NewAnswers ?? new Dictionary<int, Dictionary<int, string>>();
        IsCorrect = IsCorrect ?? new Dictionary<int, Dictionary<int, string>>();

        // Update existing questions and answers
        foreach (var question in topic.Questions)
        {
            if (Questions.TryGetValue(question.Id, out var newText))
            {
                question.QuestionText = newText;
            }

            foreach (var answer in question.Answers)
            {
                if (Answers.TryGetValue(answer.Id, out var answerText))
                {
                    answer.AnswerText = answerText;
                }
            }
        }

        // Handle new questions
        foreach (var questionEntry in NewAnswers ?? new Dictionary<int, Dictionary<int, string>>())
        {
            int questionIdx = questionEntry.Key;

            // Check if we have a question text for this index
            if (Questions.TryGetValue(questionIdx, out var questionText) && !string.IsNullOrWhiteSpace(questionText))
            {
                // Get difficulty level
                QuestionDifficultyLevel difficultyLevel = QuestionDifficultyLevel.Easy;
                if (Difficulty != null && Difficulty.TryGetValue(questionIdx, out var diffValue))
                {
                    difficultyLevel = (QuestionDifficultyLevel)diffValue;
                }

                var newQuestion = new Question
                {
                    QuestionText = questionText,
                    Difficulty = difficultyLevel,
                    Answers = new List<Answer>()
                };

                // Add answers to the new question
                if (questionEntry.Value != null)
                {
                    foreach (var answerEntry in questionEntry.Value)
                    {
                        if (!string.IsNullOrWhiteSpace(answerEntry.Value))
                        {
                            bool isCorrect = false;

                            if (IsCorrect != null &&
                                IsCorrect.TryGetValue(questionIdx, out var correctAnswers) &&
                                correctAnswers.ContainsKey(answerEntry.Key))
                            {
                                isCorrect = true;
                            }

                            newQuestion.Answers.Add(new Answer
                            {
                                AnswerText = answerEntry.Value,
                                IsCorrect = isCorrect
                            });
                        }
                    }
                }

            // Add the new question to the topic
            topic.Questions.Add(newQuestion);
            }
        }

        await _context.SaveChangesAsync();
        TempData["Message"] = "Topic updated successfully!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> DeleteQuestion(int questionId)
    {
        var question = await _context.Questions
            .Include(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null) return NotFound();

        _context.Questions.Remove(question);
        await _context.SaveChangesAsync();

        TempData["Message"] = "Question deleted successfully!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> DeleteAnswer(int answerId)
    {
        var answer = await _context.Answers.FindAsync(answerId);
        if (answer == null) return NotFound();

        _context.Answers.Remove(answer);
        await _context.SaveChangesAsync();

        TempData["Message"] = "Answer deleted successfully!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> CreateTopic(string Name, Dictionary<int, string> Questions, Dictionary<int, int> Difficulty, Dictionary<int, Dictionary<int, string>> Answers, Dictionary<int, Dictionary<int, string>> IsCorrect)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError("", "Topic name is required.");
            return RedirectToAction("Index");
        }

        var newTopic = new Topic { Name = Name, Questions = new List<Question>() };

        foreach (var questionEntry in Questions)
        {
            var questionText = questionEntry.Value;
            if (string.IsNullOrWhiteSpace(questionText)) continue;

            QuestionDifficultyLevel difficultyLevel = QuestionDifficultyLevel.Easy;
            if (Difficulty != null && Difficulty.TryGetValue(questionEntry.Key, out var difficultyValue))
            {
                difficultyLevel = (QuestionDifficultyLevel)difficultyValue;
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
                    if (!string.IsNullOrWhiteSpace(answerEntry.Value))
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
                            AnswerText = answerEntry.Value,
                            IsCorrect = isCorrect
                        });
                    }
                }
            }

            newTopic.Questions.Add(newQuestion);
        }

        _context.Topics.Add(newTopic);
        await _context.SaveChangesAsync();

        TempData["Message"] = "Topic created successfully!";
        return RedirectToAction("Index");
    }
}