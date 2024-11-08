using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Users;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    public class FriendsController : Controller
    {
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;
        public FriendsController(IUserRepository userRepository, IMapper mapper)
        {
            this.userRepository = userRepository;
            this.mapper = mapper;
        }

        // GET: api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/{searchString}
        [HttpGet("{searchString}")]
        public async Task<IActionResult> Get(string searchString)
        {
            if (searchString != null)
            {
                var resultEmail = await userRepository.GetUserByEmail(searchString);
                if (resultEmail != null)
                {
                    return Ok(mapper.Map<UserDto>(resultEmail));
                }
                var resultUsername = await userRepository.GetUserByUserName(searchString);
                if (resultUsername != null)
                {
                    return Ok(mapper.Map<UserDto>(resultUsername));
                }
                return NotFound("User not found");
            }
            else
            {
                return BadRequest("Search query can not be null");
            }


        }
        // POST api/values
        [HttpPost]
        [Route("sendRequest")]
        public async Task<IActionResult> SendRequest([FromBody] UserDto userDto)
        {
            await userRepository.SendRequest(userDto.Id.ToString(), userDto.Username);
            return NoContent();
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}

