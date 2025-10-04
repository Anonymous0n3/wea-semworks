using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Models
{
    public interface IWidget
    {
        string Name { get; }
        string DisplayName { get; }
        Task<IViewComponentResult> InvokeAsync();
    }
}
