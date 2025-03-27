using Microsoft.AspNetCore.Mvc;
using InterviewPortal.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace InterviewPortal.Controllers
{
    public class InterviewSessionController : Controller
    {
        private readonly InterviewPortalDbContext _context;

        public InterviewSessionController(InterviewPortalDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int sessionId)
        {
            var interviewSession = await _context.InterviewSessions
                .Include(s => s.Position)   
                .Include(s => s.Topic)     
                .ThenInclude(t => t.Questions)  
                    .ThenInclude(q => q.Answers) 
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (interviewSession == null)
            {
                return NotFound(); 
            }

            return View(interviewSession);
        }
    }
}