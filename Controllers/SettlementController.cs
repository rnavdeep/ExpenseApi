using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Expense.API.CustomActionFilters;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Settlement;

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class SettlementController : Controller
    {
        private readonly ISettlementRepository settlementRepository;

        public SettlementController(ISettlementRepository settlementRepository)
        {
            this.settlementRepository = settlementRepository;
        }

        // POST api/Settlement
        [HttpPost]
        [ValidateModelAtrribute]
        public async Task<IActionResult> Post([FromBody] CreateSettlementDto createSettlementDto)
        {
            try
            {
                var settlement = await settlementRepository.CreateAsync(createSettlementDto);
                return StatusCode(201, settlement);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        // GET api/Settlement?pageNumber=&pageSize=
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var settlements = await settlementRepository.GetForUserAsync(pageNumber, pageSize);
                if (settlements == null || !settlements.Any())
                {
                    return NotFound("No settlements found for the logged in user");
                }
                return Ok(settlements);
            }
            catch (Exception e)
            {
                return BadRequest($"An error occurred: {e.Message}");
            }
        }
    }
}
