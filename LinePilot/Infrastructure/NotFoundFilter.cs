using LinePilot.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LinePilot.Infrastructure;

/// <summary>Turns a NotFoundException thrown anywhere in a controller into the friendly 404 page.</summary>
public class NotFoundFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not NotFoundException ex) return;
        context.Result = new ViewResult
        {
            ViewName = "NotFound",
            StatusCode = StatusCodes.Status404NotFound,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
            {
                ["Message"] = ex.Message,
            },
        };
        context.ExceptionHandled = true;
    }
}
