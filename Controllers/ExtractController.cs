
using Expense.API.Repositories.ExpenseAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class ExtractController : Controller
    {
        private readonly IExpenseAnalysis expenseAnalysis;

        public ExtractController(IExpenseAnalysis expenseAnalysis)
        {
            this.expenseAnalysis = expenseAnalysis;
        } 
        [HttpPost("startTextract")]
        public async Task<IActionResult> StartTextractAsync(Guid expenseGuid)
        {
            try
            {
                return Ok(await expenseAnalysis.StartExpenseExtractAsync(expenseId: expenseGuid));
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}

