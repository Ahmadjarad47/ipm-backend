using System.Security.Cryptography;
using System.Text;
using Ipm.Application.Abstractions.Security;
using Ipm.Infrastructure.Git;
using Microsoft.Extensions.Options;

namespace Ipm.Infrastructure.Security;

/// <summary>AES-256-GCM with a fresh nonce per secret: the stored text is base64 of <c>nonce(12) + tag(16) + ciphertext</c>.</summary>
internal sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesGcmSecretProtector(IOptions<GitOptions> options)
    {
        try
        {
            _key = Convert.FromBase64String(options.Value.SecretKey);
        }
        catch (FormatException e)
        {
            throw new InvalidOperationException("Git:SecretKey must be base64 of 32 bytes.", e);
        }

        if (_key.Length != 32)
        {
            throw new InvalidOperationException("Git:SecretKey must be base64 of 32 bytes.");
        }
    }

    public string Protect(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string Unprotect(string cipherText)
    {
        var data = Convert.FromBase64String(cipherText);
        var nonce = data[..NonceSize];
        var tag = data[NonceSize..(NonceSize + TagSize)];
        var cipher = data[(NonceSize + TagSize)..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
