using Microsoft.AspNetCore.Mvc;
using InterviewPortal.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Newtonsoft.Json;

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

            if (!interviewSession.IsMock)
            {
                HttpContext.Session.Remove("MockInterviewState");
            }

            return View(interviewSession);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitInterview(int sessionId, Dictionary<int, int> Answers)
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

            int correctAnswers = 0;
            int totalQuestions = interviewSession.Topic.Questions.Count;
            var userId = interviewSession.UserId;

            foreach (var question in interviewSession.Topic.Questions)
            {
                if (!Answers.TryGetValue(question.Id, out int selectedAnswerId))
                    continue;

                var selectedAnswer = question.Answers.FirstOrDefault(a => a.Id == selectedAnswerId);

                if (selectedAnswer != null && selectedAnswer.IsCorrect)
                {
                    correctAnswers++;
                }

                // Save to DB only if it's NOT a mock interview
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
                else
                {
                    // For mock interviews, store answers in the session
                    var mockStateJson = HttpContext.Session.GetString("MockInterviewState");
                    MockInterviewState mockState;

                    if (!string.IsNullOrEmpty(mockStateJson))
                    {
                        mockState = JsonConvert.DeserializeObject<MockInterviewState>(mockStateJson);
                    }
                    else
                    {
                        mockState = new MockInterviewState
                        {
                            SessionId = sessionId,
                            CurrentQuestionIndex = 0,
                            UserAnswers = new List<int>()
                        };
                    }

                    while (mockState.UserAnswers.Count <= question.Id)
                        mockState.UserAnswers.Add(-1); 

                    mockState.UserAnswers[question.Id] = selectedAnswerId;

                    HttpContext.Session.SetString("MockInterviewState", JsonConvert.SerializeObject(mockState));
                }
            }

            interviewSession.CompletedAt = DateTime.Now;

            TempData["CorrectAnswers"] = correctAnswers;
            TempData["TotalQuestions"] = totalQuestions;

            _context.Entry(interviewSession).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            if (interviewSession.IsMock)
            {
                HttpContext.Session.Remove("MockInterviewState");
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

            if (TempData["CorrectAnswers"] != null)
            {
                ViewData["CorrectAnswers"] = TempData["CorrectAnswers"];
                ViewData["TotalQuestions"] = TempData["TotalQuestions"];
            }
            else
            {
                if (!interviewSession.IsMock)
                {
                    var userAnswers = await _context.UserAnswers
                        .Where(ua => ua.UserId == interviewSession.UserId)
                        .Include(ua => ua.Answer)
                        .ToListAsync();

                    int correctCount = userAnswers.Count(ua => ua.Answer.IsCorrect);
                    ViewData["CorrectAnswers"] = correctCount;
                }
                else
                {
                    var mockStateJson = HttpContext.Session.GetString("MockInterviewState");
                    if (!string.IsNullOrEmpty(mockStateJson))
                    {
                        var mockState = JsonConvert.DeserializeObject<MockInterviewState>(mockStateJson);
                        ViewData["CorrectAnswers"] = mockState.UserAnswers.Count;
                    }
                    else
                    {
                        ViewData["CorrectAnswers"] = 0;
                    }
                }

                ViewData["TotalQuestions"] = interviewSession.Topic.Questions.Count;
            }

            ViewData["ShowCorrectAnswers"] = interviewSession.IsMock;

            return View(interviewSession);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveAnswerToSession([FromBody] AnswerSessionUpdate data)
        {
            var mockStateJson = HttpContext.Session.GetString("MockInterviewState");
            MockInterviewState mockState;

            if (!string.IsNullOrEmpty(mockStateJson))
            {
                mockState = JsonConvert.DeserializeObject<MockInterviewState>(mockStateJson);
            }
            else
            {
                mockState = new MockInterviewState
                {
                    SessionId = data.SessionId,
                    CurrentQuestionIndex = 0,
                    UserAnswers = new List<int>()
                };
            }

            while (mockState.UserAnswers.Count <= data.QuestionIndex)
                mockState.UserAnswers.Add(-1);

            mockState.UserAnswers[data.QuestionIndex] = data.AnswerId;

            HttpContext.Session.SetString("MockInterviewState", JsonConvert.SerializeObject(mockState));

            return Ok();
        }
    }

    public class MockInterviewState
    {
        public int SessionId { get; set; }
        public int CurrentQuestionIndex { get; set; }
        public List<int> UserAnswers { get; set; }
    }

    public class AnswerSessionUpdate
    {
        public int SessionId { get; set; }
        public int QuestionIndex { get; set; }
        public int AnswerId { get; set; }
    }
}
