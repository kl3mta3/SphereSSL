using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SphereSSLv2.Data.Helpers;

namespace SphereSSLv2.Pages.Shared
{
    public class _LayoutModel : PageModel
    {


        public void OnGet()
        {
            Random random = new Random();
            ViewData["TitleTag"] = SphereSSLTaglines.TaglineArray[random.Next(SphereSSLTaglines.TaglineArray.Length)];
        }

        public async Task<IActionResult> OnPostLogOffAsync()
        {
            // Clear the session (or authentication cookie)
            HttpContext.Session.Clear();
            // For cookie auth: await HttpContext.SignOutAsync();
            return new JsonResult(new { success = true });
        }
    }

}
