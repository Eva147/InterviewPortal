namespace InterviewPortal.Controllers;
[Authorize]
public class ResultsController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly UserManager<User> _userManager;

    /// <summary>
    /// Constructor for the ResultsController.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userManager"></param>
    public ResultsController(InterviewPortalDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Retrieves the results for candidates based on the selected position.
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
                    ViewBag.SelectedPositionName = selectedPosition.Name;
                    ViewBag.Topics = selectedPosition.PositionTopics.Select(pt => pt.Topic).ToList();

                    var candidateResults = await GetCandidateResultsAsync(positionId.Value, ViewBag.Topics);
                    ViewBag.CandidateResults = candidateResults;
                }
            }

            return View();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
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
        return await _context.Positions
            .Where(p => p.IsActive)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a specific position with its associated topics.
    /// </summary>
    /// <param name="positionId"></param>
    /// <returns></returns>
    private async Task<Position?> GetPositionWithTopicsAsync(int positionId)
    {
        return await _context.Positions
            .Include(p => p.PositionTopics!)
                .ThenInclude(pt => pt.Topic!)
            .FirstOrDefaultAsync(p => p.Id == positionId);
    }

    /// <summary>
    /// Retrieves the candidate IDs for a specific position that have completed their interviews.
    /// </summary>
    /// <param name="positionId"></param>
    /// <returns></returns>
    private async Task<List<string>> GetCandidateIdsForPositionAsync(int positionId)
    {
        return await _context.InterviewSessions
            .Where(s => s.PositionId == positionId && s.CompletedAt != null)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves the results for all candidates for a specific position.
    /// </summary>
    /// <param name="positionId"></param>
    /// <param name="topics"></param>
    /// <returns></returns>
    private async Task<List<Dictionary<string, object>>> GetCandidateResultsAsync(int positionId, List<Topic> topics)
    {
        var userIds = await GetCandidateIdsForPositionAsync(positionId);
        var candidateResults = new List<Dictionary<string, object>>();

        foreach (var userId in userIds)
        {
            var candidateResult = await BuildCandidateResultAsync(userId, positionId, topics);
            if (candidateResult != null)
            {
                candidateResults.Add(candidateResult);
            }
        }

        // Sort results by total percentage
        return candidateResults
            .OrderByDescending(r => (double)r["TotalPercentage"])
            .ToList();
    }

    /// <summary>
    /// Builds the result object for a candidate.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="positionId"></param>
    /// <param name="topics"></param>
    /// <returns></returns>
    private async Task<Dictionary<string, object>> BuildCandidateResultAsync(string userId, int positionId, List<Topic> topics)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new Dictionary<string, object>(); 
        }

        // result object for candidate
        var candidateResult = new Dictionary<string, object>
        {
            ["CandidateId"] = userId,
            ["CandidateName"] = $"{user.FirstName} {user.LastName}",
            ["CandidateEmail"] = user.Email ?? string.Empty, 
            ["TopicResults"] = new List<Dictionary<string, object>>(),
            ["TotalQuestions"] = 0,
            ["TotalCorrect"] = 0,
            ["TotalPercentage"] = 0.0
        };

        // Get all sessions for candidate and position
        var userSessions = await GetUserSessionsAsync(userId, positionId);

        int totalQuestions = 0;
        int totalCorrect = 0;

        // results for topics
        foreach (var topic in topics)
        {
            var topicResult = CalculateTopicResult(userSessions, topic);
            ((List<Dictionary<string, object>>)candidateResult["TopicResults"]).Add(topicResult);

            totalQuestions += (int)topicResult["TotalQuestions"];
            totalCorrect += (int)topicResult["QuestionsCorrect"];
        }

        //  total result
        double totalPercentage = totalQuestions > 0 ? (double)totalCorrect / totalQuestions * 100 : 0;
        candidateResult["TotalQuestions"] = totalQuestions;
        candidateResult["TotalCorrect"] = totalCorrect;
        candidateResult["TotalPercentage"] = totalPercentage;

        await AddFinalResultDataAsync(candidateResult, userId, positionId);

        return candidateResult;
    }

    /// <summary>
    /// Retrieves all interview sessions for a specific user and position that have been completed.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="positionId"></param>
    /// <returns></returns>
    private async Task<List<InterviewSession>> GetUserSessionsAsync(string userId, int positionId)
    {
        return await _context.InterviewSessions
            .Include(s => s.UserAnswers)
                .ThenInclude(ua => ua.Question)
                    .ThenInclude(q => q.Topic)
            .Include(s => s.UserAnswers)
                .ThenInclude(ua => ua.Answer)
            .Where(s => s.UserId == userId &&
                     s.PositionId == positionId &&
                     s.CompletedAt != null)
            .ToListAsync();
    }

    /// <summary>
    /// Calculates the result for a specific topic based on user answers.
    /// </summary>
    /// <param name="userSessions"></param>
    /// <param name="topic"></param>
    /// <returns></returns>
    private Dictionary<string, object> CalculateTopicResult(List<InterviewSession> userSessions, Topic topic)
    {
        // Get all candidate answers for topic
        var topicUserAnswers = userSessions
            .SelectMany(s => s.UserAnswers)
            .Where(ua => ua.Question.TopicId == topic.Id)
            .ToList();

        int topicQuestions = topicUserAnswers.Count;
        int topicCorrect = topicUserAnswers.Count(ua => ua.Answer.IsCorrect);
        double percentage = topicQuestions > 0 ? (double)topicCorrect / topicQuestions * 100 : 0;

        return new Dictionary<string, object>
        {
            ["TopicId"] = topic.Id,
            ["TopicName"] = topic.Name,
            ["QuestionsCorrect"] = topicCorrect,
            ["TotalQuestions"] = topicQuestions,
            ["PercentageCorrect"] = percentage
        };
    }

    /// <summary>
    /// Adds the final result data to the candidate result object.
    /// </summary>
    /// <param name="candidateResult"></param>
    /// <param name="userId"></param>
    /// <param name="positionId"></param>
    /// <returns></returns>
    private async Task AddFinalResultDataAsync(Dictionary<string, object> candidateResult, string userId, int positionId)
    {
        // final result
        var result = await _context.Results
            .Where(r => r.UserId == userId &&
                   r.InterviewSession.PositionId == positionId)
            .FirstOrDefaultAsync();

        if (result != null)
        {
            candidateResult["FinalScore"] = result.FinalScore; 
            candidateResult["Feedback"] = result.Feedback ?? string.Empty;
        }
        else
        {
            candidateResult["FinalScore"] = 0; 
            candidateResult["Feedback"] = string.Empty;
        }
    }

    /// <summary>
    /// Redirects to the index action with the selected position ID.
    /// </summary>
    /// <param name="positionId"></param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult SelectPosition(int positionId)
    {
        return RedirectToAction(nameof(Index), new { positionId });
    }
}