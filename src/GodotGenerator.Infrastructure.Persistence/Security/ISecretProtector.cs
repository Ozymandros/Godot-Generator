#nullable enable

namespace GodotGenerator.Infrastructure.Persistence.Security;

/// <summary>
/// Provides symmetric protection (encrypt / decrypt) for secret values stored at rest.
/// </summary>
internal interface ISecretProtector
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> and returns the opaque ciphertext token.
    /// </summary>
    /// <param name="plaintext">Secret value to protect.</param>
    /// <returns>Encrypted, Base64-encoded ciphertext.</returns>
    string Protect(string plaintext);

    /// <summary>
    /// Attempts to decrypt <paramref name="ciphertext"/>.
    /// Returns <see langword="null"/> when decryption fails (key rotation, data corruption, or machine move).
    /// </summary>
    /// <param name="ciphertext">Encrypted token previously produced by <see cref="Protect"/>.</param>
    /// <returns>Plaintext on success; <see langword="null"/> on any cryptographic failure.</returns>
    string? TryUnprotect(string ciphertext);
}
