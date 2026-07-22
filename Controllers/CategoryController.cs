using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Expense.API.CustomActionFilters;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Category;

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class CategoryController : Controller
    {
        private readonly ICategoryRepository categoryRepository;

        public CategoryController(ICategoryRepository categoryRepository)
        {
            this.categoryRepository = categoryRepository;
        }

        // PUT api/Category
        [HttpPut]
        [ValidateModelAtrribute]
        public async Task<IActionResult> Put([FromBody] UpsertCategoryDto upsertCategoryDto)
        {
            try
            {
                var category = await categoryRepository.UpsertAsync(upsertCategoryDto);
                return Ok(category);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        // GET api/Category?period=month
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string period = "month")
        {
            try
            {
                var statuses = await categoryRepository.GetStatusAsync(period);
                if (statuses == null || !statuses.Any())
                {
                    return NotFound("No categories found for the logged in user");
                }
                return Ok(statuses);
            }
            catch (Exception e)
            {
                return BadRequest($"An error occurred: {e.Message}");
            }
        }

        // DELETE api/Category/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await categoryRepository.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }

        // GET api/Category/{name}/expenses
        [HttpGet("{name}/expenses")]
        public async Task<IActionResult> GetCategoryExpenses(string name)
        {
            try
            {
                var expenses = await categoryRepository.GetCategoryExpensesAsync(name);
                return Ok(expenses);
            }
            catch (Exception e)
            {
                return BadRequest($"An error occurred: {e.Message}");
            }
        }
    }
}
