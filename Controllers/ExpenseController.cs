﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using Expense.API.Repositories.Expense;
using ExpenseModel = Expense.API.Models.Domain.Expense;
// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class ExpenseController : Controller
    {
        private readonly IExpenseRepository expenseRepository;
        private readonly IMapper mapper;

        public ExpenseController(IExpenseRepository expenseRepository, IMapper mapper)
        {
            this.mapper = mapper;
            this.expenseRepository = expenseRepository;
        }

        // GET: api/values
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                // Fetch expenses based on the given id (assuming id is a filter or related key)
                var result = await expenseRepository.GetExpensesAsync(); // Modify your repository method accordingly
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

        // GET api/values/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid guid)
        {
            return BadRequest();
        }

        // POST api/values
        [HttpPost]
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
        public void Delete(int id)
        {
        }
    }
}
