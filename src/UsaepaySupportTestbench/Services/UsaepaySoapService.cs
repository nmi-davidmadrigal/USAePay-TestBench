using System.Diagnostics;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UsaepaySupportTestbench.Models;
using usaepay;

namespace UsaepaySupportTestbench.Services;

public sealed class UsaepaySoapService(
    IOptions<UsaepayOptions> options,
    IHttpContextAccessor httpContextAccessor,
    ILogger<UsaepaySoapService> logger)
{
    public async Task<SoapOperationResult> ExecuteAsync(SoapTransactionInput input, CancellationToken cancellationToken)
    {
        var session = httpContextAccessor.HttpContext?.Session;
        var endpoint = ResolveEndpoint(input.EndpointUrl, session);
        var (sourceKey, pin) = ResolveCredentials(input.SourceKey, input.Pin, session);
        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(pin))
        {
            throw new InvalidOperationException("SOAP Source Key and PIN are required. Provide them in the form, session, or appsettings.");
        }

        var clientIp = ResolveClientIp(input.ClientIp, httpContextAccessor.HttpContext);
        var token = BuildToken(sourceKey, pin, clientIp);

        using var client = CreateClient(endpoint);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            object? payload = input.Operation switch
            {
                SoapOperationType.RunSale => await client.runSaleAsync(token, BuildCardRequest(input, clientIp))
                    .WaitAsync(cancellationToken),
                SoapOperationType.RunAuthOnly => await client.runAuthOnlyAsync(token, BuildCardRequest(input, clientIp))
                    .WaitAsync(cancellationToken),
                SoapOperationType.RunCredit => await client.runCreditAsync(token, BuildCardRequest(input, clientIp))
                    .WaitAsync(cancellationToken),
                SoapOperationType.RunCheckSale => await client.runCheckSaleAsync(token, BuildCheckRequest(input, clientIp))
                    .WaitAsync(cancellationToken),
                SoapOperationType.RunQuickSale => await client.runQuickSaleAsync(
                        token,
                        RequireRefNum(input.RefNum, input.Operation),
                        BuildDetails(input, requireAmount: true),
                        input.AuthOnly)
                    .WaitAsync(cancellationToken),
                SoapOperationType.VoidTransaction => await client.voidTransactionAsync(
                        token,
                        RequireRefNum(input.RefNum, input.Operation))
                    .WaitAsync(cancellationToken),
                SoapOperationType.RefundTransaction => await client.refundTransactionAsync(
                        token,
                        RequireRefNum(input.RefNum, input.Operation),
                        RequireAmount(input.Amount, input.Operation))
                    .WaitAsync(cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported SOAP operation: {input.Operation}")
            };

            stopwatch.Stop();
            return new SoapOperationResult
            {
                Operation = input.Operation,
                Success = true,
                Payload = payload,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                EndpointUrl = endpoint
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new SoapOperationResult
            {
                Operation = input.Operation,
                Success = false,
                ErrorMessage = "SOAP request canceled.",
                LatencyMs = stopwatch.ElapsedMilliseconds,
                EndpointUrl = endpoint
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "SOAP {Operation} failed", input.Operation);
            return new SoapOperationResult
            {
                Operation = input.Operation,
                Success = false,
                ErrorMessage = ex.Message,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                EndpointUrl = endpoint
            };
        }
    }

    private ueSoapServerPortTypeClient CreateClient(string endpointUrl)
    {
        var binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport)
        {
            MaxBufferSize = int.MaxValue,
            MaxReceivedMessageSize = int.MaxValue,
            ReaderQuotas = XmlDictionaryReaderQuotas.Max,
            AllowCookies = true
        };

        return new ueSoapServerPortTypeClient(binding, new EndpointAddress(endpointUrl));
    }

    private string ResolveEndpoint(string? endpointOverride, ISession? session)
    {
        if (!string.IsNullOrWhiteSpace(endpointOverride))
        {
            var endpoint = NormalizeEndpoint(endpointOverride);
            EnsureEndpointHasWdslKey(endpoint);
            return endpoint;
        }

        const string prefix = "Usaepay:Sandbox";
        var sessionEndpoint = session?.GetString($"{prefix}:SoapEndpoint");
        if (!string.IsNullOrWhiteSpace(sessionEndpoint))
        {
            var endpoint = NormalizeEndpoint(sessionEndpoint);
            EnsureEndpointHasWdslKey(endpoint);
            return endpoint;
        }

        var envOptions = options.Value.Sandbox;

        if (string.IsNullOrWhiteSpace(envOptions.SoapEndpoint))
        {
            throw new InvalidOperationException("SOAP endpoint not configured for sandbox.");
        }

        var resolvedEndpoint = NormalizeEndpoint(envOptions.SoapEndpoint);
        EnsureEndpointHasWdslKey(resolvedEndpoint);
        return resolvedEndpoint;
    }

    private (string? SourceKey, string? Pin) ResolveCredentials(
        string? sourceKeyOverride,
        string? pinOverride,
        ISession? session)
    {
        if (!string.IsNullOrWhiteSpace(sourceKeyOverride) && !string.IsNullOrWhiteSpace(pinOverride))
        {
            return (sourceKeyOverride.Trim(), pinOverride.Trim());
        }

        const string prefix = "Usaepay:Sandbox";
        var sessionSourceKey = session?.GetString($"{prefix}:SourceKey") ?? session?.GetString($"{prefix}:ApiKey");
        var sessionPin = session?.GetString($"{prefix}:Pin") ?? session?.GetString($"{prefix}:ApiSecret");
        if (!string.IsNullOrWhiteSpace(sessionSourceKey) && !string.IsNullOrWhiteSpace(sessionPin))
        {
            return (sessionSourceKey, sessionPin);
        }

        var envOptions = options.Value.Sandbox;

        return (envOptions.SourceKey ?? envOptions.ApiKey, envOptions.Pin ?? envOptions.ApiSecret);
    }

    private static string ResolveClientIp(string? clientIpOverride, HttpContext? httpContext)
    {
        if (!string.IsNullOrWhiteSpace(clientIpOverride))
        {
            return clientIpOverride.Trim();
        }

        var remoteIp = httpContext?.Connection.RemoteIpAddress?.ToString();
        return remoteIp ?? string.Empty;
    }

    private static ueSecurityToken BuildToken(string sourceKey, string pin, string clientIp)
    {
        var hash = new ueHash
        {
            Type = "md5",
            Seed = Guid.NewGuid().ToString()
        };

        var prehash = string.Concat(sourceKey, hash.Seed, pin);
        hash.HashValue = GenerateHash(prehash);

        return new ueSecurityToken
        {
            SourceKey = sourceKey,
            ClientIP = clientIp,
            PinHash = hash
        };
    }

    private static string GenerateHash(string input)
    {
        var data = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(data).ToLowerInvariant();
    }

    private static TransactionRequestObject BuildCardRequest(SoapTransactionInput input, string clientIp)
    {
        RequireAmount(input.Amount, input.Operation);
        return new TransactionRequestObject
        {
            AccountHolder = input.Cardholder ?? string.Empty,
            ClientIP = clientIp,
            CreditCardData = BuildCardData(input),
            Details = BuildDetails(input, requireAmount: true),
            Software = "UsaepaySupportTestbench"
        };
    }

    private static TransactionRequestObject BuildCheckRequest(SoapTransactionInput input, string clientIp)
    {
        RequireAmount(input.Amount, input.Operation);
        return new TransactionRequestObject
        {
            AccountHolder = input.Cardholder ?? string.Empty,
            ClientIP = clientIp,
            CheckData = BuildCheckData(input),
            Details = BuildDetails(input, requireAmount: true),
            Software = "UsaepaySupportTestbench"
        };
    }

    private static TransactionDetail BuildDetails(SoapTransactionInput input, bool requireAmount)
    {
        var details = new TransactionDetail
        {
            Description = input.Description ?? string.Empty,
            Invoice = input.Invoice ?? string.Empty
        };

        if (input.Amount.HasValue)
        {
            details.Amount = (double)input.Amount.Value;
            details.AmountSpecified = true;
        }
        else if (requireAmount)
        {
            RequireAmount(null, input.Operation);
        }

        return details;
    }

    private static CreditCardData BuildCardData(SoapTransactionInput input)
    {
        return new CreditCardData
        {
            CardNumber = RequireValue(input.CardNumber, "Card number", input.Operation),
            CardExpiration = RequireValue(input.CardExpiration, "Card expiration", input.Operation),
            CardCode = input.CardCode ?? string.Empty,
            AvsStreet = input.AvsStreet ?? string.Empty,
            AvsZip = input.AvsZip ?? string.Empty
        };
    }

    private static CheckData BuildCheckData(SoapTransactionInput input)
    {
        return new CheckData
        {
            Routing = RequireValue(input.CheckRouting, "Routing number", input.Operation),
            Account = RequireValue(input.CheckAccount, "Account number", input.Operation),
            CheckNumber = RequireValue(input.CheckNumber, "Check number", input.Operation),
            AccountType = input.CheckAccountType ?? string.Empty
        };
    }

    private static string RequireRefNum(string? refNum, SoapOperationType operation)
    {
        return RequireValue(refNum, "RefNum", operation);
    }

    private static double RequireAmount(decimal? amount, SoapOperationType operation)
    {
        if (!amount.HasValue)
        {
            throw new InvalidOperationException($"{operation} requires an amount.");
        }

        return (double)amount.Value;
    }

    private static string RequireValue(string? value, string fieldName, SoapOperationType operation)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{operation} requires {fieldName}.");
        }

        return value.Trim();
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        var trimmed = endpoint.Trim();
        if (trimmed.EndsWith("/usaepay.wsdl", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^"/usaepay.wsdl".Length];
        }

        return trimmed.TrimEnd('/');
    }

    private static void EnsureEndpointHasWdslKey(string endpoint)
    {
        var normalized = endpoint.TrimEnd('/');
        if (!normalized.EndsWith("/soap/gate", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            "SOAP endpoint is missing the WSDL key. Use your Developer Center WSDL URL " +
            "(https://sandbox.usaepay.com/soap/gate/<key>/usaepay.wsdl) or set SoapEndpoint " +
            "to https://sandbox.usaepay.com/soap/gate/<key>.");
    }
}
