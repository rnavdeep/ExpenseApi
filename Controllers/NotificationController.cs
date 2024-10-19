using AutoMapper;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Documents;
using Expense.API.Repositories.ExpenseAnalysis;
using Expense.API.Repositories.Notifications;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    public class NotificationController : Controller
    {
        private readonly ITextractNotification textractNotification;
        private readonly IMapper mapper;

        public NotificationController(ITextractNotification textractNotification, IMapper mapper)
        {
            this.textractNotification = textractNotification;
            this.mapper = mapper;
        }
        // GET: api/values
        [HttpGet]
        public async Task<IActionResult> Get()
        {

            var result = await textractNotification.GetNotifications();
            var resultDto = mapper.Map<List<NotificationDto>>(result);
            return Ok(resultDto);

        }

        //// GET api/values/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

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

        [HttpPost("readAll")]
        public async Task<IActionResult> StartTextractExpDocJobIdAsync()
        {
            try
            {
                await textractNotification.ReadAllNotifications();
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}

