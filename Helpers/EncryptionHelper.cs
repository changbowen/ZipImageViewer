using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace ZipImageViewer
{
    public static class EncryptionHelper
    {
        //based on: https://stackoverflow.com/a/10177020/3652073
        internal const string CipherHeader = @"🔒";

        // This constant is used to determine the keysize of the encryption algorithm in bits.
        // We divide this by 8 within the code below to get the equivalent number of bytes.
        private const int Keysize = 256;

        public class Password
        {
            internal string EncryptedRaw;
            /// <summary>
            /// The encrypted password containing the <see cref="CipherHeader"/>.
            /// </summary>
            public string Encrypted => CipherHeader + EncryptedRaw;

            private string hash;
            public string Hash {
                get {
                    if (hash == null) hash = GetHash(Decrypt());
                    return hash;
                }
                private set => hash = value;
            }

            public string Decrypt() => EncryptionHelper.Decrypt(EncryptedRaw);

            /// <summary>
            /// <paramref name="text"/> can be encrypted or unencrypted password;
            /// </summary>
            public Password(string text) {
                if (text == null) return;
                if (!text.StartsWith(CipherHeader)) { //not encrypted
                    Hash = GetHash(text);
                    EncryptedRaw = Encrypt(text);
                }
                else
                    EncryptedRaw = text.Remove(0, CipherHeader.Length);
            }
        }

        //public static bool IsEncrypted(string text) {
        //    return text.StartsWith(CipherHeader);
        //}

        public static string GetHash(string text) {
            byte[] hashBytes;
            using (var sha256 = SHA256.Create()) {
                hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
            }
            var sb = new StringBuilder();
            foreach (byte b in hashBytes) sb.Append(b.ToString(@"X2"));
            return sb.ToString();
        }

        /// <summary>
        /// <para>Encrypt the text if it is not identified as already encrypted. Otherwise return the original value.</para>
        /// <para>The returned value does not include <see cref="CipherHeader"/></para>
        /// </summary>
        public static string TryEncrypt(string text, string passPhrase = null) {
            if (text == null) return null;
            if (text.StartsWith(CipherHeader)) return text.Remove(0, CipherHeader.Length);

            return Encrypt(text, passPhrase);
        }

        /// <summary>
        /// Decrypt the text if it is identified as encrypted. Otherwise return the original value.
        /// </summary>
        public static string TryDecrypt(string text, string passPhrase = null) {
            if (text == null) return null;
            if (!text.StartsWith(CipherHeader)) return text;
            
            text = text.Remove(0, CipherHeader.Length);
            return Decrypt(text, passPhrase);
        }


        public static string Encrypt(string plainText, string passPhrase = null) {
            if (plainText == null) return null;
            if (passPhrase == null) passPhrase = Setting.MasterPassword;

            // Salt and IV is randomly generated each time, but is preprended to encrypted cipher text
            // so that the same Salt and IV values can be used when decrypting.  
            var saltStringBytes = Generate256BitsOfRandomEntropy();
            var ivStringBytes = Generate256BitsOfRandomEntropy();
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes)) {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = new RijndaelManaged()) {
                    symmetricKey.BlockSize = 256;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var encryptor = symmetricKey.CreateEncryptor(keyBytes, ivStringBytes)) {
                        using (var memoryStream = new MemoryStream()) {
                            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write)) {
                                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                                cryptoStream.FlushFinalBlock();
                                // Create the final bytes as a concatenation of the random salt bytes, the random iv bytes and the cipher bytes.
                                var cipherTextBytes = saltStringBytes;
                                cipherTextBytes = cipherTextBytes.Concat(ivStringBytes).ToArray();
                                cipherTextBytes = cipherTextBytes.Concat(memoryStream.ToArray()).ToArray();
                                memoryStream.Close();
                                cryptoStream.Close();
                                return Convert.ToBase64String(cipherTextBytes);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns null when the decryption fails.
        /// </summary>
        public static string Decrypt(string cipherText, string passPhrase = null) {
            if (cipherText == null) return null;
            if (passPhrase == null) passPhrase = Setting.MasterPassword;

            try {
                // Get the complete stream of bytes that represent:
                // [32 bytes of Salt] + [32 bytes of IV] + [n bytes of CipherText]
                var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
                // Get the saltbytes by extracting the first 32 bytes from the supplied cipherText bytes.
                var saltStringBytes = cipherTextBytesWithSaltAndIv.Take(Keysize / 8).ToArray();
                // Get the IV bytes by extracting the next 32 bytes from the supplied cipherText bytes.
                var ivStringBytes = cipherTextBytesWithSaltAndIv.Skip(Keysize / 8).Take(Keysize / 8).ToArray();
                // Get the actual cipher text bytes by removing the first 64 bytes from the cipherText string.
                var cipherTextBytes = cipherTextBytesWithSaltAndIv.Skip((Keysize / 8) * 2).Take(cipherTextBytesWithSaltAndIv.Length - ((Keysize / 8) * 2)).ToArray();

                using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes)) {
                    var keyBytes = password.GetBytes(Keysize / 8);
                    using (var symmetricKey = new RijndaelManaged()) {
                        symmetricKey.BlockSize = 256;
                        symmetricKey.Mode = CipherMode.CBC;
                        symmetricKey.Padding = PaddingMode.PKCS7;
                        using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, ivStringBytes)) {
                            using (var memoryStream = new MemoryStream(cipherTextBytes)) {
                                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read)) {
                                    var plainTextBytes = new byte[cipherTextBytes.Length];
                                    var decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                                    memoryStream.Close();
                                    cryptoStream.Close();
                                    return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception) {
                return null;
            }
        }

        private static byte[] Generate256BitsOfRandomEntropy() {
            var randomBytes = new byte[32]; // 32 Bytes will give us 256 bits.
            using (var rngCsp = new RNGCryptoServiceProvider()) {
                // Fill the array with cryptographically secure random bytes.
                rngCsp.GetBytes(randomBytes);
            }
            return randomBytes;
        }

    }
}
