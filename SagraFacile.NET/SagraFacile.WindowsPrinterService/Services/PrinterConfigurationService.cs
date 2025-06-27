using Microsoft.Extensions.Logging;
using SagraFacile.WindowsPrinterService.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SagraFacile.WindowsPrinterService.Services
{
    public interface IPrinterConfigurationService
    {
        Task<(PrintMode PrintMode, string? WindowsPrinterName)> FetchConfigurationAsync(string hubHostAndPort, string instanceGuid, string? localPrinterName, CancellationToken cancellationToken);
    }

    public class PrinterConfigurationService : IPrinterConfigurationService
    {
        private readonly ILogger<PrinterConfigurationService> _logger;
        private static readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        static PrinterConfigurationService()
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                Console.WriteLine("[WARN] SOLO SVILUPPO: Validazione certificato SSL bypassata per HttpClient (recupero configurazione stampante). NON USARE IN PRODUZIONE.");
                return true;
            };
            _httpClient = new HttpClient(httpClientHandler);
        }

        public PrinterConfigurationService(ILogger<PrinterConfigurationService> logger)
        {
            _logger = logger;
        }

        public async Task<(PrintMode PrintMode, string? WindowsPrinterName)> FetchConfigurationAsync(
            string hubHostAndPort, 
            string instanceGuid, 
            string? localPrinterName, 
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(hubHostAndPort) || string.IsNullOrWhiteSpace(instanceGuid))
            {
                _logger.LogError("Cannot fetch printer configuration: Hub URL or Instance GUID missing.");
                return (PrintMode.Immediate, localPrinterName);
            }

            if (!Uri.TryCreate(hubHostAndPort.Trim(), UriKind.Absolute, out Uri? baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                _logger.LogError($"Invalid Hub URL '{hubHostAndPort}' during configuration fetch.");
                return (PrintMode.Immediate, localPrinterName);
            }

            string configUrl = new Uri(baseUri, $"/api/printers/config/{instanceGuid}").ToString();
            _logger.LogInformation($"Fetching printer configuration from: {configUrl}");

            try
            {
                var backendConfig = await _httpClient.GetFromJsonAsync<PrinterConfigDto>(configUrl, _jsonSerializerOptions, cancellationToken);

                if (backendConfig != null)
                {
                    _logger.LogInformation($"Configuration retrieved: PrintMode={backendConfig.PrintMode}. WindowsPrinterName (from local)='{localPrinterName}'");
                    return (backendConfig.PrintMode, localPrinterName);
                }
                else
                {
                    _logger.LogWarning("PrintMode configuration not found or empty from backend. Using PrintMode.Immediate.");
                    return (PrintMode.Immediate, localPrinterName);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"HTTP error during printer configuration fetch from {configUrl}.");
                return (PrintMode.Immediate, localPrinterName);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"JSON error during printer configuration parsing from {configUrl}.");
                return (PrintMode.Immediate, localPrinterName);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, $"Timeout during printer configuration fetch from {configUrl}.");
                return (PrintMode.Immediate, localPrinterName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Generic error during printer configuration fetch from {configUrl}.");
                return (PrintMode.Immediate, localPrinterName);
            }
        }
    }
}
