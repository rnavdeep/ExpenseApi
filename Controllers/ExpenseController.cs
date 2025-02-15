using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Expense;
using ExpenseModel = Expense.API.Models.Domain.Expense;
using Newtonsoft.Json;
using Expense.API.Repositories.Documents;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class ExpenseController : Controller
    {
        private readonly IExpenseRepository expenseRepository;
        private readonly IDocumentRepository documentRespository;
        private readonly IMapper mapper;

        public ExpenseController(IExpenseRepository expenseRepository, IMapper mapper, IDocumentRepository documentRepository)
        {
            this.mapper = mapper;
            this.expenseRepository = expenseRepository;
            this.documentRespository = documentRepository;
        }

        // GET: api/values
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] Pagination pagination, [FromQuery] FilterBy? filterBy, [FromQuery] SortFilter? sortFilter)
        {
            try
            {
                if (filterBy?.PropertyName == null || filterBy?.Value == null)
                {
                    filterBy = null;
                }
                if (sortFilter?.PropertyNameSort == null)
                {
                    sortFilter = null;
                }
                var expenses = await expenseRepository.GetExpensesAsync(pagination, filterBy, sortFilter);
                var count = await expenseRepository.GetExpensesCountAsync();
                var expensesDto = mapper.Map<List<ExpenseDto>>(expenses);
                if (expensesDto == null || !expensesDto.Any())
                {
                    return NotFound($"No expenses found for the logged in user");
                }
                var result = new
                {
                    Expenses = expensesDto,
                    TotalRows = count
                };

                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest($"An error occurred: {e.Message}");
            }
        }

        // GET: api/values
        [HttpGet("/sharedExpenses")]
        public async Task<IActionResult> GetSharedExpenses([FromQuery] Pagination pagination, [FromQuery] FilterBy? filterBy, [FromQuery] SortFilter? sortFilter)
        {
            try
            {
                if (filterBy?.PropertyName == null || filterBy?.Value == null)
                {
                    filterBy = null;
                }
                if (sortFilter?.PropertyNameSort == null)
                {
                    sortFilter = null;
                }
                var expenses = await expenseRepository.GetExpensesAsync(pagination, filterBy, sortFilter);
                var count = await expenseRepository.GetExpensesCountAsync();
                var expensesDto = mapper.Map<List<ExpenseDto>>(expenses);
                if (expensesDto == null || !expensesDto.Any())
                {
                    return NotFound($"No expenses found for the logged in user");
                }
                var result = new
                {
                    Expenses = expensesDto,
                    TotalRows = count
                };

                return Ok(result);
            }
            catch (Exception e)
            {
                return BadRequest($"An error occurred: {e.Message}");
            }
        }
        // GET: api/values
        [HttpGet("count")]
        public async Task<IActionResult> GetCount()
        {
            try
            {
                var count = await expenseRepository.GetExpensesCountAsync();
                var resultOk = new
                {
                    TotalRows = count
                };

                return Ok(resultOk);
            }
            catch (Exception e)
            {
                // Log the error if necessary
                return BadRequest($"An error occurred: {e.Message}");
            }
        }
        // GET: api/values
        [HttpGet("dropdown")]
        public async Task<IActionResult> Get()
        {
            try
            {
                var result = await expenseRepository.GetExpensesDropdownAsync();
                if (result == null || !result.Any())
                {
                    return NotFound($"No expenses found for the logged in user");
                }

                return Ok(result);
            }
            catch (Exception e)
            {
                // Log the error if necessary
                return BadRequest($"An error occurred: {e.Message}");
            }
        }
        // GET: api/{id}/getAssignedUsers
        [HttpGet("{id}/getAssignedUsers")]
        public async Task<IActionResult> GetAssignedUsers(string id)
        {
            try
            {
                var result = await expenseRepository.GetAssignUsers(Guid.Parse(id));
                if (result == null || !result.Any())
                {
                    return NotFound($"No expenses found for the logged in user");
                }
                var resultDto = mapper.Map<List<ExpenseUserDto>>(result);
                return Ok(resultDto);
            }
            catch (Exception e)
            {
                // Log the error if necessary
                return BadRequest($"An error occurred: {e.Message}");
            }
        }

        // GET: api/GetDocByExpenseId
        [HttpGet("docs/{id}")]
        public async Task<IActionResult> GetDocsByExpenseId(string id)
        {
            if (!Guid.TryParse(id, out var expenseId))
            {
                return BadRequest("Invalid ID format.");
            }
            var result = await expenseRepository.GetDocByExpenseId(expenseId);
            return Ok(result);
        }

        // POST api/values
        [HttpPost]
        [Route("{id}/uploadDoc")]
        public async Task<IActionResult> Post(string id, IFormFile file)
        {

            if (!Guid.TryParse(id, out var expenseId))
            {
                return BadRequest("Invalid ID format.");
            }
            try
            {
                var result = await documentRespository.UploadDocumentByExpenseId(expenseId, file);
                var resultDto= mapper.Map<UploadedDocumentDto>(result);
                return Ok(resultDto);

            }
            catch(Exception e)
            {
                return BadRequest(e.Message);
            }



        }

        // GET api/values/Guid
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            try
            {
                return Ok(await expenseRepository.GetExpenseByIdAsync(id));
            }
            catch(Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        // POST api/values
        [HttpPost]
        public async Task<IActionResult> Post(string title, string description)
        {

            AddExpenseDto addExpenseDto = new AddExpenseDto();
            addExpenseDto.Amount = 0;


            if (title != null && description != null)
            {
                addExpenseDto.Description = description;
                addExpenseDto.Title = title;
                var expenseCreated = await expenseRepository.CreateExpenseAsync(mapper.Map<ExpenseModel>(addExpenseDto));
                var expenseDto = mapper.Map<ExpenseDto>(expenseCreated);

                if (expenseCreated != null)
                {
                    var expenseUser = new ExpenseUser(expenseCreated.Id, expenseCreated.CreatedById, null);
                    await expenseRepository.CreateExpenseUserAsync(expenseUser);
                    return Ok(expenseDto);

                }
            }
            return BadRequest("Not created expense");
        }

        // POST api/values
        [HttpPost]
        [Route("{id}/addUser")]
        public async Task<IActionResult> PostUserToExpense(string id, string userId)
        {
            try
            {
                var expenseUser = new ExpenseUser(Guid.Parse(id), Guid.Parse(userId), null);
                await expenseRepository.CreateExpenseUserAsync(expenseUser);
                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        // PUT api/values/5
        [HttpPut]
        [Route("{id}")]
        public async Task<IActionResult> Put(string id, [FromBody] UpdateExpenseDto updateExpenseDto)
        {
            if(id != updateExpenseDto.Id)
            {
                return BadRequest("You messed up");
            }
            try
            {
                var result = await expenseRepository.UpdateExpenseAsync(updateExpenseDto);

                var resultDto = mapper.Map<ExpenseDto>(result);
                return Ok(resultDto);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (!Guid.TryParse(id, out var expenseId))
            {
                return BadRequest("Invalid ID format.");
            }
            var result = await expenseRepository.RemoveExpense(expenseId);

            if (result)
            {
                return NoContent();
            }
            else
            {
                return NotFound(); 
            }
        }

        [Authorize]
        [HttpGet("{expenseId}/doc/{docId}")]
        public async Task<IActionResult> GetResults(string expenseId, string docId)
        {
            try
            {
                var result = await expenseRepository.GetDocResult(Guid.Parse(expenseId), Guid.Parse(docId));
                var resultDto = mapper.Map<DocumentResultDto>(result);
                return Ok(resultDto);

            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }

        }
    }
}

