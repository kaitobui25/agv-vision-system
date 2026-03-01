// ==========================================================================
// VisionClient.cs — REST client for Vision AI (Python FastAPI)
// ==========================================================================
// Calls GET http://localhost:8000/detect/latest to get obstacle detections.
// Used by AgvOrchestrator every 100ms in the control loop.
//
// Design:
// - S: Only responsible for HTTP communication with Vision AI
// - O: Interface allows swapping implementation (e.g., mock for testing)
// - D: AgvOrchestrator depends on IVisionClient, not this concrete class
// - KISS: Two methods only — detect + health check
// - Graceful degradation: returns null on failure, never throws
// ==========================================================================

using System.Text.Json;
using AgvControl.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgvControl.Services;

// ---------------------------------------------------------------------------
// Configuration — bound from appsettings.json "VisionAi" section
// ---------------------------------------------------------------------------
public class VisionAiSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public int TimeoutMs { get; set; } = 2000;
}

// ---------------------------------------------------------------------------
// Interface — contract for dependency injection
// ---------------------------------------------------------------------------
public interface IVisionClient
{
    /// <summary>
    /// Get latest detections from Vision AI.
    /// Returns null if Vision AI is unreachable or returns invalid data.
    /// </summary>
    Task<VisionResponse?> GetLatestDetectionsAsync();

    /// <summary>
    /// Check if Vision AI server is running.
    /// </summary>
    Task<bool> HealthCheckAsync();
}

// ---------------------------------------------------------------------------
// Implementation
// ---------------------------------------------------------------------------
public class VisionClient : IVisionClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VisionClient> _logger;

    // JSON options: Python uses snake_case, C# uses PascalCase
    // JsonPropertyName on DetectionResult.cs handles the mapping,
    // but we set PropertyNameCaseInsensitive as a safety net.
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public VisionClient(HttpClient httpClient,
                        IOptions<VisionAiSettings> settings,
                        ILogger<VisionClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Configure HttpClient from appsettings.json
        _httpClient.BaseAddress = new Uri(settings.Value.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMilliseconds(settings.Value.TimeoutMs);
    }

    public async Task<VisionResponse?> GetLatestDetectionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/detect/latest");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Vision AI returned HTTP {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<VisionResponse>(json, _jsonOptions);

            _logger.LogDebug("Vision AI: {Count} objects in {Time}ms",
                result?.TotalObjects, result?.ProcessingTimeMs);

            return result;
        }
        catch (TaskCanceledException)
        {
            // Timeout — Vision AI took longer than TimeoutMs
            _logger.LogWarning("Vision AI timeout (>{TimeoutMs}ms)", _httpClient.Timeout.TotalMilliseconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            // Connection refused — Vision AI not running
            _logger.LogWarning("Vision AI unreachable: {Message}", ex.Message);
            return null;
        }
        catch (JsonException ex)
        {
            // Invalid JSON — version mismatch between Python and C# models
            _logger.LogWarning("Vision AI invalid response: {Message}", ex.Message);
            return null;
        }
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
