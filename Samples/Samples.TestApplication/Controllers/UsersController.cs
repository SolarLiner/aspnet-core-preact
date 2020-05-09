using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Samples.TestApplication.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UsersController: ControllerBase
    {
        private List<string> _users;

        public UsersController() : this(new[] {"Jason", "SolarLiner", "Hagemeister"})
        {
        }

        internal UsersController(IEnumerable<string> users)
        {
            _users = users.ToList();
        }

        [HttpGet]
        public IEnumerable<string> Get()
        {
            return _users;
        }

        [HttpPost, ProducesResponseType(StatusCodes.Status201Created),
         ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> Post([FromBody] string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return BadRequest();
            _users.Add(username);
            return Created("/users", username);
        }

    }
}