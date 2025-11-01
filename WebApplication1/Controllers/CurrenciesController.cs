using Microsoft.AspNetCore.Mvc;
using System;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CurrenciesController : ControllerBase
    {
        private readonly CouchDbService _couch;
        private readonly ILogger<CurrenciesController> _logger;

        public CurrenciesController(CouchDbService couch, ILogger<CurrenciesController> logger)
        {
            _couch = couch;
            _logger = logger;
        }
    }
}
