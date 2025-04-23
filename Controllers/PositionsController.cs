namespace InterviewPortal.Controllers;
[Authorize]
public class PositionController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly UserManager<User> _userManager;

    /// <summary>
    /// Constructor for the PositionController.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userManager"></param>
    public PositionController(InterviewPortalDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Retrieves a list of positions and their associated topics and questions.
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> Index()
    {
        try
        {
            var positions = await _context.Positions
                .Include(p => p.PositionTopics)
                    .ThenInclude(pt => pt.Topic)
                        .ThenInclude(t => t!.Questions)
                            .ThenInclude(q => q.Answers)
                .ToListAsync();
            ViewBag.AllTopics = await _context.Topics
                .Include(t => t.Questions)
                    .ThenInclude(q => q.Answers)
                .ToListAsync();
            return View(positions);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"An error occurred while retrieving positions: {ex.Message}");
            ViewBag.AllTopics = new List<Topic>();
            return View(new List<Position>());
        }
    }

    /// <summary>
    /// Creates a new position.
    /// </summary>
    /// <param name="Name"></param>
    /// <param name="Topics"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePosition(string Name, List<int> Topics)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            TempData["Error"] = "Position name is required.";
            return RedirectToAction("Index");
        }
        var newPosition = new Position
        {
            Name = Name,
            IsActive = true,
            PositionTopics = new List<PositionTopic>()
        };
        if (Topics != null)
        {
            foreach (var topicId in Topics)
            {
                newPosition.PositionTopics.Add(new PositionTopic { TopicId = topicId });
            }
        }
        _context.Positions.Add(newPosition);
        await _context.SaveChangesAsync();
        TempData["Message"] = "Position created successfully!";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Updates an existing position.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="Name"></param>
    /// <param name="Topics"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditPosition(int id, string Name, List<int> Topics)
    {
        var position = await _context.Positions
            .Include(p => p.PositionTopics)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (position == null)
        {
            TempData["Error"] = "Position not found.";
            return RedirectToAction("Index");
        }
        position.Name = Name;

        // Clear existing topics and add updated ones
        _context.PositionTopics.RemoveRange(position.PositionTopics);
        position.PositionTopics.Clear();
        if (Topics != null)
        {
            foreach (var topicId in Topics)
            {
                position.PositionTopics.Add(new PositionTopic { PositionId = id, TopicId = topicId });
            }
        }
        await _context.SaveChangesAsync();
        TempData["Message"] = "Position updated successfully!";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Toggles the status of a position (active/inactive).
    /// </summary>
    /// <param name="id"></param>
    /// <param name="isActive"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TogglePositionStatus(int id, bool isActive)
    {
        var position = await _context.Positions.FindAsync(id);
        if (position == null)
        {
            TempData["Error"] = "Position not found.";
            return NotFound();
        }
        position.IsActive = isActive;
        await _context.SaveChangesAsync();
        string statusMessage = isActive ? "activated" : "inactivated";
        TempData["Message"] = $"Position '{position.Name}' has been {statusMessage} successfully.";
        return RedirectToAction("Index");
    }
}