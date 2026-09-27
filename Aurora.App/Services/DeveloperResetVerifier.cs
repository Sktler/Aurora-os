using System;
using System.Security.Cryptography;
using System.Text;

namespace Aurora.App.Services
{
    /// <summary>
    /// Verifies the developer-only factory-reset credential without storing the credential itself.
    /// The verifier is intentionally separate from user recovery/profile credentials.
    /// </summary>
    internal static class DeveloperResetVerifier
    {
        private const int Iterations = 600_000;
        private static readonly byte[] Salt = Convert.FromBase64String("7VqrKwTy0smmMD+GiHDaew==");
        private static readonly byte[] ExpectedHash = Convert.FromBase64String("UOlrCPeq6npBvS+paUnyly8RLosfqiRK7e0eOmydYAs=");

        public static bool Verify(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(candidate),
                Salt,
                Iterations,
                HashAlgorithmName.SHA256,
                ExpectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(hash, ExpectedHash);
        }
    }
}
