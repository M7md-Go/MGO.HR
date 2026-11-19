using System;
using System.Security.Cryptography;

namespace HR.Helpers
{
    /// <summary>
    /// Password hashing per design decision D6 / rule 21:
    /// SHA-256 with a per-user random salt; plaintext is never stored.
    /// Format stored in Users: hex(SHA-256( utf8(salt_hex + password) )).
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>Generates a 32-byte random salt as 64 uppercase hex characters.</summary>
        public static string NewSalt()
        {
            byte[] salt = new byte[32];
            using (RNGCryptoServiceProvider generator = new RNGCryptoServiceProvider())
            {
                generator.GetBytes(salt);
            }
            return BytesToHex(salt);
        }

        /// <summary>Computes the hash of a password with the stored salt.</summary>
        public static string Hash(string password, string saltHex)
        {
            if (password == null)
            {
                throw new ArgumentNullException("password");
            }
            if (string.IsNullOrWhiteSpace(saltHex))
            {
                throw new ArgumentNullException("saltHex");
            }
            byte[] salt = HexToBytes(saltHex);
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            byte[] input = new byte[salt.Length + passwordBytes.Length];
            Array.Copy(salt, input, salt.Length);
            Array.Copy(passwordBytes, 0, input, salt.Length, passwordBytes.Length);

            using (SHA256 sha = SHA256.Create())
            {
                return BytesToHex(sha.ComputeHash(input));
            }
        }

        /// <summary>True when the password matches the stored hash (constant-time compare).</summary>
        public static bool Verify(string password, string saltHex, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                return false;
            }
            string computed = Hash(password, saltHex);
            return CompareConstantTime(computed, storedHash);
        }

        private static bool CompareConstantTime(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }
            int diff = 0;
            for (int index = 0; index < left.Length; index++)
            {
                diff |= left[index] ^ right[index];
            }
            return diff == 0;
        }

        private static string BytesToHex(byte[] bytes)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                text.Append(value.ToString("X2"));
            }
            return text.ToString();
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
            }
            return bytes;
        }
    }
}
