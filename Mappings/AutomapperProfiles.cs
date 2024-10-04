﻿using System;
using AutoMapper;
using Expense.API.Models.Domain;
using Expense.API.Models.DTO;
using ExpenseModel = Expense.API.Models.Domain.Expense;
namespace Expense.API.Mappings
{
	public class AutomapperProfiles : Profile
	{
		public AutomapperProfiles()
		{
            CreateMap<User, RegisterRequestDto>().ReverseMap();
            CreateMap<User, UserDto>().ReverseMap();
            CreateMap<Document, DocumentDto>().ReverseMap();
            CreateMap<ExpenseModel, AddExpenseDto>().ReverseMap();
            CreateMap<ExpenseModel, ExpenseDto>().ReverseMap();
            // Mapping from Expense to ExpenseDto
            CreateMap<ExpenseModel, ExpenseDto>()
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt.ToShortDateString()));
                //.ForMember(dest => dest.DocumentUrls, opt => opt.MapFrom(src => src.Documents.Where(doc=>doc.ExpenseId.Equals(src.Id)).Select(d => d.S3Url).ToList()));
            CreateMap<ExpenseDto, ExpenseModel>();
        }
    }
}

