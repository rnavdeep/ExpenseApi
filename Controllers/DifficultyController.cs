using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSWalks.API.CustomActionFilters;
using NSWalks.API.Models.Domain;
using NSWalks.API.Models.DTO;
using NSWalks.API.Repositories;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace NSWalks.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DifficultyController : Controller
    {
        private readonly IDifficultyRepository difficultyRepository;
        private readonly IMapper mapper;
        public DifficultyController(IDifficultyRepository difficultyRepository,IMapper mapper)
        {
            this.difficultyRepository = difficultyRepository;
            this.mapper = mapper;
        }
        // GET: api/values
        [HttpGet]
        [Authorize(Roles = "Reader,Writer")]
        public async Task<IActionResult> GetAllDifficulties([FromQuery] string? filterOn, [FromQuery] string? filterQuery, [FromQuery] string? sortBy,
            [FromQuery] bool? isAscending, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 100)
        {
            //get difficulty domain models from repository which interacts with the database
            var difficulties = await difficultyRepository.GetAllAsync(filterOn,filterQuery,sortBy,isAscending,pageNumber,pageSize);


            //convert difficult domain model to DTO using mapper
            var difficultiesDto = mapper.Map<List<DifficultyDto>>(difficulties);

            //return DTO
            return Ok(difficultiesDto);
        }

        // GET api/values/5
        [HttpGet("{code}")]
        [Authorize(Roles = "Reader,Writer")]
        public async Task<IActionResult> GetDifficultyByCode(string code)
        {
            //get the domain model from db using repository
            var difficulty = await difficultyRepository.GetByCodeAsync(code);

            //check if not null
            if (difficulty != null)
            {
                //convert to dto
                var difficultyDto = mapper.Map<DifficultyDto>(difficulty);

                //return dto
                return Ok(difficultyDto);
            }
            return NotFound();
        }

        // POST api/values
        [HttpPost]
        [ValidateModelAtrribute]
        [Authorize(Roles = "Writer")]
        public async Task<IActionResult> AddDifficulty([FromBody]DifficultyDto difficultyDto)
        {

            //convert dto to domain for database
            var difficultyDomainModel = mapper.Map<Difficulty>(difficultyDto);

            try
            {
                //check if the code is unique or not before adding and saving changes
                var difficulty = await difficultyRepository.GetByCodeAsync(difficultyDto.Code);

                if (difficulty != null)
                {
                    //throw exception
                    throw new NotImplementedException("Code is not unique");
                }
                else
                {
                    difficultyDomainModel = await difficultyRepository.CreateAsync(difficultyDomainModel);

                }
            }
            catch (Exception e)
            {
                var result = new BadRequestObjectResult(new { message = e.Message, currentDate = DateTime.Now });
                return result;
            }
            //map from model to dto to send back
            var diffcultyDto = mapper.Map<DifficultyDto>(difficultyDomainModel);

            //send the id created.
            return CreatedAtAction(nameof(GetDifficultyByCode), new { code = diffcultyDto.Code }, diffcultyDto);
        }

        // PUT api/values/5
        [HttpPut("{code}")]
        [ValidateModelAtrribute]
        [Authorize(Roles = "Writer")]
        public async Task<IActionResult> UpdateDiffcultyByCode(string code, [FromBody]UpdateDifficultDto updateDifficultDto)
        {
                //convert update dto to domain model for repository
            var updateDifficultyDomainModel = mapper.Map<Difficulty>(difficultyRepository);

            //call the update in repository
            updateDifficultyDomainModel = await difficultyRepository.UpdateAsync(code, updateDifficultyDomainModel);

            //if not null
            if (updateDifficultyDomainModel != null)
            {
                //convert to dto
                var updateDifficultyDto = mapper.Map<UpdateDifficultDto>(updateDifficultyDomainModel);

                //return result
                return Ok(updateDifficultyDto);
            }

            //result not found
            return NotFound();

        }

        // DELETE api/values/5
        [HttpDelete("{code}")]
        [Authorize(Roles = "Writer")]
        public async Task<IActionResult> DeleteDiffculity(string code)
        {
            //see the deleted domain model
            var deleltedDifficulty = await difficultyRepository.DeleteAsync(code);

            // if deleted
            if (deleltedDifficulty != null)
            {
                //convert to dto
                var difficultyDto = mapper.Map<DifficultyDto>(deleltedDifficulty);

                //return result
                return Ok(difficultyDto);
            }
            //if not deleted means not found
            return NotFound();
        }
    }
}

