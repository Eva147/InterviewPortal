using Microsoft.AspNetCore.Mvc;

namespace InterviewPortal.Components
{
    public class NavbarViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View();
        }
    }
}