using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Expense.API.Models.DTO;
using Expense.API.Repositories.FriendRequest;
using Expense.API.Repositories.Users;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    public class FriendsController : Controller
    {
        private readonly IFriendRequestRepository friendRequestRepository;
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;
        public FriendsController(IFriendRequestRepository friendRequestRepository, IMapper mapper, IUserRepository userRepository)
        {
            this.friendRequestRepository = friendRequestRepository;
            this.mapper = mapper;
            this.userRepository = userRepository;
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

        [HttpGet]
        [Route("getFriends")]
        public async Task<IActionResult> GetFriends()
        {
            return Ok(await friendRequestRepository.GetFriends());
        }

        [HttpGet]
        [Route("getDropdownUsers")]
        public async Task<IActionResult> GetDropdownUsers()
        {
            return Ok(await friendRequestRepository.GetDropdownUsers());
        }
        // POST api/values
        [HttpPost]
        [Route("sendRequest")]
        public async Task<IActionResult> SendRequest([FromBody] UserDto userDto)
        {
            try
            {
                await friendRequestRepository.SendRequest(userDto.Id.ToString(), userDto.Username);
                return NoContent();
            }catch(Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        // POST api/values
        [HttpPost]
        [Route("acceptRequest")]
        public async Task<IActionResult> AcceptRequest([FromBody] string id)
        {
            try
            {
                await friendRequestRepository.AcceptRequest(id);
                return NoContent();
            }catch(Exception e)
            {
                return BadRequest(e.Message);
            }
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

