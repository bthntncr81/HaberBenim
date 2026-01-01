namespace HaberPlatform.Api.Services;

/// <summary>
/// Unit tests for ContentCleaner - DetectTruncation and Clean methods
/// Run with: dotnet test or use test explorer
/// </summary>
public static class ContentCleanerTests
{
    /// <summary>
    /// Run all tests and return results
    /// </summary>
    public static (int Passed, int Failed, List<string> Failures) RunAllTests()
    {
        var failures = new List<string>();
        var passed = 0;
        var failed = 0;

        // DetectTruncation Tests
        void AssertTruncated(string? content, bool expected, string testName)
        {
            var result = ContentCleaner.DetectTruncation(content);
            if (result == expected)
            {
                passed++;
            }
            else
            {
                failed++;
                failures.Add($"{testName}: Expected {expected}, got {result}");
            }
        }

        // Test 1: Turkish truncation phrase detection
        AssertTruncated(
            "Bu haberin detayları için Devamı için tıklayınız",
            true,
            "DetectTruncation_TurkishPhrase_ReturnsTrue");

        // Test 2: Turkish truncation phrase with missing chars
        AssertTruncated(
            "Haberin devamı devami için tiklayiniz",
            true,
            "DetectTruncation_TurkishPhraseVariant_ReturnsTrue");

        // Test 3: Short content with ellipsis
        AssertTruncated(
            "Bu kısa bir özet...",
            true,
            "DetectTruncation_ShortWithEllipsis_ReturnsTrue");

        // Test 4: Long content without truncation markers
        AssertTruncated(
            "Bu uzun bir haber metnidir. Birçok paragraf içermektedir. " +
            "Detaylı bilgiler verilmektedir. Kaynaklara atıfta bulunulmaktadır. " +
            "Uzmanların görüşleri aktarılmaktadır. Sonuç olarak önemli bir gelişme yaşanmıştır. " +
            "Bu gelişmenin etkileri uzun süre hissedilecektir. Yetkililer konuyla ilgili açıklama yaptı.",
            false,
            "DetectTruncation_LongContentNoMarkers_ReturnsFalse");

        // Test 5: English "read more" detection
        AssertTruncated(
            "This is a summary. Read more",
            true,
            "DetectTruncation_EnglishReadMore_ReturnsTrue");

        // Test 6: Empty content
        AssertTruncated(
            "",
            true,
            "DetectTruncation_EmptyContent_ReturnsTrue");

        // Test 7: Null content
        AssertTruncated(
            null,
            true,
            "DetectTruncation_NullContent_ReturnsTrue");

        // Test 8: "devamı..." pattern
        AssertTruncated(
            "Haber özeti burada devamı...",
            true,
            "DetectTruncation_DevamPattern_ReturnsTrue");

        // Clean Tests
        void AssertCleaned(string? input, string expected, string testName)
        {
            var result = ContentCleaner.Clean(input);
            if (result == expected)
            {
                passed++;
            }
            else
            {
                failed++;
                failures.Add($"{testName}: Expected '{expected}', got '{result}'");
            }
        }

        // Test 9: Clean Turkish truncation phrase
        AssertCleaned(
            "Bu haberin detayları. Devamı için tıklayınız",
            "Bu haberin detayları.",
            "Clean_RemovesTurkishPhrase");

        // Test 10: Clean with trailing ellipsis
        AssertCleaned(
            "Kısa özet...",
            "Kısa özet",
            "Clean_RemovesTrailingEllipsis");

        // Test 11: Clean normalizes whitespace
        AssertCleaned(
            "Çoklu   boşluklar    var",
            "Çoklu boşluklar var",
            "Clean_NormalizesWhitespace");

        // Test 12: Clean handles null
        AssertCleaned(
            null,
            "",
            "Clean_HandlesNull");

        // Test 13: Long content keeps ellipsis (not a teaser)
        var longContent = new string('a', 600) + "...";
        var cleanedLong = ContentCleaner.Clean(longContent);
        if (cleanedLong.EndsWith("..."))
        {
            passed++;
        }
        else
        {
            failed++;
            failures.Add("Clean_LongContentKeepsEllipsis: Expected ellipsis to be preserved");
        }

        return (passed, failed, failures);
    }

    /// <summary>
    /// Prints test results to console
    /// </summary>
    public static void PrintResults()
    {
        var (passed, failed, failures) = RunAllTests();
        
        Console.WriteLine($"ContentCleaner Tests: {passed} passed, {failed} failed");
        
        if (failures.Count > 0)
        {
            Console.WriteLine("\nFailures:");
            foreach (var failure in failures)
            {
                Console.WriteLine($"  - {failure}");
            }
        }
    }
}
