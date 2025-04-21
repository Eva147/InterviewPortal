namespace InterviewPortal.Controllers;
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<User> _signInManager;
    private readonly InterviewPortalDbContext _context; 

    public AdminController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, SignInManager<User> signInManager, InterviewPortalDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _context = context; 
    }

    // GET: Users 
    [HttpGet]
    public async Task<IActionResult> Index(string search, string roleFilter, string sortOrder)
    {
        // Get all roles for dropdown
        var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        ViewData["Roles"] = roles;
        ViewData["SortOrder"] = sortOrder;
        ViewData["Search"] = search;
        ViewData["RoleFilter"] = roleFilter;

        var usersQuery = _context.Users.AsQueryable();
        usersQuery = ApplyFilters(usersQuery, search, roleFilter, sortOrder);
        ViewData["Users"] = await usersQuery.Take(50).ToListAsync();

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> SearchUsers(string search, string roleFilter, string sortOrder)
    {
        var usersQuery = _context.Users.AsQueryable();
        usersQuery = ApplyFilters(usersQuery, search, roleFilter, sortOrder);
        var users = await usersQuery.ToListAsync();

        var userList = new List<object>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            userList.Add(new
            {
                id = user.Id,
                firstName = user.FirstName,
                lastName = user.LastName,
                email = user.Email,
                roles = roles.ToList()
            });
        }

        return Json(userList);
    }

    private IQueryable<User> ApplyFilters(IQueryable<User> query, string search, string roleFilter, string sortOrder)
    {
        // Filter by role
        if (!string.IsNullOrEmpty(roleFilter))
        {
            var usersInRole = _userManager.GetUsersInRoleAsync(roleFilter).Result;
            var userIds = usersInRole.Select(u => u.Id);
            query = query.Where(u => userIds.Contains(u.Id));
        }

        // Search by name
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(u =>
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                (u.FirstName + " " + u.LastName).Contains(search));
        }

        // Sorting
        return sortOrder switch
        {
            "name_desc" => query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName),
            _ => query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
        };
    }

    // CREATE
    [HttpPost]
    public async Task<IActionResult> CreateUser(string firstName, string lastName, string email, string password)
    {
        var user = new User { FirstName = firstName, LastName = lastName, Email = email, UserName = email };
        var result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            await _context.SaveChangesAsync(); 
            return RedirectToAction("Index");
        }

        return BadRequest("Failed to create user");
    }

    // UPDATE 
    [HttpPost]
    public async Task<IActionResult> UpdateUser(string userId, string firstName, string lastName, string email)
    {
        var user = await _userManager.FindByIdAsync(userId);
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return NotFound();
        }

        if (user == null)
        {
            return NotFound(); 
        }

        var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
        var userRoles = await _userManager.GetRolesAsync(user);

        // Check if current user is an admin
        if (currentUserRoles.Contains("Admin"))
        {
            if (userRoles.Contains("Admin") || userRoles.Contains("Owner"))
            {
                return Forbid(); 
            }
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = email;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            return RedirectToAction("Index"); 
        }

        return View("Index");
    }

    // DELETE
    [HttpPost]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        // Find the user by ID
        var user = await _userManager.FindByIdAsync(userId);

        if (user != null)
        {
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                return RedirectToAction("Index");
            }

            ModelState.AddModelError(string.Empty, "Failed to delete the user.");
            return RedirectToAction("Index"); 
        }

        ModelState.AddModelError(string.Empty, "User not found.");
        return RedirectToAction("Index"); 
    }

    // UPDATE ROLES
    [HttpPost]
    public async Task<IActionResult> AssignRole(string userId, string role)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return NotFound();
        }
        var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
        if (!currentUserRoles.Contains("Admin"))
        {
            return Forbid(); 
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null && (role == "HR" || role == "Candidate"))
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            var result = await _userManager.AddToRoleAsync(user, role);

            if (!result.Succeeded)
            {
                return BadRequest("Failed to assign role.");
            }
        }
        return RedirectToAction("Index");
    }
}