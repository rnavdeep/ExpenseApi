using System;
using Microsoft.AspNetCore.Identity;

namespace NSWalks.API.Repositories.AuthToken
{
	public interface ITokenRepository
	{
		string CreateJwtToken(IdentityUser user, string[] roles);
	}
}

