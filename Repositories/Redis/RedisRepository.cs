
namespace Expense.API.Repositories.Redis
{
	public class RedisRepository:IRedisRepository
	{
        private readonly StackExchange.Redis.IConnectionMultiplexer redis;

        public RedisRepository(StackExchange.Redis.IConnectionMultiplexer redis)
		{
            this.redis = redis;
		}

        public async Task StoreTokenAsync(string userName, string token)
        {
            var db = redis.GetDatabase();
            await db.StringSetAsync(userName, token, TimeSpan.FromHours(5));
        }

        public async Task<string> GetTokenAsync(string userId)
        {
            var db = redis.GetDatabase();
            return await db.StringGetAsync(userId);
        }

        public async Task DeleteTokenAsync(string userId)
        {
            var db = redis.GetDatabase();
            await db.KeyDeleteAsync(userId.ToString());
        }
    }
}

