
using AutoMapper;
using Expense.API.Models.DTO;
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
        private readonly IMapper mapper;
        public ExtractController(IExpenseAnalysis expenseAnalysis, IMapper mapper)
        {
            this.expenseAnalysis = expenseAnalysis;
            this.mapper = mapper;
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
        [HttpPost("startTextractExpDoc")]
        public async Task<IActionResult> StartTextractExpDocAsync(Guid expenseId, Guid docId)
        {
            try
            {
                var result = await expenseAnalysis.StartExpenseExtractByDocIdAsync(expenseId, docId);
                var resultDto = mapper.Map<DocumentResultDto>(result);
                return Ok(resultDto);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
        [HttpPost("startTextractExpDocJobId")]
        public async Task<IActionResult> StartTextractExpDocJobIdAsync(Guid expenseId, Guid docId)
        {
            try
            {
                var result = await expenseAnalysis.StartExpenseExtractByDocIdJobIdAsync(expenseId, docId);
                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}

