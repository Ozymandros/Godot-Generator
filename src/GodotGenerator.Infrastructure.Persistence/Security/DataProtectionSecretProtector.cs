#nullable enable
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Infrastructure.Persistence.Security;

/// <summary>
/// <see cref="ISecretProtector"/> implementation backed by ASP.NET Core Data Protection.
/// Uses OS-native key storage (DPAPI on Windows, Keychain on macOS, XDG on Linux) and
/// isolates keys by application name to prevent cross-app decryption.
/// </summary>
internal sealed class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionSecretProtector> _logger;

    /// <summary>
    /// Purpose string used when creating the scoped <see cref="IDataProtector"/>.
    /// Changing this value renders all previously encrypted secrets unreadable.
    /// </summary>
    private const string Purpose = "GodotGenerator.ApiKeys.v1";

    /// <summary>
    /// Initializes a new instance of <see cref="DataProtectionSecretProtector"/>.
    /// </summary>
    /// <param name="provider">Root Data Protection provider supplied by DI.</param>
    /// <param name="logger">Logger for cryptographic failure diagnostics.</param>
    public DataProtectionSecretProtector(
        IDataProtectionProvider provider,
        ILogger<DataProtectionSecretProtector> logger)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return _protector.Protect(plaintext);
    }

    /// <inheritdoc />
    public string? TryUnprotect(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(
                ex,
                "Data Protection decryption failed (possible key rotation, machine move, or corruption). " +
                "The secret must be re-entered.");
            return null;
        }
    }
}
