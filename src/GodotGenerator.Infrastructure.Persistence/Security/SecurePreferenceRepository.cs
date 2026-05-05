#nullable enable
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Infrastructure.Persistence.Security;

/// <summary>
/// Decorator over <see cref="JsonPreferenceRepository"/> that transparently encrypts
/// API key values at rest using <see cref="ISecretProtector"/>.
/// </summary>
/// <remarks>
/// <para>
/// Only values stored under keys prefixed with <c>api.keys.</c> are encrypted; all other
/// preferences are forwarded to the inner repository unchanged.
/// </para>
/// <para>
/// Encrypted values are stored with an <c>"ENC:"</c> marker prefix so that legacy
/// plaintext values (written before encryption was introduced) are still readable.
/// Legacy values are returned as-is until the user re-saves them via the UI, at which
/// point they are automatically encrypted on the next write.
/// </para>
/// </remarks>
internal sealed class SecurePreferenceRepository(
    JsonPreferenceRepository inner,
    ISecretProtector protector,
    ILogger<SecurePreferenceRepository> logger) : IPreferenceRepository
{
    /// <summary>
    /// Key prefix that identifies API key preference entries eligible for encryption.
    /// </summary>
    private const string ApiKeyPrefix = "api.keys.";

    /// <summary>
    /// Marker prepended to all Data Protection ciphertext tokens when persisted.
    /// Allows reliable distinction between encrypted and legacy plaintext values.
    /// </summary>
    private const string EncryptionMarker = "ENC:";

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var raw = await inner.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (raw is null || !IsApiKey(key))
        {
            return raw;
        }

        return DecryptValue(key, raw);
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        var stored = value is not null && IsApiKey(key)
            ? EncryptionMarker + protector.Protect(value)
            : value;

        await inner.SetAsync(key, stored, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether the given preference key holds an API key value.
    /// </summary>
    /// <param name="key">Preference key to test.</param>
    /// <returns><see langword="true"/> when the key is an API key entry.</returns>
    private static bool IsApiKey(string key) =>
        key.StartsWith(ApiKeyPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Decrypts an API key value, handling both encrypted and legacy plaintext formats.
    /// </summary>
    /// <param name="key">Preference key (used only for diagnostic logging).</param>
    /// <param name="raw">Raw stored value.</param>
    /// <returns>
    /// Decrypted plaintext for encrypted values; the original raw value for legacy plaintext;
    /// <see langword="null"/> when the encrypted token cannot be decrypted (machine move / key rotation).
    /// </returns>
    private string? DecryptValue(string key, string raw)
    {
        if (!raw.StartsWith(EncryptionMarker, StringComparison.Ordinal))
        {
            // Legacy plaintext — return as-is until the user re-saves and triggers encryption.
            logger.LogDebug(
                "Preference key '{Key}' holds a legacy plaintext value. It will be encrypted on next save.",
                key);
            return raw;
        }

        var ciphertext = raw[EncryptionMarker.Length..];
        var decrypted = protector.TryUnprotect(ciphertext);
        if (decrypted is null)
        {
            // Key rotation or machine move: secret is unreadable; caller should prompt re-entry.
            logger.LogWarning(
                "Cannot decrypt preference key '{Key}'. The stored secret must be re-entered.",
                key);
        }

        return decrypted;
    }
}
