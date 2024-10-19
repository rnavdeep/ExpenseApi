using System;
namespace Expense.API.Models.Domain
{
	public class SortFilter
	{
		public SortFilter(string propName, bool asc = true)
		{
			this.PropertyNameSort = propName;
			this.Ascending = asc;
		}
		public SortFilter()
		{

		}
		public string PropertyNameSort { get; set; }
		public bool Ascending { get; set; }
	}
}

