using System;
using AutoMapper;
using NSWalks.API.Models.Domain;
using NSWalks.API.Models.DTO;

namespace NSWalks.API.Mappings
{
	public class AutomapperProfiles : Profile
	{
		public AutomapperProfiles()
		{
			CreateMap<Region, RegionDto>().ReverseMap();
            CreateMap<Region, AddRegionRequestDto>().ReverseMap();
            CreateMap<Region, UpdateRegionRequestDto>().ReverseMap();
			CreateMap<Difficulty, DifficultyDto>().ReverseMap();
            CreateMap<Difficulty, UpdateDifficultDto>().ReverseMap();
			CreateMap<Walks, AddWalkRequestDto>().ReverseMap();
            CreateMap<Walks, WalkDto>().ReverseMap();
            CreateMap<Walks, UpdateWalkRequestDto>().ReverseMap();
            CreateMap<WalkImage, WalkImageDto>().ReverseMap();
            CreateMap<RegionImage, RegionImageDto>().ReverseMap();
            CreateMap<User, RegisterRequestDto>().ReverseMap();
            CreateMap<User, UserDto>().ReverseMap();
            CreateMap<Document, DocumentDto>().ReverseMap();
            CreateMap<Expense, AddExpenseDto>().ReverseMap();
            CreateMap<Expense, ExpenseDto>().ReverseMap();
            // Mapping from Expense to ExpenseDto
            CreateMap<Expense, ExpenseDto>()
                .ForMember(dest => dest.CreatedByName, opt => opt.MapFrom(src => src.CreatedBy.Username))
                .ForMember(dest => dest.DocumentUrls, opt => opt.MapFrom(src => src.Documents.Select(d => d.S3Url).ToList()));
            CreateMap<ExpenseDto, Expense>();
        }
    }
}

