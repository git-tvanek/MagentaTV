namespace MagentaTV.Extensions
{
    public static class SecurityExtensions
    {
        public static IServiceCollection AddSecurityValidation(this IServiceCollection services, IConfiguration configuration)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            // Validate encryption keys are not hardcoded in production
            if (environment == "Production")
            {
                ValidateProductionSecurity(configuration);
            }

            return services;
        }

        private static void ValidateProductionSecurity(IConfiguration configuration)
        {
            // Check for hardcoded encryption keys
            var sessionKey = configuration["Session:EncryptionKey"];
            if (!string.IsNullOrEmpty(sessionKey) &&
                (sessionKey.Contains("development") || sessionKey.Contains("dev") || sessionKey.Length < 32))
            {
                throw new InvalidOperationException("Production environment detected with unsafe encryption key. Use environment variables.");
            }

            // Validate required environment variables
            var requiredEnvVars = new[]
            {
            "SESSION_ENCRYPTION_KEY",
            "DATABASE_CONNECTION_STRING", // if using database
            "API_RATE_LIMIT_KEY" // if using external rate limiting
        };

            foreach (var envVar in requiredEnvVars)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar)))
                {
                    throw new InvalidOperationException($"Required environment variable {envVar} is not set");
                }
            }
        }
    }
}
