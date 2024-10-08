﻿using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Expense.API.Repositories.AuthToken
{
	public class TokenRepository:ITokenRepository
	{
        private readonly IConfiguration configuration;
        public TokenRepository(IConfiguration configuration)
		{
            this.configuration = configuration;
		}

        public string CreateJwtToken(IdentityUser user, string[] roles)
        {
            //Create claims from roles
            var claims = new List<Claim>();
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, user.UserName));
            foreach(var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            //create token
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                configuration["Jwt:Issuer"], configuration["Jwt:Audience"],claims,expires:DateTime.Now.AddMinutes(15),signingCredentials:credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

