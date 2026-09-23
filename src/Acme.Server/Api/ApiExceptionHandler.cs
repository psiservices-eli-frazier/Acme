using Acme.Server.Services;
using Microsoft.AspNetCore.Diagnostics;

namespace Acme.Server.Api;

/// <summary>
/// Maps the domain's refusals onto HTTP.
///
/// Acme split error handling three ways on purpose, and the split is preserved here:
///
///   * "this does not exist" -> 404, which the client renders as its Not found page;
///   * a business-rule refusal -> back on the screen the user was already on, either
///     as a field error (400) or a banner (409) -- never a separate error page;
///   * "you may not" -> 403 with its own client screen, kept distinct from 404 so a
///     refusal never implies the record is missing.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var result = Map(exception);

        if (result is null)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

            // Deliberately opaque: the message may name internals. The order-number
            // exhaustion case lands here, exactly as it produced a 500 in Java.
            result = Results.Problem(
                detail: "Something went wrong.", statusCode: StatusCodes.Status500InternalServerError);
        }

        await result.ExecuteAsync(httpContext);
        return true;
    }

    private static IResult? Map(Exception exception) => exception switch
    {
        NotFoundException e => Results.Problem(
            detail: e.Message, statusCode: StatusCodes.Status404NotFound, title: "Not found"),

        // Keyed to the offending field so the form can render it next to the input --
        // the BindingResult.rejectValue equivalent.
        DuplicateValueException e => Results.ValidationProblem(
            new Dictionary<string, string[]> { [e.Field] = [e.Message] }),

        EntityInUseException e => Results.Problem(
            detail: e.Message, statusCode: StatusCodes.Status409Conflict, title: "Cannot delete"),

        InvalidSortException e => Results.ValidationProblem(
            new Dictionary<string, string[]> { ["sort"] = [e.Message] }),

        ForbiddenException e => Results.Problem(
            detail: e.Message, statusCode: StatusCodes.Status403Forbidden, title: "Not permitted"),

        _ => null,
    };
}
