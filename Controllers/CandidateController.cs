using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InterviewPortal.Controllers;
[Authorize(Roles = "Candidate")]
public class CandidateController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly UserManager<User> _userManager;
    public CandidateController(InterviewPortalDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult ApplyPosition(int id)
    {
        return View();
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
            return Unauthorized("User not authenticated or user ID not available.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized("User not found in the database.");
            }
            userId = user.Id;
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

        // Create the interview session
        var session = new InterviewSession
        {
            UserId = userId,
            PositionId = id,
            TopicId = positionTopic.TopicId,
            IsMock = isMock,
            StartedAt = DateTime.Now
        };

        if (!isMock || (isMock))
        {
            _context.InterviewSessions.Add(session);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Index", "InterviewSession", new { sessionId = session.Id });
    }
}
