using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PassManager.Security
{
    /// <summary>
    /// Handles master password hashing using PBKDF2-HMAC-SHA256
    /// </summary>
    public class PasswordHasher
    {
        private const int SaltSize = 16; // 128 bits
        private const int HashSize = 32; // 256 bits
        private const int Iterations = 100000;

        /// <summary>
        /// Hashes a password with a randomly generated salt
        /// </summary>
        public static string HashPassword(string password)
        {
            // Generate salt
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Hash password
            byte[] hash = HashPasswordWithSalt(password, salt);

            // Combine salt and hash
            byte[] hashBytes = new byte[SaltSize + HashSize];
            Buffer.BlockCopy(salt, 0, hashBytes, 0, SaltSize);
            Buffer.BlockCopy(hash, 0, hashBytes, SaltSize, HashSize);

            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Verifies a password against a hash using constant-time comparison
        /// </summary>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                byte[] hashBytes = Convert.FromBase64String(hashedPassword);

                if (hashBytes.Length != SaltSize + HashSize)
                    return false;

                // Extract salt
                byte[] salt = new byte[SaltSize];
                Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);

                // Extract stored hash
                byte[] storedHash = new byte[HashSize];
                Buffer.BlockCopy(hashBytes, SaltSize, storedHash, 0, HashSize);

                // Hash the input password with the stored salt
                byte[] computedHash = HashPasswordWithSalt(password, salt);

                // Constant-time comparison to prevent timing attacks
                return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Hashes password with given salt using PBKDF2
        /// </summary>
        private static byte[] HashPasswordWithSalt(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }

        /// <summary>
        /// Validates password strength - requires min 8 chars, uppercase, lowercase, digit, and symbol
        /// </summary>
        public static bool IsPasswordValid(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;

            bool hasLowercase = password.Any(char.IsLower);
            bool hasUppercase = password.Any(char.IsUpper);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSymbol = password.Any(c => !char.IsLetterOrDigit(c));

            return hasLowercase && hasUppercase && hasDigit && hasSymbol;
        }

        /// <summary>
        /// Generates a random secure password
        /// </summary>
        public static string GeneratePassword(int length = 16)
        {
            const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
            const string digitChars = "0123456789";
            const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            string allChars = upperChars + lowerChars + digitChars + specialChars;

            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] randomBytes = new byte[length];
                rng.GetBytes(randomBytes);

                var result = new StringBuilder(length);
                foreach (byte b in randomBytes)
                {
                    result.Append(allChars[b % allChars.Length]);
                }

                // Ensure at least one of each type
                string password = result.ToString();

                // Simple validation - if missing required types, regenerate
                if (!password.Any(c => upperChars.Contains(c)) ||
                    !password.Any(c => lowerChars.Contains(c)) ||
                    !password.Any(c => digitChars.Contains(c)) ||
                    !password.Any(c => specialChars.Contains(c)))
                {
                    // Try again recursively (very unlikely to need this)
                    return GeneratePassword(length);
                }

                return password;
            }
        }
    }
}
