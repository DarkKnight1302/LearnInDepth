using LearnInDepth.Models;

namespace LearnInDepth.Repositories
{
    public interface IUserRepository
    {
        Task<User> GetByIdAsync(string email);
        Task UpsertAsync(User user);
    }
}
