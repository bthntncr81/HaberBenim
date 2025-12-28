using System.Security.Cryptography;
using System.Text;

namespace HaberPlatform.Api.Services.XIntegration;

/// <summary>
/// OAuth 1.0a signing helper for X API authentication
/// </summary>
public static class OAuth1Helper
{
    /// <summary>
    /// Generate OAuth 1.0a Authorization header for a request
    /// </summary>
    public static string GenerateAuthHeader(
        string httpMethod,
        string url,
        string consumerKey,
        string consumerSecret,
        string accessToken,
        string accessTokenSecret,
        Dictionary<string, string>? additionalParams = null)
    {
        var oauth = new Dictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_token"] = accessToken,
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = GetTimestamp(),
            ["oauth_nonce"] = GetNonce(),
            ["oauth_version"] = "1.0"
        };

        // Create signature base string
        var allParams = new SortedDictionary<string, string>(oauth);
        if (additionalParams != null)
        {
            foreach (var param in additionalParams)
            {
                allParams[param.Key] = param.Value;
            }
        }

        // For POST requests with JSON body, don't include body in signature
        // Only include query parameters from the URL
        var uri = new Uri(url);
        if (!string.IsNullOrEmpty(uri.Query))
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            foreach (string? key in queryParams.AllKeys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    allParams[key] = queryParams[key] ?? "";
                }
            }
        }

        // Build base URL (without query string)
        var baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

        // Build parameter string
        var paramString = string.Join("&", 
            allParams.Select(kv => $"{UrlEncode(kv.Key)}={UrlEncode(kv.Value)}"));

        // Build signature base string
        var signatureBase = $"{httpMethod.ToUpperInvariant()}&{UrlEncode(baseUrl)}&{UrlEncode(paramString)}";

        // Create signing key
        var signingKey = $"{UrlEncode(consumerSecret)}&{UrlEncode(accessTokenSecret)}";

        // Generate signature
        using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey));
        var signatureBytes = hmac.ComputeHash(Encoding.ASCII.GetBytes(signatureBase));
        var signature = Convert.ToBase64String(signatureBytes);

        oauth["oauth_signature"] = signature;

        // Build Authorization header
        var headerParams = oauth
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{UrlEncode(kv.Key)}=\"{UrlEncode(kv.Value)}\"");

        return $"OAuth {string.Join(", ", headerParams)}";
    }

    private static string GetTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    }

    private static string GetNonce()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string UrlEncode(string value)
    {
        // RFC 3986 compliant URL encoding
        var encoded = new StringBuilder();
        foreach (char c in value)
        {
            if ((c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c == '-' || c == '_' || c == '.' || c == '~')
            {
                encoded.Append(c);
            }
            else
            {
                foreach (byte b in Encoding.UTF8.GetBytes(c.ToString()))
                {
                    encoded.Append('%');
                    encoded.Append(b.ToString("X2"));
                }
            }
        }
        return encoded.ToString();
    }
}

