using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UsaepaySupportTestbench.Pages.PayJs;

public class IndexModel : PageModel
{
    public string? LastToken { get; private set; }
    public string? LastPaymentKey { get; private set; }

    public void OnGet()
    {
        LastToken = HttpContext.Session.GetString("PayJs:Token");
        LastPaymentKey = HttpContext.Session.GetString("PayJs:PaymentKey");
    }
}
