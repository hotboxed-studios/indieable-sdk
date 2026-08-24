using System.Net.Http;
using System.Collections.Generic;

namespace IndieableSdk
{
    /// <summary>
    /// Configuration for the engine-agnostic .NET client.
    /// Constructing the client is local and performs no network request.
    /// </summary>
    public sealed class IndieableClientOptions
    {
        public string BaseUrl { get; set; } = "https://indieable.com";
        public string PublicGameKey { get; set; } = "";
        public string SdkVersion { get; set; } = "dotnet-0.6.1";
        public string BuildVersion { get; set; } = "";
        public string Platform { get; set; } = "";
        public string Environment { get; set; } = "production";
        public string Engine { get; set; } = ".NET";
        public string EngineVersion { get; set; } = System.Environment.Version.ToString();
        public string LocalProfileRef { get; set; } = "";
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);
        public bool AutoClearInvalidIdentity { get; set; } = true;

        /// <summary>
        /// Optional public or locally resolved headers applied to every
        /// request. SDK-owned headers such as Authorization and Content-Type
        /// cannot be overridden.
        /// </summary>
        public IDictionary<string, string> RequestHeaders { get; } =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Optional host-owned HttpClient. The Indieable client never disposes it.
        /// </summary>
        public HttpClient? HttpClient { get; set; }

        /// <summary>
        /// Optional platform-specific secure storage. The file implementation is
        /// used when this is null.
        /// </summary>
        public IIndieableIdentityStorage? IdentityStorage { get; set; }
    }
}
