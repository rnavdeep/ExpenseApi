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
        [Route("myExpenses")]
        public async Task<IActionResult> Get()
        {
            try
            {
                var result = await expenseRepository.GetExpensesAsync();
                if (result == null || !result.Any())
                {
                    return NotFound($"No expenses found for the logged in user");
                }

                // Map the domain model to DTO
                var returnResult = mapper.Map<List<ExpenseDto>>(result);
                return Ok(returnResult);
            }
            catch (Exception e)
            {
                // Log the error if necessary
                return BadRequest($"An error occurred: {e.Message}");
            }
        }

        // GET: api/GetDocByExpenseId
        [HttpGet("getDocs/{id}")]
        public async Task<IActionResult> GetDocsByExpenseId(string id)
        {
            if (!Guid.TryParse(id, out var expenseId))
            {
                return BadRequest("Invalid ID format.");
            }
            var result = await expenseRepository.GetDocByExpenseId(expenseId);
            var resultDto = mapper.Map<List<UploadedDocumentDto>>(result);
            return Ok(resultDto);
        }

        // POST api/values
        [HttpPost]
        [Route("uploadDoc")]
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
        [HttpGet("{guid}")]
        public async Task<IActionResult> Get(Guid guid)
        {
            try
            {
                return Ok(await expenseRepository.GetExpenseByIdAsync(guid));
            }
            catch(Exception e)
            {
                return BadRequest(e.Message);
            }
        }
        // POST api/values
        [HttpPost]
        [Route("createForm")]
        public async Task<IActionResult> Post(string title, string description, IFormCollection files)
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
                    var expenseUser = new ExpenseUser(expenseCreated.Id, expenseCreated.CreatedById);
                    var expenseUserLink = await expenseRepository.CreateExpenseUserAsync(expenseUser);
                    //after expense is create upload all the docs
                    //await documentRespository.UploadFileFormAsync(files, expenseCreated);
                    return Ok(expenseDto);

                }

            }
            return BadRequest("Not created expense");

        }
        // POST api/values
        [HttpPost]
        [Route("create")]
        public async Task<IActionResult> Post([FromBody]AddExpenseDto addExpenseDto)
        {

            try
            {
                var expenseCreated = await expenseRepository.CreateExpenseAsync(mapper.Map<ExpenseModel>(addExpenseDto));
                var expenseDto = mapper.Map<ExpenseDto>(expenseCreated);

                if (expenseCreated != null)
                {
                    var expenseUser = new ExpenseUser(expenseCreated.Id, expenseCreated.CreatedById);
                    var expenseUserLink = await expenseRepository.CreateExpenseUserAsync(expenseUser);
                }
                return Ok(expenseDto);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
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
    }
}

