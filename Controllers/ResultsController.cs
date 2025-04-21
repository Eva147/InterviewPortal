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

    private async Task<List<Position>> GetActivePositionsAsync()
    {
        return await _context.Positions
            .Where(p => p.IsActive)
            .ToListAsync();
    }

    private async Task<Position> GetPositionWithTopicsAsync(int positionId)
    {
        return await _context.Positions
        .Include(p => p.PositionTopics)
            .ThenInclude(pt => pt.Topic)
        .FirstOrDefaultAsync(p => p.Id == positionId);
    }

    private async Task<List<string>> GetCandidateIdsForPositionAsync(int positionId)
    {
        return await _context.InterviewSessions
            .Where(s => s.PositionId == positionId && s.CompletedAt != null)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync();
    }

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

    private async Task<Dictionary<string, object>> BuildCandidateResultAsync(string userId, int positionId, List<Topic> topics)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return null;

        // result object for candidate
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
            candidateResult["Feedback"] = result.Feedback;
        }
        else
        {
            candidateResult["FinalScore"] = null;
            candidateResult["Feedback"] = null;
        }
    }

    [HttpPost]
    public IActionResult SelectPosition(int positionId)
    {
        return RedirectToAction(nameof(Index), new { positionId });
    }
}