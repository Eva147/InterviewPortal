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
                .ToListAsync();

            foreach (var position in positions)
            {
                position.PositionTopics = position.PositionTopics
                    .GroupBy(pt => pt.TopicId)
                    .Select(g => g.First())
                    .ToList();
            }

            return View(positions);
        }
        catch
        {
            ModelState.AddModelError("", "An error occurred while retrieving the positions. Please try again.");
            return View(new List<Position>());
        }
    }

    [HttpPost]
    public async Task<IActionResult> StartInterview(int positionId, int topicId, bool isMock)
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Unauthorized();
        }

        var interviewSession = new InterviewSession
        {
            PositionId = positionId,
            TopicId = topicId,
            IsMock = isMock,
            UserId = userId,
            StartedAt = DateTime.UtcNow
        };

        _context.InterviewSessions.Add(interviewSession);
        await _context.SaveChangesAsync();

        return RedirectToAction("Index", "InterviewSession", new { sessionId = interviewSession.Id });
    }

    [Authorize(Roles = "HR,Admin")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> Create(Position position, List<int> Topics)
    {
        if (!ModelState.IsValid)
        {
            return View(position);
        }

        if (Topics != null && Topics.Any())
        {
            position.PositionTopics = Topics.Select(topicId => new PositionTopic
            {
                PositionId = position.Id,
                TopicId = topicId
            }).ToList();
        }

        _context.Add(position);
        await _context.SaveChangesAsync();
        TempData["Message"] = "Position added successfully!";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var position = await _context.Positions.FindAsync(id);
        if (position == null) return NotFound();

        return View(position);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> Edit(int id, Position position, List<int> Topics)
    {
        if (id != position.Id) return NotFound();

        if (!ModelState.IsValid)
        {
            return View(position);
        }

        var existingPosition = await _context.Positions
            .Include(p => p.PositionTopics)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (existingPosition == null) return NotFound();

        existingPosition.Name = position.Name;

        existingPosition.PositionTopics.Clear();
        if (Topics != null && Topics.Any())
        {
            existingPosition.PositionTopics = Topics.Select(topicId => new PositionTopic
            {
                PositionId = position.Id,
                TopicId = topicId
            }).ToList();
        }

        _context.Update(existingPosition);
        await _context.SaveChangesAsync();
        TempData["Message"] = "Position updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]       
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var position = await _context.Positions.FindAsync(id);
        if (position != null)
        {
            _context.Positions.Remove(position);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}