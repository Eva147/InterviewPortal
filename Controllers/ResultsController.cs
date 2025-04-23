namespace InterviewPortal.Controllers;
[Authorize]
public class ResultsController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<ResultsController> _logger;

    /// <summary>
    /// Constructor for the ResultsController.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userManager"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public ResultsController(InterviewPortalDbContext context, UserManager<User> userManager, ILogger<ResultsController> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Displays the results for a specific position.
    /// </summary>
    /// <param name="positionId"></param>
    /// <returns></returns>
    public async Task<IActionResult> Index(int? positionId = null)
    {
        try
        {
            var activePositions = await GetActivePositionsAsync();
            ViewBag.Positions = activePositions;
            ViewBag.SelectedPositionId = positionId ?? 0;

            if (positionId.HasValue && positionId.Value > 0)
            {
                var selectedPosition = await GetPositionWithTopicsAsync(positionId.Value);
                if (selectedPosition != null)
                {
                    ViewBag.SelectedPositionName = selectedPosition.Name ?? "Unnamed Position";
                    var topics = selectedPosition.PositionTopics?
                        .Where(pt => pt?.Topic != null)
                        .Select(pt => pt.Topic!)
                        .ToList() ?? new List<Topic>();

                    ViewBag.Topics = topics;

                    var candidateResults = await GetCandidateResultsAsync(positionId.Value, topics);
                    ViewBag.CandidateResults = candidateResults;
                }
            }

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading results for position {PositionId}", positionId);
            ModelState.AddModelError("", "An error occurred while loading results. Please try again.");
            ViewBag.Positions = new List<Position>();
            return View();
        }
    }

    /// <summary>
    /// Retrieves all active positions from the database.
    /// </summary>
    /// <returns></returns>
    private async Task<List<Position>> GetActivePositionsAsync()
    {
        try
        {
            return await _context.Positions
                .Where(p => p.IsActive)
                .ToListAsync() ?? new List<Position>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active positions");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific position along with its associated topics.
    /// </summary>
    /// <param name="positionId"></param>
    /// <returns></returns>
    private async Task<Position?> GetPositionWithTopicsAsync(int positionId)
    {
        try
        {
            return await _context.Positions
                .Include(p => p.PositionTopics!)
                    .ThenInclude(pt => pt.Topic!)
                .FirstOrDefaultAsync(p => p.Id == positionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving position with ID {PositionId}", positionId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the IDs of candidates who have completed interviews for a specific position.
    /// </summary>
    /// <param name="positionId"></param>
    /// <returns></returns>
    private async Task<List<string>> GetCandidateIdsForPositionAsync(int positionId)
    {
        try
        {
            return await _context.InterviewSessions
                .Where(s => s.PositionId == positionId && s.CompletedAt != null)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync() ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving candidate IDs for position {PositionId}", positionId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the results for all candidates for a specific position.
    /// </summary>
    /// <param name="positionId"></param>
    /// <param name="topics"></param>
    /// <returns></returns>
    private async Task<List<Dictionary<string, object>>> GetCandidateResultsAsync(int positionId, List<Topic> topics)
    {
        try
        {
            var userIds = await GetCandidateIdsForPositionAsync(positionId);
            var candidateResults = new List<Dictionary<string, object>>();

            foreach (var userId in userIds.Where(id => !string.IsNullOrEmpty(id)))
            {
                try
                {
                    var candidateResult = await BuildCandidateResultAsync(userId!, positionId, topics);
                    if (candidateResult != null && candidateResult.Any())
                    {
                        candidateResults.Add(candidateResult);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error building result for user {UserId}", userId);
                }
            }

            return candidateResults
                .OrderByDescending(r => r.TryGetValue("TotalPercentage", out var p) ? (double)p : 0)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving candidate results for position {PositionId}", positionId);
            return new List<Dictionary<string, object>>();
        }
    }

    /// <summary>
    /// Builds the result for a specific candidate based on their interview sessions and topics.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="positionId"></param>
    /// <param name="topics"></param>
    /// <returns></returns>
    private async Task<Dictionary<string, object>> BuildCandidateResultAsync(string userId, int positionId, List<Topic> topics)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return new Dictionary<string, object>();
            }

            var candidateResult = new Dictionary<string, object>
            {
                ["CandidateId"] = userId,
                ["CandidateName"] = $"{user.FirstName} {user.LastName}".Trim(),
                ["CandidateEmail"] = user.Email ?? string.Empty,
                ["TopicResults"] = new List<Dictionary<string, object>>(),
                ["TotalQuestions"] = 0,
                ["TotalCorrect"] = 0,
                ["TotalPercentage"] = 0.0
            };

            var userSessions = await GetUserSessionsAsync(userId, positionId);
            if (userSessions == null || !userSessions.Any())
            {
                return candidateResult;
            }

            int totalQuestions = 0;
            int totalCorrect = 0;

            foreach (var topic in topics.Where(t => t != null))
            {
                try
                {
                    var topicResult = CalculateTopicResult(userSessions, topic);
                    ((List<Dictionary<string, object>>)candidateResult["TopicResults"]).Add(topicResult);

                    totalQuestions += topicResult.TryGetValue("TotalQuestions", out var tq) ? (int)tq : 0;
                    totalCorrect += topicResult.TryGetValue("QuestionsCorrect", out var tc) ? (int)tc : 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating topic result for topic {TopicId}", topic?.Id);
                }
            }

            double totalPercentage = totalQuestions > 0 ? (double)totalCorrect / totalQuestions * 100 : 0;
            candidateResult["TotalQuestions"] = totalQuestions;
            candidateResult["TotalCorrect"] = totalCorrect;
            candidateResult["TotalPercentage"] = totalPercentage;

            await AddFinalResultDataAsync(candidateResult, userId, positionId);
            return candidateResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building candidate result for user {UserId}", userId);
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Retrieves all completed interview sessions for a specific user and position.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="positionId"></param>
    /// <returns></returns>
    private async Task<List<InterviewSession>> GetUserSessionsAsync(string userId, int positionId)
    {
        try
        {
            return await _context.InterviewSessions
                .Include(s => s.UserAnswers!)
                    .ThenInclude(ua => ua.Question!)
                        .ThenInclude(q => q.Topic)
                .Include(s => s.UserAnswers!)
                    .ThenInclude(ua => ua.Answer)
                .Where(s => s.UserId == userId && s.PositionId == positionId && s.CompletedAt != null)
                .ToListAsync() ?? new List<InterviewSession>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sessions for user {UserId}", userId);
            return new List<InterviewSession>();
        }
    }

    /// <summary>
    /// Calculates the result for a specific topic based on user answers.
    /// </summary>
    /// <param name="userSessions"></param>
    /// <param name="topic"></param>
    /// <returns></returns>
    private Dictionary<string, object> CalculateTopicResult(List<InterviewSession> userSessions, Topic topic)
    {
        try
        {
            var topicUserAnswers = userSessions
                .SelectMany(s => s.UserAnswers ?? Enumerable.Empty<UserAnswer>())
                .Where(ua => ua?.Question?.TopicId == topic.Id)
                .ToList();

            int topicQuestions = topicUserAnswers.Count;
            int topicCorrect = topicUserAnswers.Count(ua => ua?.Answer?.IsCorrect ?? false);
            double percentage = topicQuestions > 0 ? (double)topicCorrect / topicQuestions * 100 : 0;

            return new Dictionary<string, object>
            {
                ["TopicId"] = topic.Id,
                ["TopicName"] = topic.Name ?? "Unnamed Topic",
                ["QuestionsCorrect"] = topicCorrect,
                ["TotalQuestions"] = topicQuestions,
                ["PercentageCorrect"] = percentage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating topic results for topic {TopicId}", topic.Id);
            return new Dictionary<string, object>
            {
                ["TopicId"] = topic.Id,
                ["TopicName"] = topic.Name ?? "Unnamed Topic",
                ["QuestionsCorrect"] = 0,
                ["TotalQuestions"] = 0,
                ["PercentageCorrect"] = 0.0
            };
        }
    }

    /// <summary>
    /// Adds final result data to the candidate result dictionary.
    /// </summary>
    /// <param name="candidateResult"></param>
    /// <param name="userId"></param>
    /// <param name="positionId"></param>
    /// <returns></returns>
    private async Task AddFinalResultDataAsync(Dictionary<string, object> candidateResult, string userId, int positionId)
    {
        try
        {
            var result = await _context.Results
                .FirstOrDefaultAsync(r => r.UserId == userId && r.InterviewSession.PositionId == positionId);

            candidateResult["FinalScore"] = result?.FinalScore ?? 0;
            candidateResult["Feedback"] = result?.Feedback ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding final result data for user {UserId}", userId);
            candidateResult["FinalScore"] = 0;
            candidateResult["Feedback"] = "Error loading feedback";
        }
    }

    /// <summary>
    /// Selects a position for which to view results.
    /// </summary>
    /// <param name="positionId"></param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult SelectPosition(int positionId)
    {
        try
        {
            return RedirectToAction(nameof(Index), new { positionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting position {PositionId}", positionId);
            return RedirectToAction(nameof(Index));
        }
    }
}