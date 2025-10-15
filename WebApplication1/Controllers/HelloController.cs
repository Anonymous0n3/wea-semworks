using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebApplication1.Models;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    public class HelloController : Controller
    {
        private readonly CouchDbService _couch;
        private readonly ILogger<HelloController> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public HelloController(CouchDbService couch, ILogger<HelloController> logger)
        {
            _couch = couch;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            await _couch.EnsureDbExistsAsync();

            // Použití generické metody pro HelloDoc
            var allDocs = await _couch.GetAllDocumentsAsync<HelloDoc>();

            return View("~/Views/Home/Index.cshtml", allDocs);
        }

        [HttpPost]
        public async Task<IActionResult> Save(string text)
        {
            await _couch.EnsureDbExistsAsync();
            var doc = new HelloDoc { text = text };

            var post = await _couch.PostDocumentAsync(doc);
            post.EnsureSuccessStatusCode();

            _logger.LogInformation("Saved NEW hello doc with text: {text}", text);
            return RedirectToAction("Index");
        }
    }
}
