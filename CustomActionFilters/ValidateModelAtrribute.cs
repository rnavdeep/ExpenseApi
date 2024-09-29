using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Expense.API.CustomActionFilters
{
	public class ValidateModelAtrribute:ActionFilterAttribute
	{
		public ValidateModelAtrribute()
		{
		}
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState.IsValid == false)
            {
                context.Result = new BadRequestResult();
            }
        }
    }
}

