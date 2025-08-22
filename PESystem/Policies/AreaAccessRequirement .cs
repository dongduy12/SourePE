using Microsoft.AspNetCore.Authorization;

namespace PESystem.Policies
{
    public class AreaAccessRequirement : IAuthorizationRequirement
    {
        public string Area { get; set; }
        public AreaAccessRequirement(string area)
        {
            Area = area;
        }
    }
}
