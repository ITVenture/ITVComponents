using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect
{
    public class TranslatedTokenResponse
    {
        public string AccessToken { get; init; } = null!;

        public string TokenType { get; init; } = null!;

        public DateTimeOffset ExpiresAt { get; init; }

        public string? RefreshToken { get; init; }

        public string? Scope { get; init; }
    }
}
