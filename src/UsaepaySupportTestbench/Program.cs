using Microsoft.EntityFrameworkCore;
using UsaepaySupportTestbench.Data;
using UsaepaySupportTestbench.Models;
using UsaepaySupportTestbench.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient("UsaepayProxy");

builder.Services.AddScoped<RedactionService>();
builder.Services.AddScoped<RestProxyService>();
builder.Services.AddScoped<SoapProxyService>();
builder.Services.AddScoped<UsaepaySoapService>();
builder.Services.AddScoped<PresetService>();
builder.Services.AddScoped<ScenarioRunService>();

builder.Services.Configure<UsaepayOptions>(builder.Configuration.GetSection("Usaepay"));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
    await DbSeeder.SeedAsync(dbContext);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapRazorPages();

app.MapPost("/api/proxy/rest", async (
        ProxyRestRequest request,
        RestProxyService restProxyService,
        ScenarioRunService scenarioRunService,
        CancellationToken cancellationToken) =>
    {
        var response = await restProxyService.ExecuteAsync(request, cancellationToken);
        await scenarioRunService.RecordRestAsync(request, response);

        return Results.Ok(response);
    })
    .WithName("RestProxy");

app.MapPost("/api/proxy/soap", async (
        ProxySoapRequest request,
        SoapProxyService soapProxyService,
        ScenarioRunService scenarioRunService,
        CancellationToken cancellationToken) =>
    {
        var response = await soapProxyService.ExecuteAsync(request, cancellationToken);
        await scenarioRunService.RecordSoapAsync(request, response);

        return Results.Ok(response);
    })
    .WithName("SoapProxy");

app.MapPost("/api/payjs/token", (PayJsTokenRequest request, HttpContext context) =>
{
    context.Session.SetString("PayJs:Token", request.Token);
    if (!string.IsNullOrWhiteSpace(request.PaymentKey))
    {
        context.Session.SetString("PayJs:PaymentKey", request.PaymentKey);
    }

    if (!string.IsNullOrWhiteSpace(request.MetadataJson))
    {
        context.Session.SetString("PayJs:Metadata", request.MetadataJson);
    }

    return Results.Ok(new { saved = true });
});

app.MapGet("/api/payjs/token", (HttpContext context) =>
{
    var token = context.Session.GetString("PayJs:Token") ?? string.Empty;
    var paymentKey = context.Session.GetString("PayJs:PaymentKey") ?? string.Empty;
    var metadata = context.Session.GetString("PayJs:Metadata") ?? string.Empty;
    return Results.Ok(new { token, paymentKey, metadata });
});

app.Run();
