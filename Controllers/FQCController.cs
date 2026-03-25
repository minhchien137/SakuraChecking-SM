using Microsoft.AspNetCore.Mvc;

namespace ScanCheckSakura.Controllers
{
    public class FQCController : Controller
    {
        private readonly ILogger<FQCController> _logger;

        public FQCController(ILogger<FQCController> logger)
        {
            _logger = logger;
        }

         [HttpGet]
        public IActionResult FQCBP()
        {
            return View();
        }


    }
}