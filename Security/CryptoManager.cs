using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PassManager.Security
{
    /// <summary>
    /// Handles encryption/decryption using AES-256 (equivalent to Python's Fernet)
    /// </summary>
    public class CryptoManager
    {
        private byte[] _key;

        public CryptoManager(byte[] key)
        {
            if (key.Length != 32)
                throw new ArgumentException("Key must be 32 bytes for AES-256");
            _key = key;
        }

        /// <summary>
        /// Encrypts data using AES-256-CBC with HMAC-SHA256 authentication
        /// </summary>
        public byte[] Encrypt(string plaintext)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                byte[] iv = aes.IV;
                byte[] encrypted;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                    cs.Write(plaintextBytes, 0, plaintextBytes.Length);
                    cs.FlushFinalBlock();
                    encrypted = ms.ToArray();
                }

                // Compute HMAC
                using (var hmac = new HMACSHA256(_key))
                {
                    byte[] dataToAuth = new byte[iv.Length + encrypted.Length];
                    Buffer.BlockCopy(iv, 0, dataToAuth, 0, iv.Length);
                    Buffer.BlockCopy(encrypted, 0, dataToAuth, iv.Length, encrypted.Length);
                    byte[] hash = hmac.ComputeHash(dataToAuth);

                    // Result: [hash][iv][encrypted]
                    byte[] result = new byte[hash.Length + iv.Length + encrypted.Length];
                    Buffer.BlockCopy(hash, 0, result, 0, hash.Length);
                    Buffer.BlockCopy(iv, 0, result, hash.Length, iv.Length);
                    Buffer.BlockCopy(encrypted, 0, result, hash.Length + iv.Length, encrypted.Length);

                    return result;
                }
            }
        }

        /// <summary>
        /// Decrypts data encrypted with Encrypt method
        /// </summary>
        public string Decrypt(byte[] ciphertext)
        {
            if (ciphertext.Length < 48) // 32 (hash) + 16 (iv minimum)
                throw new ArgumentException("Invalid ciphertext");

            // Extract hash, iv, and encrypted data
            byte[] hash = new byte[32];
            byte[] iv = new byte[16];
            byte[] encrypted = new byte[ciphertext.Length - 48];

            Buffer.BlockCopy(ciphertext, 0, hash, 0, 32);
            Buffer.BlockCopy(ciphertext, 32, iv, 0, 16);
            Buffer.BlockCopy(ciphertext, 48, encrypted, 0, encrypted.Length);

            // Verify HMAC
            using (var hmac = new HMACSHA256(_key))
            {
                byte[] dataToAuth = new byte[iv.Length + encrypted.Length];
                Buffer.BlockCopy(iv, 0, dataToAuth, 0, iv.Length);
                Buffer.BlockCopy(encrypted, 0, dataToAuth, iv.Length, encrypted.Length);
                byte[] computedHash = hmac.ComputeHash(dataToAuth);

                if (!CryptographicOperations.FixedTimeEquals(hash, computedHash))
                    throw new CryptographicException("Authentication failed - data may be tampered");
            }

            // Decrypt
            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(encrypted))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Generates a new encryption key (32 bytes for AES-256)
        /// </summary>
        public static byte[] GenerateKey()
        {
            byte[] key = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }
            return key;
        }

        /// <summary>
        /// Encrypts data and returns base64 string
        /// </summary>
        public string EncryptToBase64(string plaintext)
        {
            return Convert.ToBase64String(Encrypt(plaintext));
        }

        /// <summary>
        /// Decrypts base64 string
        /// </summary>
        public string DecryptFromBase64(string base64Ciphertext)
        {
            return Decrypt(Convert.FromBase64String(base64Ciphertext));
        }
    }
}
