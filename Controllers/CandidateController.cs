namespace InterviewPortal.Controllers;
[Authorize]
public class CandidateController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<CandidateController> _logger;

    /// <summary>
    /// Constructor for the CandidateController.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userManager"></param>
    /// <param name="logger"></param>
    public CandidateController(InterviewPortalDbContext context, UserManager<User> userManager, ILogger<CandidateController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a list of positions available for candidates to apply for.
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> Index()
    {
        try
        {
            var positions = await _context.Positions.ToListAsync();
            if (positions == null || !positions.Any())
            {
                _logger.LogWarning("No positions found in database");
                TempData["Warning"] = "No positions available at this time";
            }
            return View(positions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve positions");
            TempData["Error"] = "Error loading positions";
            return View(new List<Position>());
        }
    }

    /// <summary>
    /// Displays the application form for a specific position.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public IActionResult ApplyPosition(int id)
    {
        try
        {
            return View(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading application for position {id}");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Initiates an interview session for a specific position.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    [Route("Candidate/ApplyPosition/{id}/{type}")]
    public IActionResult ApplyPositionWithType(int id, string type)
    {
        try
        {
            ViewData["PositionId"] = id;
            ViewData["InterviewType"] = type;
            return View("InterviewStart");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading interview start for position {id}, type {type}");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Starts an interview session for a specific position and type (mock or real).
    /// </summary>
    /// <param name="id"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    [Route("Candidate/StartInterview/{id}/{type}")]
    public async Task<IActionResult> StartInterview(int id, string type)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthenticated user attempted interview start");
                return Unauthorized("Please log in to start an interview");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning($"User {userId} not found");
                return Unauthorized("User account not found");
            }

            bool isMock = type?.ToLower() == "mock";

            var positionTopic = await _context.PositionTopics
                .Where(pt => pt.PositionId == id)
                .Include(pt => pt.Topic)
                .FirstOrDefaultAsync();

            if (positionTopic?.Topic == null)
            {
                _logger.LogWarning($"No topics found for position {id}");
                return NotFound("This position has no interview topics configured");
            }

            var session = new InterviewSession
            {
                UserId = userId,
                PositionId = id,
                TopicId = positionTopic.TopicId,
                IsMock = isMock,
                StartedAt = DateTime.Now
            };

            await _context.InterviewSessions.AddAsync(session);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "InterviewSession", new { sessionId = session.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error starting interview for position {id}");
            TempData["Error"] = "Failed to start interview session";
            return RedirectToAction(nameof(Index));
        }
    }
}