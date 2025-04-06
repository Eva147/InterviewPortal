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
        var usersQuery = _context.Users.AsQueryable();

        // Fetch roles
        var userRoles = await _context.UserRoles.ToListAsync();
        var roles = await _context.Roles.ToListAsync();

        if (User.IsInRole("Admin") && roleFilter == "Owner")
        {
            roleFilter = ""; 
        }

        // Filtering by Role
        if (!string.IsNullOrEmpty(roleFilter))
        {
            var usersInRole = userRoles
                .Where(ur => roles.Any(r => r.Id == ur.RoleId && r.Name == roleFilter))
                .Select(ur => ur.UserId)
                .ToList();

            usersQuery = usersQuery.Where(u => usersInRole.Contains(u.Id));
        }

        // Searching by Name
        if (!string.IsNullOrEmpty(search))
        {
            usersQuery = usersQuery.Where(u => (u.FirstName + " " + u.LastName).Contains(search));
        }

        // Sorting
        usersQuery = sortOrder switch
        {
            "name_desc" => usersQuery.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName),
            _ => usersQuery.OrderBy(u => u.FirstName).ThenBy(u => u.LastName), 
        };

        var users = await usersQuery.ToListAsync();

        // Pass data to the View
        ViewData["Users"] = users;
        ViewData["Roles"] = roles.Select(r => r.Name).ToList();
        ViewData["Search"] = search;
        ViewData["RoleFilter"] = roleFilter;
        ViewData["SortOrder"] = sortOrder;

        return View();
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