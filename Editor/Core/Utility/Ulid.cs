using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace _4OF.ee4v.Core.Utility {
    public static class Ulid {
        private static readonly char[] Base32Alphabet =
            "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

        private static readonly Regex UlidRegex =
            new("^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.Compiled);
        
        public static string Generate() {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var bytes = new byte[16];

            bytes[0] = (byte)((timestamp >> 40) & 0xFF);
            bytes[1] = (byte)((timestamp >> 32) & 0xFF);
            bytes[2] = (byte)((timestamp >> 24) & 0xFF);
            bytes[3] = (byte)((timestamp >> 16) & 0xFF);
            bytes[4] = (byte)((timestamp >> 8) & 0xFF);
            bytes[5] = (byte)(timestamp & 0xFF);

            RandomNumberGenerator.Fill(bytes.AsSpan(6, 10));

            return EncodeBase32(bytes);
        }

        public static bool IsValid(string value)
            => !string.IsNullOrEmpty(value) && UlidRegex.IsMatch(value);

        private static string EncodeBase32(byte[] data) {
            var sb = new StringBuilder(26);
            var index = 0;
            int buffer = data[0];
            var bitsLeft = 8;

            for (var i = 0; i < 26; i++) {
                if (bitsLeft < 5) {
                    index++;
                    if (index < data.Length) {
                        buffer = (buffer << 8) | data[index];
                        bitsLeft += 8;
                    } else {
                        buffer <<= (5 - bitsLeft);
                        bitsLeft = 5;
                    }
                }
                var val = (buffer >> (bitsLeft - 5)) & 0x1F;
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[val]);
            }

            return sb.ToString();
        }
    }
}
