using System.Globalization;
using System.Text.RegularExpressions;

namespace AgentForge.WebApi.Services;

public enum OutboundMediaKind
{
    Image,
    Video,
    Audio,
    Document,
    Sticker,
    Location,
    Contact
}

public sealed record OutboundMediaMarker(
    OutboundMediaKind Kind,
    string Raw,
    string? Url = null,
    string? Caption = null,
    string? Filename = null,
    double? Latitude = null,
    double? Longitude = null,
    string? Label = null,
    string? Address = null,
    string? ContactName = null,
    string? ContactNumber = null)
{
    public bool IsValid => Kind switch
    {
        OutboundMediaKind.Location => Latitude.HasValue && Longitude.HasValue,
        OutboundMediaKind.Contact => !string.IsNullOrWhiteSpace(ContactName) && !string.IsNullOrWhiteSpace(ContactNumber),
        _ => !string.IsNullOrWhiteSpace(Url)
    };
}

public sealed record ParsedMediaReply(string Text, IReadOnlyList<OutboundMediaMarker> Markers);

public sealed partial class MediaMarkerParser
{
    public ParsedMediaReply Parse(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return new ParsedMediaReply(string.Empty, []);
        }

        var matches = MediaMarker().Matches(reply);
        var markers = matches
            .Select(match => ParseMatch(match))
            .Where(marker => marker.IsValid)
            .ToArray();
        var text = MediaMarker().Replace(reply, string.Empty).Trim();

        return new ParsedMediaReply(text, markers);
    }

    private static OutboundMediaMarker ParseMatch(Match match)
    {
        var raw = match.Value;
        var kindText = match.Groups["kind"].Value;
        var body = match.Groups["body"].Value;
        var kind = Enum.Parse<OutboundMediaKind>(kindText, ignoreCase: true);
        var parts = body
            .Split('|', StringSplitOptions.TrimEntries)
            .Select(part => part.Trim())
            .ToArray();

        return kind switch
        {
            OutboundMediaKind.Image => new OutboundMediaMarker(kind, raw, Url: GetPart(parts, 0), Caption: GetPart(parts, 1)),
            OutboundMediaKind.Video => new OutboundMediaMarker(kind, raw, Url: GetPart(parts, 0), Caption: GetPart(parts, 1)),
            OutboundMediaKind.Audio => new OutboundMediaMarker(kind, raw, Url: GetPart(parts, 0), Filename: GetPart(parts, 1)),
            OutboundMediaKind.Document => new OutboundMediaMarker(kind, raw, Url: GetPart(parts, 0), Filename: GetPart(parts, 1), Caption: GetPart(parts, 2)),
            OutboundMediaKind.Sticker => new OutboundMediaMarker(kind, raw, Url: GetPart(parts, 0)),
            OutboundMediaKind.Location => ParseLocation(raw, parts, kind),
            OutboundMediaKind.Contact => new OutboundMediaMarker(kind, raw, ContactName: GetPart(parts, 0), ContactNumber: GetPart(parts, 1)),
            _ => new OutboundMediaMarker(kind, raw)
        };
    }

    private static OutboundMediaMarker ParseLocation(string raw, IReadOnlyList<string> parts, OutboundMediaKind kind)
    {
        var coordinates = (GetPart(parts, 0) ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        if (coordinates.Length != 2 ||
            !double.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            return new OutboundMediaMarker(kind, raw);
        }

        return new OutboundMediaMarker(
            kind,
            raw,
            Latitude: latitude,
            Longitude: longitude,
            Label: GetPart(parts, 1),
            Address: GetPart(parts, 2));
    }

    private static string? GetPart(IReadOnlyList<string> parts, int index)
        => index < parts.Count && !string.IsNullOrWhiteSpace(parts[index]) ? parts[index] : null;

    [GeneratedRegex(@"\{\{(?<kind>image|video|audio|document|sticker|location|contact):(?<body>[^}]*)\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex MediaMarker();
}
