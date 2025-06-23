using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SphereSSLv2.Data;
using System;
using System.Diagnostics;

namespace SphereSSLv2.Pages.Shared
{
    public class _LayoutModel : PageModel
    {


        public void OnGet()
        {
            Random random = new Random();
            ViewData["TitleTag"] = SphereSSLTaglines.TaglineArray[random.Next(SphereSSLTaglines.TaglineArray.Length)];
        }
    }

}
