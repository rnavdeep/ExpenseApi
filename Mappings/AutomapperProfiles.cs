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
            CreateMap<Document, DocumentDto>().ReverseMap();

        }
    }
}

