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
            CreateMap<CartDiffDTO, CartItem>().ReverseMap();
            CreateMap<CartItem, CartItemResponseDTO>();

            // Product -> ProductResponseDTO. The entity stores rating flat (RatingRate/RatingCount)
            // while the DTO nests it under Rating, so project it explicitly.
            CreateMap<Product, ProductResponseDTO>()
                .ForMember(dest => dest.Rating, opt => opt.MapFrom(src => new ProductRatingDTO
                {
                    Rate = src.RatingRate,
                    Count = src.RatingCount
                }));
        }
    }
}
