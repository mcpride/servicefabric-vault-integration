using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

#pragma warning disable SCS0006 // Weak hashing function
namespace VaultService.Core.Extensions
{
    public static class StreamExtensionsMethods
    {
        // ReSharper disable once InconsistentNaming
        public static string GenerateMD5CheckSum(this Stream stream)
        {
            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                var hash = md5.ComputeHash(stream);
                var contentMd5 = BitConverter.ToString(hash).Replace("-", "");
                return contentMd5;
            }
        }

        // ReSharper disable once InconsistentNaming
        public static string GenerateMD5CheckSum(this byte[] buffer)
        {
            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                var hash = md5.ComputeHash(buffer);
                var contentMd5 = BitConverter.ToString(hash).Replace("-", "");
                return contentMd5;
            }
        }

        public static byte[] ReadAllBytes(this Stream stream)
        {
            var buffer = new byte[32768];
            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    var read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }

        public static async Task<byte[]> ReadAllBytesAsync(this Stream stream)
        {
            var buffer = new byte[32768];
            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read <= 0) return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }
    }
}
#pragma warning restore SCS0006 // Weak hashing function
