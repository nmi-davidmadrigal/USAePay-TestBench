using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UsaepaySupportTestbench.Models;

namespace UsaepaySupportTestbench.Pages.PayJs;

public class IndexModel : PageModel
{
    [BindProperty]
    public string? PublicKey { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? LastToken { get; private set; }
    public string? LastPaymentKey { get; private set; }

    public void OnGet()
    {
        LastToken = HttpContext.Session.GetString("PayJs:Token");
        LastPaymentKey = HttpContext.Session.GetString("PayJs:PaymentKey");

        PublicKey = HttpContext.Session.GetString("PayJs:PublicKey");
    }

    public IActionResult OnPostSaveConfig()
    {
        if (string.IsNullOrWhiteSpace(PublicKey))
        {
            ModelState.AddModelError(string.Empty, "Pay.js Public Key is required.");
            OnGet();
            return Page();
        }

        HttpContext.Session.SetString("PayJs:PublicKey", PublicKey.Trim());
        StatusMessage = "Saved Pay.js configuration to this session.";
        return RedirectToPage();
    }

    public IActionResult OnPostClearConfig()
    {
        HttpContext.Session.Remove("PayJs:PublicKey");
        StatusMessage = "Cleared Pay.js configuration from this session.";
        return RedirectToPage();
    }

    public IActionResult OnPostClearToken()
    {
        HttpContext.Session.Remove("PayJs:Token");
        HttpContext.Session.Remove("PayJs:PaymentKey");
        HttpContext.Session.Remove("PayJs:Metadata");
        StatusMessage = "Cleared Pay.js token from this session.";
        return RedirectToPage();
    }
}
