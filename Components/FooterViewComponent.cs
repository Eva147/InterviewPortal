using Microsoft.AspNetCore.Mvc;

namespace InterviewPortal.Components
{
    public class FooterViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View();
        }
    }
}