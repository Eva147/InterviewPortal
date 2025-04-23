namespace InterviewPortal.Controllers;
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<User> _signInManager;
    private readonly InterviewPortalDbContext _context;

    /// <summary>
    /// Constructor for the AdminController.
    /// </summary>
    /// <param name="userManager"></param>
    /// <param name="roleManager"></param>
    /// <param name="signInManager"></param>
    /// <param name="context"></param>
    public AdminController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, SignInManager<User> signInManager, InterviewPortalDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _context = context; 
    }

    /// <summary>
    /// Retrieves a list of users with optional search and role filtering.
    /// </summary>
    /// <param name="search"></param>
    /// <param name="roleFilter"></param>
    /// <param name="sortOrder"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Searches for users based on search term and role filter.
    /// </summary>
    /// <param name="search"></param>
    /// <param name="roleFilter"></param>
    /// <param name="sortOrder"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Applies filters to the user query based on search term and role.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="search"></param>
    /// <param name="roleFilter"></param>
    /// <param name="sortOrder"></param>
    /// <returns></returns>
    private IQueryable<User> ApplyFilters(IQueryable<User> query, string search, string roleFilter, string sortOrder)
    {
        if (!string.IsNullOrEmpty(roleFilter))
        {
            query = _context.UserRoles
                .Where(ur => _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == roleFilter))
                .Select(ur => ur.UserId)
                .Distinct()
                .Join(_context.Users, userId => userId, user => user.Id, (userId, user) => user)
                .AsQueryable();
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(u =>
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                (u.FirstName + " " + u.LastName).Contains(search));
        }

        return sortOrder switch
        {
            "name_desc" => query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName),
            _ => query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
        };
    }

    /// <summary>
    /// Creates a new user in the system.
    /// </summary>
    /// <param name="firstName"></param>
    /// <param name="lastName"></param>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> CreateUser(string firstName, string lastName, string email, string password)
    {
        try
        {
            // Create new user object with provided details
            var user = new User { FirstName = firstName, LastName = lastName, Email = email, UserName = email };

            // Attempt to create user - this validates password strength automatically via Identity
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                // Explicitly assign Candidate role by default instead of HR
                await _userManager.AddToRoleAsync(user, "Candidate");
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "User created successfully.";
                return RedirectToAction("Index");
            }
            else
            {
                // Process validation errors from Identity (includes password strength errors)
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                // Consolidate errors for display in TempData
                TempData["ErrorMessage"] = "Failed to create user: " + string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction("Index");
            }
        }
        catch (Exception ex)
        {
            // General exception handling for unexpected errors
            TempData["ErrorMessage"] = "An error occurred while creating the user: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// Deletes a user from the system.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        try
        {
            // Find the user by ID
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Index");
            }

            // Remove user from all roles before deletion to prevent constraints
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, userRoles);
            }

            // Attempt to delete user
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "User deleted successfully.";
                return RedirectToAction("Index");
            }
            else
            {
                // Identity-specific errors during deletion
                TempData["ErrorMessage"] = "Failed to delete the user: " + string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction("Index");
            }
        }
        catch (DbUpdateException)
        {
            // Handle specific database constraints violations
            // This often happens when there are foreign key relationships
            TempData["ErrorMessage"] = "Cannot delete user because they have related records in the system. Consider deactivating instead of deleting.";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            // General exception handling
            TempData["ErrorMessage"] = "An error occurred while deleting the user: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// Updates user information.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="firstName"></param>
    /// <param name="lastName"></param>
    /// <param name="email"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> UpdateUser(string userId, string firstName, string lastName, string email)
    {
        try
        {
            // Get target user and current user for permission checks
            var user = await _userManager.FindByIdAsync(userId);
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "Current user not found.";
                return RedirectToAction("Index");
            }

            if (user == null)
            {
                TempData["ErrorMessage"] = "User to update not found.";
                return RedirectToAction("Index");
            }

            // Security check: Prevent non-admin users from modifying admin accounts
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
            var userRoles = await _userManager.GetRolesAsync(user);

            // Only allow admin to modify non-admin users (or their own account)
            if (currentUserRoles.Contains("Admin"))
            {
                if (userRoles.Contains("Admin") && currentUser.Id != user.Id)
                {
                    // Admin can't modify other admin users - important security constraint
                    TempData["ErrorMessage"] = "You cannot modify another admin user.";
                    return RedirectToAction("Index");
                }
            }

            // Check for email uniqueness to prevent conflicts
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null && existingUser.Id != userId)
            {
                TempData["ErrorMessage"] = "Email is already in use by another user.";
                return RedirectToAction("Index");
            }

            // Update user information
            user.FirstName = firstName;
            user.LastName = lastName;

            // Only update email-related fields if email actually changed
            if (user.Email != email)
            {
                user.Email = email;
                user.UserName = email; // In this system username = email
                user.NormalizedEmail = email.ToUpper();
                user.NormalizedUserName = email.ToUpper();
            }

            // Attempt to save changes
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "User updated successfully.";
                return RedirectToAction("Index");
            }
            else
            {
                // Process Identity-specific errors
                TempData["ErrorMessage"] = "Failed to update user: " + string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction("Index");
            }
        }
        catch (Exception ex)
        {
            // General exception handling
            TempData["ErrorMessage"] = "An error occurred while updating the user: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="role"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> AssignRole(string userId, string role)
    {
        try
        {
            // Security checks
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "Current user not found.";
                return RedirectToAction("Index");
            }

            // Ensure only admins can assign roles
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
            if (!currentUserRoles.Contains("Admin"))
            {
                TempData["ErrorMessage"] = "You don't have permission to assign roles.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Index");
            }

            // Validate role is one of the permitted values
            if (role != "HR" && role != "Candidate" && role != "Admin")
            {
                TempData["ErrorMessage"] = "Invalid role specified.";
                return RedirectToAction("Index");
            }

            // Special protection for admin accounts - prevents privilege escalation attacks
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Contains("Admin") && role != "Admin" && user.Id != currentUser.Id)
            {
                TempData["ErrorMessage"] = "Cannot change role of another administrator.";
                return RedirectToAction("Index");
            }

            // Remove all existing roles before assigning new role
            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (!removeResult.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to remove current roles: " +
                    string.Join(", ", removeResult.Errors.Select(e => e.Description));
                return RedirectToAction("Index");
            }

            // Assign the specified role
            var addResult = await _userManager.AddToRoleAsync(user, role);

            if (addResult.Succeeded)
            {
                TempData["SuccessMessage"] = $"Role '{role}' assigned successfully.";
                return RedirectToAction("Index");
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to assign role: " +
                    string.Join(", ", addResult.Errors.Select(e => e.Description));
                return RedirectToAction("Index");
            }
        }
        catch (Exception ex)
        {
            // General exception handling
            TempData["ErrorMessage"] = "An error occurred while assigning role: " + ex.Message;
            return RedirectToAction("Index");
        }
    }
}