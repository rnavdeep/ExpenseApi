using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Amazon.S3;
using Microsoft.AspNetCore.Authorization;


// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class S3Controller : ControllerBase
    {
        private readonly IAmazonS3 _s3Client;

        public S3Controller(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
        }
        [Authorize]
        [HttpGet("list-buckets")]
        public async Task<IActionResult> ListBuckets()
        {
            var response = await _s3Client.ListBucketsAsync();
            return Ok(response.Buckets);
        }
    }
}

