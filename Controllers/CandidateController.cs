namespace InterviewPortal.Controllers;
[Authorize(Roles = "Candidate")]
public class CandidateController : Controller
{
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
}
