using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace IndieableSdk
{
    public interface IIndieableIdentityStorage
    {
        IndieableStoredIdentity Load(string storageKey);
        void Save(string storageKey, IndieableStoredIdentity identity);
        void Clear(string storageKey);
    }

    [Serializable]
    public sealed class IndieableStoredIdentity
    {
        public string InstallationCredential = "";
        public string LocalProfileRef = "";
    }

    // A random server-issued credential is stored in the game's persistent data
    // directory only after the Player permits telemetry or requests a persistent
    // feature. No hardware identifier or device fingerprint is used.
    public sealed class IndieableFileIdentityStorage : IIndieableIdentityStorage
    {
        public IndieableStoredIdentity Load(string storageKey)
        {
            try
            {
                var path = PathFor(storageKey);
                if (!File.Exists(path)) return null;
                var value = JsonUtility.FromJson<IndieableStoredIdentity>(File.ReadAllText(path));
                return value != null && !string.IsNullOrWhiteSpace(value.InstallationCredential) ? value : null;
            }
            catch { return null; }
        }

        public void Save(string storageKey, IndieableStoredIdentity identity)
        {
            if (identity == null || string.IsNullOrWhiteSpace(identity.InstallationCredential)) return;
            var path = PathFor(storageKey);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(identity));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }

        public void Clear(string storageKey)
        {
            try
            {
                var path = PathFor(storageKey);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
            catch { }
        }

        private static string PathFor(string storageKey)
        {
            var digest = Sha256(storageKey ?? "indieable");
            return Path.Combine(Application.persistentDataPath, "Indieable", digest + ".json");
        }

        private static string Sha256(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
                var builder = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
