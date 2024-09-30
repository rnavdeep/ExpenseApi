using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Expense.API.Models.Domain;
using Expense.API.Repositories.Users;
using Expense.API.Repositories.AuthToken;
using Expense.API.Models.DTO;
using Newtonsoft.Json;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Expense.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly ITokenRepository tokenRepository;
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;

        public AuthController(UserManager<IdentityUser> userManager, ITokenRepository tokenRepository, IMapper mapper, IUserRepository userRepository)
        {
            this.userManager = userManager;
            this.tokenRepository = tokenRepository;
            this.mapper = mapper;
            this.userRepository = userRepository;
        }
        // POST api/Auth/Register
        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register()
        {
            // Retrieve the decrypted data from HttpContext
            if (HttpContext.Items.TryGetValue("DecryptedData", out var decryptedData))
            {
                if(decryptedData!= null)
                {
                    var registerRequestDto = JsonConvert.DeserializeObject<RegisterRequestDto>((string)decryptedData);

                    var identityUser = new IdentityUser
                    {
                        UserName = registerRequestDto.Username,
                        Email = registerRequestDto.Username
                    };

                    var identityResult = await userManager.CreateAsync(identityUser, registerRequestDto.Password);

                    if (identityResult.Succeeded)
                    {
                        if (registerRequestDto.Roles != null && registerRequestDto.Roles.Any())
                        {
                            //add roles to user
                            identityResult = await userManager.AddToRolesAsync(identityUser, registerRequestDto.Roles);
                            if (identityResult.Succeeded)
                            {
                                var userModel = mapper.Map<User>(registerRequestDto);
                                userModel.Email = registerRequestDto.Username;
                                var userCreated = await userRepository.CreateAsync(userModel);
                                if (userCreated != null)
                                {
                                    return Ok(userCreated);
                                }
                            }
                        }
                    }
                    return BadRequest(identityResult.Errors);
                }
            }
            return BadRequest("No data to register user");
        }

        //POST /api/Auth/Login
        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login()
        {
            // Retrieve the decrypted data from HttpContext
            if (HttpContext.Items.TryGetValue("DecryptedData", out var decryptedData))
            {
                if (decryptedData != null)
                {
                    var loginRequestDto = JsonConvert.DeserializeObject<LoginRequestDto>((string)decryptedData);

                    var user = await userManager.FindByEmailAsync(loginRequestDto.Username);
                    if (user != null)
                    {
                        var isPasswordCorrect = await userManager.CheckPasswordAsync(user, loginRequestDto.Password);
                        if (isPasswordCorrect == true)
                        {

                            var roles = await userManager.GetRolesAsync(user);
                            if (roles != null)
                            {
                                //Create JWT token to use for Endpoint calls
                                var jwtToken = tokenRepository.CreateJwtToken(user, roles.ToArray());
                                var response = new LoginResponseDto
                                {
                                    JwtToken = jwtToken
                                };
                                // Configure cookie options
                                var cookieOptions = new CookieOptions
                                {
                                    HttpOnly = false,   // Prevent client-side access
                                    Secure = true,     // Send cookie only over HTTPS
                                    SameSite = SameSiteMode.Strict, // Restrict to same-site requests
                                    Expires = DateTime.UtcNow.AddHours(1) // Set cookie expiration time
                                };

                                // Add JWT token to cookies
                                Response.Cookies.Append("jwtToken", jwtToken, cookieOptions);

                                return Ok(response);

                            }

                            return Ok("Login Success");
                        }
                    }
                    return BadRequest("Username or Password Incorrect");
                }
            }
            return BadRequest("Provide Data");
        }


        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }
    }
}

