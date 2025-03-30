using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.Application.Interfaces
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetUserByEmailAsync(string email);
    }
}
