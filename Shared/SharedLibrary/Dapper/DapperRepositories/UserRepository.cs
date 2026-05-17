using SharedLibrary.Dapper.DapperRepositories.Abstractions;
using SharedLibrary.UserModels;

namespace SharedLibrary.Dapper.DapperRepositories
{
    public class UserRepository : IUserRepository
    {
        private readonly string Connection;

        public UserRepository(string conn)
        {
            Connection = conn;
        }

        public async Task<UserModel?> GetUserAsync(int id)
        {
            var query = "SELECT * FROM \"Users\" WHERE \"Id\" = @Id";
            var result = await DapperOperations.QueryAsync<UserModel>(query, new { Id = id }, Connection);
            return result.FirstOrDefault();
        }

        public async Task<UserModel?> GetUserByEmailAsync(string email)
        {
            var query = "SELECT * FROM \"Users\" WHERE \"Email\" = @Email";
            var result = await DapperOperations.QueryAsync<UserModel>(query, new { Email = email }, Connection);
            return result.FirstOrDefault();
        }

        public async Task<IEnumerable<UserModel>> GetUsersByIdsAsync(IEnumerable<int> ids)
        {
            if (ids == null || !ids.Any())
            {
                return Enumerable.Empty<UserModel>();
            }

            var query = "SELECT * FROM \"Users\" WHERE \"Id\" = ANY(@Ids)";

            var result = await DapperOperations
                .QueryAsync<UserModel>(query, new { Ids = ids.ToArray() }, Connection);

            return result;
        }
    }
}