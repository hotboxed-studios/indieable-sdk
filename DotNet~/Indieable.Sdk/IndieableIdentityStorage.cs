using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IndieableSdk
{
    public sealed class IndieableStoredIdentity
    {
        public string InstallationCredential { get; set; } = "";
        public string LocalProfileRef { get; set; } = "";
    }

    public interface IIndieableIdentityStorage
    {
        ValueTask<IndieableStoredIdentity?> LoadAsync(
            string storageKey,
            CancellationToken cancellationToken = default);

        ValueTask SaveAsync(
            string storageKey,
            IndieableStoredIdentity identity,
            CancellationToken cancellationToken = default);

        ValueTask ClearAsync(
            string storageKey,
            CancellationToken cancellationToken = default);
    }

    public sealed class IndieableMemoryIdentityStorage : IIndieableIdentityStorage
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, IndieableStoredIdentity> _values =
            new(StringComparer.Ordinal);

        public ValueTask<IndieableStoredIdentity?> LoadAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (!_values.TryGetValue(storageKey, out var value))
                    return ValueTask.FromResult<IndieableStoredIdentity?>(null);

                return ValueTask.FromResult<IndieableStoredIdentity?>(
                    new IndieableStoredIdentity
                    {
                        InstallationCredential = value.InstallationCredential,
                        LocalProfileRef = value.LocalProfileRef
                    });
            }
        }

        public ValueTask SaveAsync(
            string storageKey,
            IndieableStoredIdentity identity,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(identity);

            lock (_gate)
            {
                _values[storageKey] = new IndieableStoredIdentity
                {
                    InstallationCredential = identity.InstallationCredential,
                    LocalProfileRef = identity.LocalProfileRef
                };
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate) _values.Remove(storageKey);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Basic file storage for desktop/server integrations. Production hosts may
    /// replace this with OS credential protection, a secret store, or another
    /// platform-appropriate implementation.
    /// </summary>
    public sealed class IndieableFileIdentityStorage : IIndieableIdentityStorage
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        private readonly string _directory;

        public IndieableFileIdentityStorage(string? directory = null)
        {
            _directory = string.IsNullOrWhiteSpace(directory)
                ? DefaultDirectory()
                : Path.GetFullPath(directory);
        }

        public async ValueTask<IndieableStoredIdentity?> LoadAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            var path = PathFor(storageKey);
            if (!File.Exists(path)) return null;

            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken)
                    .ConfigureAwait(false);
                return JsonSerializer.Deserialize<IndieableStoredIdentity>(
                    json,
                    JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public async ValueTask SaveAsync(
            string storageKey,
            IndieableStoredIdentity identity,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(identity);
            Directory.CreateDirectory(_directory);

            var path = PathFor(storageKey);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var json = JsonSerializer.Serialize(identity, JsonOptions);

            await File.WriteAllTextAsync(
                    temporary,
                    json,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);

            TryRestrictFile(temporary);
            File.Move(temporary, path, true);
            TryRestrictFile(path);
        }

        public ValueTask ClearAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = PathFor(storageKey);
            if (File.Exists(path)) File.Delete(path);
            return ValueTask.CompletedTask;
        }

        private string PathFor(string storageKey)
        {
            var bytes = SHA256.HashData(
                Encoding.UTF8.GetBytes(storageKey ?? ""));
            var name = Convert.ToHexString(bytes).ToLowerInvariant() + ".json";
            return Path.Combine(_directory, name);
        }

        private static string DefaultDirectory()
        {
            var root = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
            return Path.Combine(root, "Indieable");
        }

        private static void TryRestrictFile(string path)
        {
            if (OperatingSystem.IsWindows()) return;
            try
            {
                File.SetUnixFileMode(
                    path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Hosts that need stronger guarantees should inject secure storage.
            }
        }
    }
}
