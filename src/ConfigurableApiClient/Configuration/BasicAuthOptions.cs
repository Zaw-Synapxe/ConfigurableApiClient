namespace ConfigurableApiClient.Configuration;

/// <summary>
/// Basic authentication credentials applied to a single API call.
/// </summary>
public sealed class BasicAuthOptions
{
    public bool Enabled { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}
