namespace Shared.Configurations;

public class CorsConfiguration
{
    public const string SectionName = "Cors";
    public string[] AllowedOrigins { get; set; } = [];
}
