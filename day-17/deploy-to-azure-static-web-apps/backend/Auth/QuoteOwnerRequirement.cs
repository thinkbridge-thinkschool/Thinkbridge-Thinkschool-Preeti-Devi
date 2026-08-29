using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using QuotesApi.Models;

namespace QuotesApi.Auth;

public class QuoteOwnerRequirement : IAuthorizationRequirement
{
}

public class QuoteOwnerHandler : AuthorizationHandler<QuoteOwnerRequirement, Quote>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        QuoteOwnerRequirement requirement,
        Quote resource)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId != null && userId == resource.UserId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
