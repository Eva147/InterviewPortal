namespace InterviewPortal.Controllers;

[Authorize]
public class CandidateController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly UserManager<User> _userManager;

    public CandidateController(InterviewPortalDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var positions = await _context.Positions.ToListAsync();

        if (positions == null || !positions.Any())
        {
            Console.WriteLine("No positions were found in the database.");
        }

        return View(positions);
    }

    public IActionResult ApplyPosition(int id)
    {
        return View(id);
    }

    [Microsoft.AspNetCore.Mvc.Route("Candidate/ApplyPosition/{id}/{type}")]
    public IActionResult ApplyPositionWithType(int id, string type)
    {
        ViewData["PositionId"] = id;
        ViewData["InterviewType"] = type;

        return View("InterviewStart");
    }

    [Microsoft.AspNetCore.Mvc.Route("Candidate/StartInterview/{id}/{type}")]
    public async Task<IActionResult> StartInterview(int id, string type)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User not authenticated.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized("User not found.");
        }

        bool isMock = type.ToLower() == "mock";

        var positionTopic = await _context.PositionTopics
            .Where(pt => pt.PositionId == id)
            .Include(pt => pt.Topic)
            .FirstOrDefaultAsync();

        if (positionTopic == null)
        {
            return NotFound("No topics available for this position.");
        }

        var session = new InterviewSession
        {
            UserId = userId,
            PositionId = id,
            TopicId = positionTopic.TopicId,
            IsMock = isMock,
            StartedAt = DateTime.Now
        };

        _context.InterviewSessions.Add(session);
        await _context.SaveChangesAsync();

        return RedirectToAction("Index", "InterviewSession", new { sessionId = session.Id });
    }
}
