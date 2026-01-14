using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UsaepaySupportTestbench.Models;

namespace UsaepaySupportTestbench.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext dbContext)
    {
        if (await dbContext.Presets.AnyAsync())
        {
            return;
        }

        var presets = new List<Preset>
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
                  "amount": "12.34",
                  "cardNumber": "{{cardNumber}}",
                  "expDate": "{{expDate}}",
                  "cvv": "{{cvv}}",
                  "invoice": "INV-{{timestamp}}"
                }
                """,
                VariablesJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["cardNumber"] = "4111111111111111",
                    ["expDate"] = "1228",
                    ["cvv"] = "123",
                    ["timestamp"] = DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                }),
                Notes = "Generic sample sale payload. Replace with real USAePay fields as needed.",
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
                BodyTemplate = "{ \"amount\": 12.34, \"cardNumber\": \"4111111111111111\", ",
                Notes = "Intentional malformed JSON to test validation errors.",
                TagsJson = "[\"quick\",\"rest\",\"malformed\"]",
                IsQuickPreset = true,
                IsSystemPreset = true
            }
        };

        dbContext.Presets.AddRange(presets);
        await dbContext.SaveChangesAsync();
    }
}
