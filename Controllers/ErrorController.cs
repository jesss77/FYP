using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FYP.Controllers
{
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        [Route("Error")]
        public IActionResult Index()
        {
            var exFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exFeature != null)
            {
                _logger.LogError(exFeature.Error, "Unhandled exception on path {Path}", exFeature.Path);
                ViewData["ErrorMessage"] = exFeature.Error.Message;
                ViewData["ErrorPath"] = exFeature.Path;
            }
            else
            {
                ViewData["ErrorMessage"] = "An unknown error occurred.";
            }

            return View();
        }
    }
}
