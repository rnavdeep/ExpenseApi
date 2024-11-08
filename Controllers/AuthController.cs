using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Expense.API.Models.Domain;
using Expense.API.Repositories.Users;
using Expense.API.Repositories.AuthToken;
using Expense.API.Models.DTO;
using Newtonsoft.Json;
using Expense.API.Repositories.Redis;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;


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
        private readonly IRedisRepository redisRepository;

        public AuthController(UserManager<IdentityUser> userManager, ITokenRepository tokenRepository, IMapper mapper, IUserRepository userRepository,
            IRedisRepository redisRepository)
        {
            this.userManager = userManager;
            this.tokenRepository = tokenRepository;
            this.mapper = mapper;
            this.userRepository = userRepository;
            this.redisRepository = redisRepository;
        }
        // Check if token is valid
        [HttpGet("checkSession")]
        public async Task<IActionResult> CheckSession()
        {
            var sessionData = new SessionDataDto(
                HttpContext.User.Identity.IsAuthenticated
                    ? HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                    : string.Empty, 
                HttpContext.User.Identity.IsAuthenticated 
            );

            return Ok(sessionData); 
        }
        // POST api/Auth/Register
        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register([FromBody]RegisterRequestDto registerRequestDto)
        {
            var identityUser = new IdentityUser
            {
                UserName = registerRequestDto.UserName,
                Email = registerRequestDto.Email
            };
            var ifEmailExists = await userManager.FindByEmailAsync(registerRequestDto.Email);
            if (ifEmailExists == null)
            {
                var identityResult = await userManager.CreateAsync(identityUser, registerRequestDto.Password);

                if (identityResult.Succeeded)
                {
                    if (registerRequestDto.Roles != null && registerRequestDto.Roles.Any())
                    {
                        //add roles to user
                        identityResult = await userManager.AddToRolesAsync(identityUser, registerRequestDto.Roles);
                        if (identityResult.Succeeded)
                        {
                            // Generate password reset token
                            var token = await userManager.GeneratePasswordResetTokenAsync(identityUser);

                            // Create reset password link
                            var resetLink = Url.Action("ResetPassword", "Account",
                                new { userId = identityUser.Id, token = token }, Request.Scheme);

                            var userModel = mapper.Map<User>(registerRequestDto);
                            userModel.Email = registerRequestDto.Email;
                            var userCreated = await userRepository.CreateAsync(userModel);
                            if (userCreated != null)
                            {
                                return Ok(userCreated);
                            }
                            else
                            {
                                await userManager.DeleteAsync(identityUser);
                                return BadRequest("Unable to Add User to Local Db");
                            }
                        }
                    }
                }
                return BadRequest(identityResult.Errors);

            }
            return BadRequest("User with Email Already Exists");

        }
        [HttpPost("Logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwtToken");
            Response.Cookies.Delete(".AspNetCore.Session");
            return Ok(new { message = "Logged out successfully" });
        }

        //POST /api/Auth/Login
        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody]LoginRequestDto loginRequestDto)
        {
            var response = new LoginResponseDto
            {
                IsLoggedIn = false, Error = string.Empty
            };
            var user = await userManager.FindByNameAsync(loginRequestDto.Username);
            var userInDb = user != null ? await userRepository.GetUserByEmail(user.Email) : null;

            if (user != null && userInDb != null)
            {
                var isPasswordCorrect = await userManager.CheckPasswordAsync(user, loginRequestDto.Password);
                if (isPasswordCorrect == true)
                {

                    var roles = await userManager.GetRolesAsync(user);
                    if (roles != null)
                    {
                        //Create JWT token to use for Endpoint calls
                        var jwtToken = tokenRepository.CreateJwtToken(user, roles.ToArray());

                        // Configure cookie options
                        var cookieOptions = new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = false,
                            SameSite = SameSiteMode.Unspecified, 
                            Expires = DateTime.UtcNow.AddHours(1)
                            ,Path = "/",

                        };


                        // Add JWT token to cookies
                        Response.Cookies.Append("jwtToken", jwtToken, cookieOptions);
                        //Add JWT token to cache
                        await redisRepository.StoreTokenAsync(userName: userInDb.Username, jwtToken);
                        Console.WriteLine();
                        HttpContext.Session.SetString("UserSession", userInDb.Username);  // Save username in session
                        HttpContext.Session.SetString("UserId", userInDb.Id.ToString());  // Save user ID in session
                        response.IsLoggedIn = true;
                        return Ok(response);

                    }
                    response.Error = ("User must have a role assigned to Login");
                    return BadRequest(response);
                }
            }
            response.Error = ("Username or Password Incorrect");
            return BadRequest(response);

        }
    }
}

