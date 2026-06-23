namespace AgentForge.Verticals.Travel.Models;

public sealed record TravelMediaItem(
    string Id,
    string Destination,
    string Purpose,
    string MediaType,
    string Title,
    string? Url,
    string? Caption,
    string? Filename,
    double? Latitude,
    double? Longitude,
    string? Label,
    string? Address,
    string? ContactName,
    string? ContactNumber);
