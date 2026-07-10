using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Expense.API.CustomActionFilters;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Budget;

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class BudgetController : Controller
    {
        private readonly IBudgetRepository budgetRepository;

        public BudgetController(IBudgetRepository budgetRepository)
        {
            this.budgetRepository = budgetRepository;
        }

        // PUT api/Budget
        [HttpPut]
        [ValidateModelAtrribute]
        public async Task<IActionResult> Put([FromBody] UpsertBudgetDto upsertBudgetDto)
        {
            try
            {
                var budget = await budgetRepository.UpsertAsync(upsertBudgetDto);
                return Ok(budget);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        // GET api/Budget?period=month
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string period = "month")
        {
            try
            {
                var statuses = await budgetRepository.GetStatusAsync(period);
                if (statuses == null || !statuses.Any())
                {
                    return NotFound("No budgets found for the logged in user");
                }
                return Ok(statuses);
            }
            catch (Exception e)
            {
                return BadRequest($"An error occurred: {e.Message}");
            }
        }
    }
}
