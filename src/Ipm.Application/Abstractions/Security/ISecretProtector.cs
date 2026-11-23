namespace Ipm.Application.Abstractions.Security;

/// <summary>
/// Encrypts secrets that must be read back (Git access tokens, webhook secrets) so they are never stored as plain text (R15 of part 07).
/// The key comes from the configuration, not from the machine, so a restarted container still reads what it stored.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plainText);

    string Unprotect(string cipherText);
}
