using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HaberPlatform.Api.Utils;

/// <summary>
/// Helper for generating URL-friendly slugs
/// </summary>
public static class SlugHelper
{
    // Turkish character mapping
    private static readonly Dictionary<char, char> TurkishMap = new()
    {
        { 'ğ', 'g' }, { 'Ğ', 'G' },
        { 'ü', 'u' }, { 'Ü', 'U' },
        { 'ş', 's' }, { 'Ş', 'S' },
        { 'ö', 'o' }, { 'Ö', 'O' },
        { 'ç', 'c' }, { 'Ç', 'C' },
        { 'ı', 'i' }, { 'İ', 'I' },
        { 'ä', 'a' }, { 'Ä', 'A' },
        { 'é', 'e' }, { 'É', 'E' },
        { 'è', 'e' }, { 'È', 'E' },
        { 'ë', 'e' }, { 'Ë', 'E' },
        { 'à', 'a' }, { 'À', 'A' },
        { 'â', 'a' }, { 'Â', 'A' },
        { 'ô', 'o' }, { 'Ô', 'O' },
        { 'û', 'u' }, { 'Û', 'U' },
        { 'î', 'i' }, { 'Î', 'I' },
        { 'ê', 'e' }, { 'Ê', 'E' },
        { 'ñ', 'n' }, { 'Ñ', 'N' }
    };

    /// <summary>
    /// Generate a URL-friendly slug from a title
    /// </summary>
    public static string GenerateSlug(string? title, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "haber";
        }

        // Convert to lowercase
        var slug = title.ToLowerInvariant();

        // Replace Turkish characters
        var sb = new StringBuilder(slug.Length);
        foreach (var c in slug)
        {
            if (TurkishMap.TryGetValue(c, out var replacement))
            {
                sb.Append(replacement);
            }
            else
            {
                sb.Append(c);
            }
        }
        slug = sb.ToString();

        // Normalize and remove diacritics
        slug = RemoveDiacritics(slug);

        // Replace spaces and underscores with hyphens
        slug = Regex.Replace(slug, @"[\s_]+", "-");

        // Remove non-alphanumeric characters except hyphens
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");

        // Replace multiple hyphens with single hyphen
        slug = Regex.Replace(slug, @"-+", "-");

        // Trim hyphens from start and end
        slug = slug.Trim('-');

        // Ensure max length
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        // Fallback if empty
        if (string.IsNullOrEmpty(slug))
        {
            return "haber";
        }

        return slug;
    }

    /// <summary>
    /// Generate a full path for the published content
    /// </summary>
    public static string GeneratePath(Guid id, string slug)
    {
        return $"/news/{id:N}-{slug}";
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

