using System;
namespace Expense.API.Models.Domain
{
	public class Pagination
	{
		public Pagination()
		{
		}
		public int pageNumber { get; set; }
		public int pageSize { get; set; }
	}
}

