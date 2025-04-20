namespace InterviewPortal.Controllers;
public class InterviewSessionController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly ILogger<InterviewSessionController> _logger;

    public InterviewSessionController(InterviewPortalDbContext context, ILogger<InterviewSessionController> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Displays the interview session with randomized questions
    /// Questions persist in session to prevent reshuffling on refresh
    /// </summary>
    public async Task<IActionResult> Index(int sessionId)
    {
        try
        {
            // Load interview session with all related data
            var interviewSession = await _context.InterviewSessions
                .Include(s => s.Position)
                    .ThenInclude(p => p.PositionTopics)
                        .ThenInclude(pt => pt.Topic)
                            .ThenInclude(t => t!.Questions)
                                .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (interviewSession == null)
            {
                _logger.LogWarning("Interview session not found for ID: {SessionId}", sessionId);
                return NotFound();
            }

            // Get all available questions for this position
            var allQuestions = interviewSession.Position?.PositionTopics?
                .SelectMany(pt => pt.Topic?.Questions ?? Enumerable.Empty<Question>())
                .ToList() ?? new List<Question>();

            List<Question> selectedQuestions;

            // Check if we have cached questions in session
            var sessionQuestionsJson = HttpContext.Session.GetString($"InterviewQuestions_{sessionId}");

            if (!string.IsNullOrEmpty(sessionQuestionsJson))
            {
                // Load questions from session to maintain consistency
                var questionIds = JsonConvert.DeserializeObject<List<int>>(sessionQuestionsJson) ?? new List<int>();
                selectedQuestions = allQuestions
                    .Where(q => q != null && questionIds.Contains(q.Id))
                    .OrderBy(q => questionIds.IndexOf(q.Id))
                    .ToList();
            }
            else
            {
                // First load - randomize 10 questions and store in session
                selectedQuestions = allQuestions
                    .Where(q => q != null)
                    .OrderBy(q => Guid.NewGuid())
                    .Take(10)
                    .ToList();

                if (selectedQuestions.Any())
                {
                    HttpContext.Session.SetString(
                        $"InterviewQuestions_{sessionId}",
                        JsonConvert.SerializeObject(selectedQuestions.Select(q => q.Id).ToList())
                    );
                }
            }

            ViewData["RandomQuestions"] = selectedQuestions;
            return View(interviewSession);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading interview session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while loading the interview session.");
        }
    }

    /// <summary>
    /// Processes submitted interview answers and calculates score
    /// For real interviews, saves answers to database
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitInterview(int sessionId, Dictionary<int, int> Answers)
    {
        try
        {
            // Load interview session with questions and answers
            var interviewSession = await _context.InterviewSessions
                            .Include(s => s.Position)
                                .ThenInclude(p => p.PositionTopics)
                                .ThenInclude(pt => pt.Topic)
                                .ThenInclude(t => t!.Questions)
                                .ThenInclude(q => q.Answers)
                            .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (interviewSession == null)
            {
                _logger.LogWarning("Interview session not found during submission: {SessionId}", sessionId);
                return NotFound();
            }

            // Get all question IDs that should have been answered
            var allQuestionIds = interviewSession.Position?.PositionTopics?
                        .Where(pt => pt != null && pt.Topic != null && pt.Topic.Questions != null)
                        .SelectMany(pt => pt.Topic!.Questions
                            .Where(q => q != null)
                            .Select(q => q.Id))
                        .ToList() ?? new List<int>();

            // Validate all questions were answered (defensive check)
            if (Answers == null || !allQuestionIds.Any() || !allQuestionIds.All(id => Answers.ContainsKey(id)))
            {
                TempData["Error"] = "Please answer all questions before submitting.";
                return RedirectToAction("Index", new { sessionId });
            }

            int correctAnswers = 0;
            var userId = interviewSession.UserId;

            // Process each submitted answer
            foreach (var (questionId, selectedAnswerId) in Answers)
            {
                var question = interviewSession.Position?.PositionTopics?
                    .SelectMany(pt => pt.Topic?.Questions ?? Enumerable.Empty<Question>())
                    .FirstOrDefault(q => q?.Id == questionId);

                if (question == null) continue;

                var selectedAnswer = question.Answers?.FirstOrDefault(a => a?.Id == selectedAnswerId);

                if (selectedAnswer?.IsCorrect == true)
                {
                    correctAnswers++;
                }

                // Save to database for real interviews only
                if (!interviewSession.IsMock && userId != null)
                {
                    var userAnswer = new UserAnswer
                    {
                        UserId = userId,
                        QuestionId = questionId,
                        AnswerId = selectedAnswerId,
                        AnsweredAt = DateTime.Now,
                        InterviewSession = interviewSession
                    };

                    _context.UserAnswers.Add(userAnswer);
                }
            }

            // Update session completion
            interviewSession.CompletedAt = DateTime.Now;

            // Store results in session for the results page
            HttpContext.Session.SetInt32($"CorrectAnswers_{sessionId}", correctAnswers);
            HttpContext.Session.SetInt32($"TotalQuestions_{sessionId}", allQuestionIds.Count());

            await _context.SaveChangesAsync();
            HttpContext.Session.Remove($"InterviewQuestions_{sessionId}");

            return RedirectToAction("Results", new { sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting interview {SessionId}", sessionId);
            TempData["Error"] = "An error occurred while processing your answers.";
            return RedirectToAction("Index", new { sessionId });
        }
    }

    /// <summary>
    /// Displays interview results from session or database
    /// For mock interviews, clears session data after display
    /// </summary>
    public async Task<IActionResult> Results(int sessionId)
    {
        try
        {
            var interviewSession = await _context.InterviewSessions
                .Include(s => s.Position)
                .Include(s => s.Topic)
                    .ThenInclude(t => t!.Questions)
                        .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (interviewSession == null)
            {
                _logger.LogWarning("Interview session not found for results: {SessionId}", sessionId);
                return NotFound();
            }

            int correctCount = 0;
            int totalQuestions = 0;

            // Try to get results from session first
            var sessionCorrectAnswers = HttpContext.Session.GetInt32($"CorrectAnswers_{sessionId}");
            var sessionTotalQuestions = HttpContext.Session.GetInt32($"TotalQuestions_{sessionId}");

            if (sessionCorrectAnswers.HasValue && sessionTotalQuestions.HasValue)
            {
                correctCount = sessionCorrectAnswers.Value;
                totalQuestions = sessionTotalQuestions.Value;

                // Clear session data for mock interviews after displaying
                if (interviewSession.IsMock)
                {
                    HttpContext.Session.Remove($"CorrectAnswers_{sessionId}");
                    HttpContext.Session.Remove($"TotalQuestions_{sessionId}");
                }
            }
            else if (!interviewSession.IsMock)
            {
                // For real interviews, fall back to database if session is lost
                var userAnswers = await _context.UserAnswers
                    .Where(ua => ua.UserId == interviewSession.UserId &&
                                 ua.InterviewSession != null &&
                                 ua.InterviewSession.Id == sessionId)
                    .Include(ua => ua.Answer)
                    .ToListAsync();

                correctCount = userAnswers.Count(ua => ua.Answer?.IsCorrect == true);
                totalQuestions = userAnswers.Count;
            }

            ViewData["CorrectAnswers"] = correctCount;
            ViewData["TotalQuestions"] = totalQuestions;
            ViewData["ShowCorrectAnswers"] = interviewSession.IsMock;

            return View(interviewSession);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading results for session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while loading your results.");
        }
    }
}