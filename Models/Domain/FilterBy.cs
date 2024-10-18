using System;
namespace Expense.API.Models.Domain
{
	public class FilterBy
	{
		public FilterBy()
		{
		}
		public FilterBy(string propName, string value, string type)
		{
			this.PropertyName = propName;
			this.Value = value;
			this.Type = type;
		}

        public string PropertyName { get; set; }
        public string Value { get; set; }
		public string Type { get; set; }
    }
}

