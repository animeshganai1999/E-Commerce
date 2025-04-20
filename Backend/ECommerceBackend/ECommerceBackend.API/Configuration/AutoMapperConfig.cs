using AutoMapper;
using ECommerceBackend.Application.DTOs;
using ECommerceBackend.Domain.Entities;

namespace ECommerceBackend.API.Configuration
{
    public class AutoMapperConfig : Profile
    {
        public AutoMapperConfig()
        {
            CreateMap<User, UserDTO > ().ReverseMap(); // Mapping User to UserDTO and vice versa
            CreateMap<CartItem, CartItemDTO>().ReverseMap(); // Mapping CartItem to CartItemDTO and vice versa
        }
    }
}
