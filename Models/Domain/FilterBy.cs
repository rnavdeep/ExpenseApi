using System;
using System.Collections;
using Newtonsoft.Json.Linq; // Install Newtonsoft.Json if not already installed

namespace Expense.API.Models.Domain
{
    public class FilterBy
    {
        public FilterBy() { }

        public FilterBy(string propName, object value, string type)
        {
            this.PropertyName = propName;
            this.Value = value;
            this.Type = type;
        }

        public string PropertyName { get; set; }
        public object Value { get; set; } 
        public string Type { get; set; }

        // Helper method to check if Value is an array
        public bool IsArray() => Value is IEnumerable && !(Value is string);

        // Helper method to get Value as an array safely
        public JArray GetArrayValue()
        {
            if (Value is JArray jArray)
            {
                return jArray;
            }
            else if (Value is string str)
            {
                try
                {
                    return JArray.Parse(str);
                }
                catch
                {
                    throw new ArgumentException("Invalid array format in Value.");
                }
            }
            throw new ArgumentException("Value is not an array.");
        }
    }
}
