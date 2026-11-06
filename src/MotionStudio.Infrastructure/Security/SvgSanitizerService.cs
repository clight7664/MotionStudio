using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using MotionStudio.Application.Contracts;
using MotionStudio.Domain.Models;

namespace MotionStudio.Infrastructure.Security;

public sealed partial class SvgSanitizerService : ISvgSanitizer
{
    public const int MaxSvgLength = 256 * 1024;

    private static readonly HashSet<string> ForbiddenElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script",
        "foreignObject",
        "iframe",
        "object",
        "embed",
        "audio",
        "video",
        "link",
        "meta",
    };

    public SvgSanitizeResult InspectAndSanitize(string rawSvg)
    {
        if (string.IsNullOrWhiteSpace(rawSvg))
        {
            return new SvgSanitizeResult(false, "ERR_EMPTY_SVG", "SVG input cannot be empty.", null, 0, 0, "#6B9CFF");
        }

        if (rawSvg.Length > MaxSvgLength)
        {
            return new SvgSanitizeResult(false, "ERR_SVG_TOO_LARGE", "SVG exceeds the 256 KB limit.", null, 0, 0, "#6B9CFF");
        }

        if (rawSvg.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            rawSvg.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase))
        {
            return new SvgSanitizeResult(false, "ERR_SVG_XXE", "XML DOCTYPE and ENTITY declarations are prohibited.", null, 0, 0, "#6B9CFF");
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxSvgLength,
        };

        XDocument doc;
        try
        {
            using var stringReader = new StringReader(rawSvg);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            doc = XDocument.Load(xmlReader);
        }
        catch (XmlException ex)
        {
            return new SvgSanitizeResult(false, "ERR_INVALID_SVG_XML", $"Malformed SVG XML: {ex.Message}", null, 0, 0, "#6B9CFF");
        }

        XElement? root = doc.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
        {
            return new SvgSanitizeResult(false, "ERR_NOT_SVG_ROOT", "Root element must be <svg>.", null, 0, 0, "#6B9CFF");
        }

        foreach (XElement element in root.DescendantsAndSelf())
        {
            string localName = element.Name.LocalName;
            if (ForbiddenElements.Contains(localName))
            {
                return new SvgSanitizeResult(
                    false,
                    "ERR_FORBIDDEN_SVG_ELEMENT",
                    $"Forbidden SVG element '<{localName}>' detected.",
                    null,
                    0,
                    0,
                    "#6B9CFF");
            }

            foreach (XAttribute attr in element.Attributes())
            {
                string attrName = attr.Name.LocalName;
                string attrVal = attr.Value.Trim();

                if (attrName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    return new SvgSanitizeResult(
                        false,
                        "ERR_FORBIDDEN_SVG_EVENT",
                        $"Event handler attribute '{attrName}' is prohibited in SVG.",
                        null,
                        0,
                        0,
                        "#6B9CFF");
                }

                if (attrVal.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
                    attrVal.Contains("data:text/html", StringComparison.OrdinalIgnoreCase) ||
                    attrVal.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    attrVal.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    attrVal.StartsWith("//", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(attrName, "xmlns", StringComparison.OrdinalIgnoreCase))
                    {
                        return new SvgSanitizeResult(
                            false,
                            "ERR_EXTERNAL_SVG_RESOURCE",
                            $"External or executable URI in attribute '{attrName}' is prohibited.",
                            null,
                            0,
                            0,
                            "#6B9CFF");
                    }
                }
            }
        }

        double width = ParseDimension(root.Attribute("width")?.Value, 200.0);
        double height = ParseDimension(root.Attribute("height")?.Value, 200.0);

        string fill = "#6B9CFF";
        XAttribute? fillAttr = root.DescendantsAndSelf()
            .Select(e => e.Attribute("fill"))
            .FirstOrDefault(a => a is not null && HexMatchRegex().IsMatch(a.Value.Trim()));

        if (fillAttr is not null)
        {
            fill = DomainGuard.NormalizeHexColor(fillAttr.Value.Trim());
        }

        return new SvgSanitizeResult(
            true,
            null,
            null,
            doc.ToString(SaveOptions.DisableFormatting),
            Math.Clamp(width, 16.0, 4096.0),
            Math.Clamp(height, 16.0, 4096.0),
            fill);
    }

    private static double ParseDimension(string? raw, double fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        string cleaned = raw.Trim().Replace("px", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) &&
            double.IsFinite(val) &&
            val > 0)
        {
            return val;
        }

        return fallback;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexMatchRegex();
}
