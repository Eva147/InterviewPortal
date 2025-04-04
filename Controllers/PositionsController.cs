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
        int id, string Name,
        Dictionary<int, string> Questions,
        Dictionary<int, string> Answers,
        string NewQuestion,
        string NewAnswerText,
        int? NewAnswerQuestionId)
    {
        var topic = await _context.Topics
            .Include(t => t.Questions)
                .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (topic == null) return NotFound();

        topic.Name = Name;

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

        if (!string.IsNullOrWhiteSpace(NewQuestion))
        {
            topic.Questions.Add(new Question
            {
                QuestionText = NewQuestion
            });
        }

        if (!string.IsNullOrWhiteSpace(NewAnswerText) && NewAnswerQuestionId.HasValue)
        {
            var questionToUpdate = topic.Questions.FirstOrDefault(q => q.Id == NewAnswerQuestionId.Value);
            if (questionToUpdate != null)
            {
                questionToUpdate.Answers.Add(new Answer { AnswerText = NewAnswerText });
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
    public async Task<IActionResult> CreateTopic(string Name, Dictionary<int, string> Questions, Dictionary<int, Dictionary<int, string>> Answers)
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

            var newQuestion = new Question { QuestionText = questionText, Answers = new List<Answer>() };

            if (Answers.TryGetValue(questionEntry.Key, out var answers))
            {
                foreach (var answerEntry in answers)
                {
                    if (!string.IsNullOrWhiteSpace(answerEntry.Value))
                    {
                        newQuestion.Answers.Add(new Answer { AnswerText = answerEntry.Value });
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