using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Expense.API.Models.Domain;

namespace Expense.API.Repositories.QueryBuilder
{
    public class QueryBuilder<T> where T : class
    {
        private IQueryable<T> query;

        public QueryBuilder(DbContext dbContext)
        {
            query = dbContext.Set<T>();
        }

        public IQueryable<T> BuildQuery(
            Pagination? pagination,
            FilterBy? filter,
            SortFilter? sort)
        {
            // Apply filter 
            if (filter != null)
            {
                //linq to determine the type of <T>
                var parameter = Expression.Parameter(typeof(T), "x");
                //linq to determine the type of Property in Model <T>
                var property = Expression.Property(parameter, filter.PropertyName);
                var value = Expression.Constant(filter.Value);

                //If the property of type decimal for now, generate query with Property == Value
                if(property.Type == typeof(decimal))
                {
                    filter.Type = "==";
                    if (decimal.TryParse(filter.Value, out decimal parsedValue))
                    {
                        value = Expression.Constant(parsedValue);
                    }
                }
                // Create the expression based on filter type
                Expression comparisonExpression = null;

                switch (filter.Type)
                {
                    case "==":
                        comparisonExpression = Expression.Equal(property, value);
                        break;
                    case "<>":
                        comparisonExpression = Expression.NotEqual(property, value);
                        break;
                    case ">=":
                        comparisonExpression = Expression.GreaterThanOrEqual(property, value);
                        break;
                    case "<=":
                        comparisonExpression = Expression.LessThanOrEqual(property, value);
                        break;
                    case "like":
                        var startsWith = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
                        comparisonExpression = Expression.Call(property, startsWith, value);
                        break;
                    default:
                        throw new NotSupportedException($"Filter type '{filter.Type}' is not supported.");
                }

                var lambda = Expression.Lambda<Func<T, bool>>(comparisonExpression, parameter);
                query = query.Where(lambda);
            }


            // Apply sorting 
            if (sort != null)
            {
                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, sort.PropertyNameSort);
                var lambda = Expression.Lambda(property, parameter);

                // Specify the types for OrderBy and OrderByDescending
                var methodName = sort.Ascending ? "OrderBy" : "OrderByDescending";
                var resultExpression = Expression.Call(
                    typeof(Queryable),
                    methodName,
                    new Type[] { typeof(T), property.Type },
                    query.Expression,
                    lambda
                );

                // Create the new query with the ordered result
                query = query.Provider.CreateQuery<T>(resultExpression);
            }


            // Apply pagination
            if (pagination != null)
            {
                query = query
                    .Skip((pagination.pageNumber - 1) * pagination.pageSize)
                    .Take(pagination.pageSize);
            }

            return query;
        }
    }
}
