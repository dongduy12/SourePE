namespace PESystem.Helpers
{
    public static class NavigationHelper
    {
        public static string IsActive(this Microsoft.AspNetCore.Mvc.Rendering.ViewContext viewContext, string controller, string action)
        {
            var currentController = viewContext.RouteData.Values["Controller"]?.ToString();
            var currentAction = viewContext.RouteData.Values["Action"]?.ToString();

            return (currentController == controller && currentAction == action) ? "active" : "";
        }
    }
}