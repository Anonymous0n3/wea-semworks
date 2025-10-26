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

        public CurrenciesController(CouchDbService couch)
        {
            _couch = couch;
        }
    }
}
