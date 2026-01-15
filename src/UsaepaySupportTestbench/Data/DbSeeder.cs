using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UsaepaySupportTestbench.Models;

namespace UsaepaySupportTestbench.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext dbContext)
    {
        // Keep custom presets; upsert the system preset library so updates ship automatically.
        var systemPresets = new List<Preset>
        {
            new()
            {
                Name = "REST: Sample Sale (Sandbox)",
                ApiType = ApiType.Rest,
                Environment = EnvironmentType.Sandbox,
                RestMethod = "POST",
                RestPathOrEndpoint = "/v2/transactions",
                HeadersJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json"
                }),
                BodyTemplate = """
                {
                  "command": "cc:sale",
                  "amount": "5.00",
                  "amount_detail": {
                    "tax": "1.00",
                    "tip": "0.50"
                  },
                  "creditcard": {
                    "cardholder": "{{cardholder}}",
                    "number": "{{cardNumber}}",
                    "expiration": "{{expiration}}",
                    "cvc": "{{cvc}}",
                    "avs_street": "{{avsStreet}}",
                    "avs_zip": "{{avsZip}}"
                  },
                  "invoice": "INV-{{timestamp}}"
                }
                """,
                VariablesJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["cardholder"] = "John Doe",
                    ["cardNumber"] = "4000100011112224",
                    ["expiration"] = "1228",
                    ["cvc"] = "123",
                    ["avsStreet"] = "1234 Main",
                    ["avsZip"] = "12345"
                }),
                Notes = "Matches USAePay REST docs sample for /v2/transactions (cc:sale).",
                TagsJson = "[\"quick\",\"rest\",\"sale\"]",
                IsQuickPreset = true,
                IsSystemPreset = true
            },
            new()
            {
                Name = "SOAP: Sample Payment (Sandbox)",
                ApiType = ApiType.Soap,
                Environment = EnvironmentType.Sandbox,
                SoapAction = "ueSoapServer/ProcessTransaction",
                HeadersJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["Content-Type"] = "text/xml; charset=utf-8"
                }),
                BodyTemplate = """
                <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ue="urn:usaepay">
                  <soapenv:Header/>
                  <soapenv:Body>
                    <ue:ProcessTransaction>
                      <ue:Token>{{token}}</ue:Token>
                      <ue:Amount>{{amount}}</ue:Amount>
                      <ue:Command>sale</ue:Command>
                    </ue:ProcessTransaction>
                  </soapenv:Body>
                </soapenv:Envelope>
                """,
                VariablesJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["token"] = "sandbox-token",
                    ["amount"] = "12.34"
                }),
                Notes = "SOAP envelope placeholder. Replace with valid USAePay credentials/fields.",
                TagsJson = "[\"quick\",\"soap\"]",
                IsQuickPreset = true,
                IsSystemPreset = true
            },
            new()
            {
                Name = "REST: Malformed JSON (Sandbox)",
                ApiType = ApiType.Rest,
                Environment = EnvironmentType.Sandbox,
                RestMethod = "POST",
                RestPathOrEndpoint = "/v2/transactions",
                HeadersJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json"
                }),
                BodyTemplate = "{ \"command\": \"cc:sale\", \"amount\": \"5.00\", \"creditcard\": { \"number\": \"4000100011112224\", ",
                Notes = "Intentional malformed JSON to test validation errors.",
                TagsJson = "[\"quick\",\"rest\",\"malformed\"]",
                IsQuickPreset = true,
                IsSystemPreset = true
            }
        };

        foreach (var preset in systemPresets)
        {
            var existing = await dbContext.Presets.FirstOrDefaultAsync(p =>
                p.IsSystemPreset && p.Name == preset.Name && p.Environment == preset.Environment && p.ApiType == preset.ApiType);

            if (existing is null)
            {
                preset.CreatedAt = DateTime.UtcNow;
                preset.UpdatedAt = DateTime.UtcNow;
                dbContext.Presets.Add(preset);
                continue;
            }

            existing.RestMethod = preset.RestMethod;
            existing.RestPathOrEndpoint = preset.RestPathOrEndpoint;
            existing.SoapAction = preset.SoapAction;
            existing.HeadersJson = preset.HeadersJson;
            existing.BodyTemplate = preset.BodyTemplate;
            existing.VariablesJson = preset.VariablesJson;
            existing.Notes = preset.Notes;
            existing.TagsJson = preset.TagsJson;
            existing.IsQuickPreset = preset.IsQuickPreset;
            existing.IsSystemPreset = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
    }
}
