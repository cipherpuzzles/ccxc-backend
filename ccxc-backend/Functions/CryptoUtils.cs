using Ccxc.Core.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.Functions
{
    public static class CryptoUtils
    {
        public static string GetLoginHash(string md5pass, string hashKey)
        {
            var passContent = $"{hashKey ?? ""}=={md5pass}.{Config.Config.Options.PassHashKey1}";
            var hashedPass = HashTools.HmacSha1Base64(passContent, Config.Config.Options.PassHashKey2);
            return hashedPass;
        }

        public static string GetRandomKey()
        {
            var random = new Random();
            var randomSeed = random.Next(0, int.MaxValue);
            var rk = HashTools.Md5Base64(randomSeed.ToString());
            return rk;
        }

        public static string GetAvatarHash(string email)
        {
            var avatarHash = HashTools.Md5Hex(email.Trim().ToLower());
            return avatarHash;
        }

        public static string AESEncrypt(string plainText, string iv)
        {
            var key = Config.Config.Options.AESMasterKey;
            var encrypted = AesEncrypt.Encrypt(plainText, key, iv);
            return encrypted;
        }

        public static string AESDecrypt(string encryptedText, string iv)
        {
            var key = Config.Config.Options.AESMasterKey;
            var decrypted = AesEncrypt.Decrypt(encryptedText, key, iv);
            return decrypted;
        }

        public static string GenRandomIV()
        {
            var ivSize = 16; // 128 bits
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var iv = new byte[ivSize];
            rng.GetBytes(iv);
            return Convert.ToBase64String(iv);
        }
    }
}
