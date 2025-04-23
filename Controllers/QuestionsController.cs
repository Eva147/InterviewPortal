namespace InterviewPortal.Controllers;

[Authorize]
public class QuestionController : Controller
{
    private readonly InterviewPortalDbContext _context;
    private readonly ILogger<QuestionController> _logger;

    /// <summary>
    /// Constructor for the QuestionController.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="logger"></param>
    public QuestionController(InterviewPortalDbContext context, ILogger<QuestionController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Deletes a question from the database.
    /// </summary>
    /// <param name="questionId"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> DeleteQuestion(int questionId)
    {
        try
        {
            var question = await _context.Questions
                .Include(q => q.Answers)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
            {
                _logger.LogWarning("DeleteQuestion: Question with ID {QuestionId} not found", questionId);
                return NotFound();
            }

            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Question deleted successfully!";
            return RedirectToAction("Index", "Position");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting question with ID {QuestionId}", questionId);
            TempData["ErrorMessage"] = "An error occurred while deleting the question.";
            return RedirectToAction("Index", "Position");
        }
    }

    /// <summary>
    /// Deletes an answer from a question.
    /// </summary>
    /// <param name="answerId"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> DeleteAnswer(int answerId)
    {
        try
        {
            var answer = await _context.Answers.FindAsync(answerId);
            if (answer == null)
            {
                _logger.LogWarning("DeleteAnswer: Answer with ID {AnswerId} not found", answerId);
                return NotFound();
            }

            _context.Answers.Remove(answer);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Answer deleted successfully!";
            return RedirectToAction("Index", "Position");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting answer with ID {AnswerId}", answerId);
            TempData["ErrorMessage"] = "An error occurred while deleting the answer.";
            return RedirectToAction("Index", "Position");
        }
    }

    /// <summary>
    /// Adds a new question to a topic.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="questionText"></param>
    /// <param name="difficultyLevel"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> UpdateQuestion(int id, string questionText, QuestionDifficultyLevel difficultyLevel)
    {
        try
        {
            var question = await _context.Questions.FindAsync(id);
            if (question == null)
            {
                _logger.LogWarning("UpdateQuestion: Question with ID {QuestionId} not found", id);
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(questionText))
            {
                TempData["ErrorMessage"] = "Question text cannot be empty.";
                return RedirectToAction("Index", "Position");
            }

            question.QuestionText = questionText;
            question.Difficulty = difficultyLevel;
            await _context.SaveChangesAsync();

            TempData["Message"] = "Question updated successfully!";
            return RedirectToAction("Index", "Position");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating question with ID {QuestionId}", id);
            TempData["ErrorMessage"] = "An error occurred while updating the question.";
            return RedirectToAction("Index", "Position");
        }
    }

    /// <summary>
    /// Adds a new answer to a question.
    /// </summary>
    /// <param name="questionId"></param>
    /// <param name="answerText"></param>
    /// <param name="isCorrect"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> AddAnswer(int questionId, string answerText, bool isCorrect)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(answerText))
            {
                TempData["ErrorMessage"] = "Answer text cannot be empty.";
                return RedirectToAction("Index", "Position");
            }

            var question = await _context.Questions
                .Include(q => q.Answers)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
            {
                _logger.LogWarning("AddAnswer: Question with ID {QuestionId} not found", questionId);
                return NotFound();
            }

            if (isCorrect)
            {
                foreach (var existingAnswer in question.Answers.Where(a => a.IsCorrect))
                {
                    existingAnswer.IsCorrect = false;
                }
            }

            question.Answers.Add(new Answer
            {
                AnswerText = answerText,
                IsCorrect = isCorrect
            });

            await _context.SaveChangesAsync();

            TempData["Message"] = "Answer added successfully!";
            return RedirectToAction("Index", "Position");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding answer to question with ID {QuestionId}", questionId);
            TempData["ErrorMessage"] = "An error occurred while adding the answer.";
            return RedirectToAction("Index", "Position");
        }
    }

    /// <summary>
    /// Updates an existing answer for a question.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="answerText"></param>
    /// <param name="isCorrect"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = "HR,Admin")]
    public async Task<IActionResult> UpdateAnswer(int id, string answerText, bool isCorrect)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(answerText))
            {
                TempData["ErrorMessage"] = "Answer text cannot be empty.";
                return RedirectToAction("Index", "Position");
            }

            var answer = await _context.Answers
                .Include(a => a.Question)
                .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (answer == null)
            {
                _logger.LogWarning("UpdateAnswer: Answer with ID {AnswerId} not found", id);
                return NotFound();
            }

            // If this answer is being marked as correct, unmark any other correct answers
            if (isCorrect && !answer.IsCorrect)
            {
                foreach (var otherAnswer in answer.Question.Answers.Where(a => a.IsCorrect))
                {
                    otherAnswer.IsCorrect = false;
                }
            }

            answer.AnswerText = answerText;
            answer.IsCorrect = isCorrect;

            await _context.SaveChangesAsync();

            TempData["Message"] = "Answer updated successfully!";
            return RedirectToAction("Index", "Position");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating answer with ID {AnswerId}", id);
            TempData["ErrorMessage"] = "An error occurred while updating the answer.";
            return RedirectToAction("Index", "Position");
        }
    }
}