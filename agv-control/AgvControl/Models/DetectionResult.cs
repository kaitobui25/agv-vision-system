// ==========================================================================
// DetectionResult.cs — Vision AI JSON response models
// ==========================================================================
// Maps response from: GET http://localhost:8000/detect/latest
// Python uses snake_case → C# uses PascalCase with JsonPropertyName
// ==========================================================================

using System.Text.Json.Serialization;

namespace AgvControl.Models;

/// <summary>
/// Top-level response from Vision AI /detect/latest endpoint.
/// </summary>
public class VisionResponse
{
    [JsonPropertyName("detections")]
    public List<Detection> Detections { get; set; } = new();

    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; set; }

    [JsonPropertyName("total_objects")]
    public int TotalObjects { get; set; }
}

/// <summary>
/// Single detected object from YOLO inference.
/// </summary>
public class Detection
{
    [JsonPropertyName("object_class")]
    public string ObjectClass { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Bounding box (normalized 0-1) for DB storage.
    /// </summary>
    [JsonPropertyName("bbox")]
    public BoundingBox? Bbox { get; set; }

    /// <summary>
    /// Bounding box in pixels (for display/debugging).
    /// </summary>
    [JsonPropertyName("bbox_pixels")]
    public BoundingBox? BboxPixels { get; set; }

    /// <summary>
    /// Estimated distance from camera using pinhole model (meters).
    /// Null if bbox height is invalid or estimation failed.
    /// agv-control uses this + camera offset (300mm) for grid mapping.
    /// </summary>
    [JsonPropertyName("distance_meters")]
    public double? DistanceMeters { get; set; }
}

/// <summary>
/// Bounding box coordinates (used for both normalized and pixel formats).
/// </summary>
public class BoundingBox
{
    [JsonPropertyName("x1")]
    public double X1 { get; set; }

    [JsonPropertyName("y1")]
    public double Y1 { get; set; }

    [JsonPropertyName("x2")]
    public double X2 { get; set; }

    [JsonPropertyName("y2")]
    public double Y2 { get; set; }
}
