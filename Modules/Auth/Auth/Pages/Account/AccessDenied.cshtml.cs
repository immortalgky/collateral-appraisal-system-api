using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.Pages.Account;

[AllowAnonymous]
public class AccessDenied : PageModel
{
    public void OnGet()
    {
    }
}
