using Microsoft.AspNetCore.Authorization;

namespace PESystem.Policies
{
    public class AreaAccessHandler : AuthorizationHandler<AreaAccessRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AreaAccessRequirement requirement)
        {
            // Lấy danh sách các areas mà người dùng được phép truy cập từ claim
            var allowedAreasClaim = context.User.FindFirst("AllowedAreas")?.Value;
            if (allowedAreasClaim != null)
            {
                var userAllowedAreas = allowedAreasClaim.Split(',', StringSplitOptions.RemoveEmptyEntries);

                // Kiểm tra nếu yêu cầu (requirement) có trong danh sách AllowedAreas
                if (userAllowedAreas.Contains(requirement.Area))
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
