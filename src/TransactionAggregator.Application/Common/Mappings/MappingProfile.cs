using AutoMapper;
using TransactionAggregator.Application.Common.Models;
using TransactionAggregator.Domain.Entities;

namespace TransactionAggregator.Application.Common.Mappings;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Transaction → DTO
        CreateMap<Transaction, TransactionDto>()
            .ForMember(d => d.CategoryName, opt => opt.MapFrom(s => s.Category != null ? s.Category.Name : null))
            .ForMember(d => d.Source, opt => opt.MapFrom(s => s.Source.ToString()));

        // Category → DTO
        CreateMap<Category, CategoryDto>();
    }
}
