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

        [HttpPost]
        public async Task<IActionResult> SubmitInterview(int sessionId, List<int> Answers)
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

            // Calculate score
            int totalQuestions = interviewSession.Topic.Questions.Count;
            int correctAnswers = 0;
            var userId = interviewSession.UserId;

            for (int i = 0; i < Math.Min(Answers.Count, totalQuestions); i++)
            {
                var question = interviewSession.Topic.Questions.ElementAt(i);
                var selectedAnswerId = Answers[i];
                var selectedAnswer = question.Answers.FirstOrDefault(a => a.Id == selectedAnswerId);

                if (selectedAnswer != null && selectedAnswer.IsCorrect)
                {
                    correctAnswers++;
                }

                // For real interviews, store the user's answers
                if (!interviewSession.IsMock)
                {
                    var userAnswer = new UserAnswer
                    {
                        UserId = userId,
                        QuestionId = question.Id,
                        AnswerId = selectedAnswerId,
                        AnsweredAt = DateTime.Now,
                        InterviewSession = interviewSession
                    };

                    _context.UserAnswers.Add(userAnswer);
                    interviewSession.UserAnswers.Add(userAnswer);
                }
            }

            // Update interview session with completion time only
            interviewSession.CompletedAt = DateTime.Now;

            // Store correct answers count in TempData to pass to the view
            TempData["CorrectAnswers"] = correctAnswers;
            TempData["TotalQuestions"] = totalQuestions;

            // Save changes to database - for real interviews, this saves the UserAnswers too
            if (!interviewSession.IsMock)
            {
                // For real interviews, we store all results in the database
                _context.Entry(interviewSession).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }
            else
            {
                // For mock interviews, we only update the session temporarily
                // But don't persist the individual answers
                _context.Entry(interviewSession).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                // For mock interviews, we could later add cleanup code here
                // to delete the session after some time (e.g., 24 hours)
            }

            return RedirectToAction("Results", new { sessionId });
        }

        public async Task<IActionResult> Results(int sessionId)
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

            // Pass the correct answers count from TempData to ViewData
            if (TempData["CorrectAnswers"] != null)
            {
                ViewData["CorrectAnswers"] = TempData["CorrectAnswers"];
                ViewData["TotalQuestions"] = TempData["TotalQuestions"];
            }
            else
            {
                // If TempData is not available (e.g., if user refreshes the page),
                // we need to count the correct answers from saved data
                if (!interviewSession.IsMock)
                {
                    // For real interviews, count from stored UserAnswers
                    var userAnswers = await _context.UserAnswers
                        .Where(ua => ua.UserId == interviewSession.UserId)
                        .Include(ua => ua.Answer)
                        .ToListAsync();

                    int correctCount = userAnswers.Count(ua => ua.Answer.IsCorrect);
                    ViewData["CorrectAnswers"] = correctCount;
                }
                else
                {
                    // For mock interviews, we don't have stored answers, so use 0
                    ViewData["CorrectAnswers"] = 0;
                }

                ViewData["TotalQuestions"] = interviewSession.Topic.Questions.Count;
            }

            // For mock interviews, we show correct answers for learning purposes
            ViewData["ShowCorrectAnswers"] = interviewSession.IsMock;

            return View(interviewSession);
        }
    }
}