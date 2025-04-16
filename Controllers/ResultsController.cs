namespace InterviewPortal.Controllers;

[Authorize]
public class ResultsController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly UserManager<User> _userManager;

    public ResultsController(InterviewPortalDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int? positionId = null)
    {
        try
        {
            // Get all active positions
            var positions = await _context.Positions
                .Where(p => p.IsActive)
                .ToListAsync();

            // Pass positions to ViewBag instead of using a model
            ViewBag.Positions = positions;
            ViewBag.SelectedPositionId = positionId ?? 0;

            if (positionId.HasValue && positionId.Value > 0)
            {
                // Get the selected position with topics
                var selectedPosition = await _context.Positions
                    .Include(p => p.PositionTopics)
                        .ThenInclude(pt => pt.Topic)
                    .FirstOrDefaultAsync(p => p.Id == positionId.Value);

                if (selectedPosition != null)
                {
                    ViewBag.SelectedPositionName = selectedPosition.Name;

                    // Get topics for this position
                    var topics = selectedPosition.PositionTopics
                        .Select(pt => pt.Topic)
                        .ToList();
                    ViewBag.Topics = topics;

                    // Find all users who have interview sessions for this position
                    var userIds = await _context.InterviewSessions
                        .Where(s => s.PositionId == positionId.Value && s.CompletedAt != null)
                        .Select(s => s.UserId)
                        .Distinct()
                        .ToListAsync();

                    var candidateResults = new List<Dictionary<string, object>>();

                    foreach (var userId in userIds)
                    {
                        // Get user information
                        var user = await _userManager.FindByIdAsync(userId);
                        if (user == null) continue;

                        // Create a result object for this candidate
                        var candidateResult = new Dictionary<string, object>
                        {
                            ["CandidateId"] = userId,
                            ["CandidateName"] = $"{user.FirstName} {user.LastName}",
                            ["CandidateEmail"] = user.Email,
                            ["TopicResults"] = new List<Dictionary<string, object>>(),
                            ["TotalQuestions"] = 0,
                            ["TotalCorrect"] = 0,
                            ["TotalPercentage"] = 0.0
                        };

                        // Find all sessions for this user and position
                        var userSessions = await _context.InterviewSessions
                            .Include(s => s.UserAnswers)
                                .ThenInclude(ua => ua.Question)
                                    .ThenInclude(q => q.Topic)
                            .Include(s => s.UserAnswers)
                                .ThenInclude(ua => ua.Answer)
                            .Where(s => s.UserId == userId &&
                                     s.PositionId == positionId.Value &&
                                     s.CompletedAt != null)
                            .ToListAsync();

                        int totalQuestions = 0;
                        int totalCorrect = 0;

                        // Calculate results for each topic
                        foreach (var topic in topics)
                        {
                            // Get all user answers for this topic across all sessions
                            var topicUserAnswers = userSessions
                                .SelectMany(s => s.UserAnswers)
                                .Where(ua => ua.Question.TopicId == topic.Id)
                                .ToList();

                            int topicQuestions = topicUserAnswers.Count;
                            int topicCorrect = topicUserAnswers.Count(ua => ua.Answer.IsCorrect);
                            double percentage = topicQuestions > 0 ? (double)topicCorrect / topicQuestions * 100 : 0;

                            var topicResult = new Dictionary<string, object>
                            {
                                ["TopicId"] = topic.Id,
                                ["TopicName"] = topic.Name,
                                ["QuestionsCorrect"] = topicCorrect,
                                ["TotalQuestions"] = topicQuestions,
                                ["PercentageCorrect"] = percentage
                            };

                            ((List<Dictionary<string, object>>)candidateResult["TopicResults"]).Add(topicResult);

                            totalQuestions += topicQuestions;
                            totalCorrect += topicCorrect;
                        }

                        // Calculate total percentage
                        double totalPercentage = totalQuestions > 0 ? (double)totalCorrect / totalQuestions * 100 : 0;
                        candidateResult["TotalQuestions"] = totalQuestions;
                        candidateResult["TotalCorrect"] = totalCorrect;
                        candidateResult["TotalPercentage"] = totalPercentage;

                        // Get the final result if it exists
                        var result = await _context.Results
                            .Where(r => r.UserId == userId &&
                                   r.InterviewSession.PositionId == positionId.Value)
                            .FirstOrDefaultAsync();

                        if (result != null)
                        {
                            candidateResult["FinalScore"] = result.FinalScore;
                            candidateResult["Feedback"] = result.Feedback;
                        }
                        else
                        {
                            candidateResult["FinalScore"] = null;
                            candidateResult["Feedback"] = null;
                        }

                        candidateResults.Add(candidateResult);
                    }

                    // Sort results by total percentage (highest first)
                    ViewBag.CandidateResults = candidateResults
                        .OrderByDescending(r => (double)r["TotalPercentage"])
                        .ToList();
                }
            }

            return View();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "An error occurred while retrieving results: " + ex.Message);
            ViewBag.Positions = new List<Position>();
            return View();
        }
    }

    [HttpPost]
    public IActionResult SelectPosition(int positionId)
    {
        return RedirectToAction(nameof(Index), new { positionId });
    }
}