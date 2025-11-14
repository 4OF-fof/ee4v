using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace _4OF.ee4v.Core.Utility {
    public readonly struct Ulid : IComparable<Ulid>, IEquatable<Ulid>, IFormattable {
        private static readonly char[] Base32Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();
        private static readonly sbyte[] Base32DecodeMap = CreateDecodeMap();
        private static readonly Regex UlidRegex = new("^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.Compiled);

        private readonly ulong _most;
        private readonly ulong _least;

        private Ulid(ulong most, ulong least) {
            _most = most;
            _least = least;
        }

        public static Ulid Generate() {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var bytes = new byte[16];

            bytes[0] = (byte)((timestamp >> 40) & 0xFF);
            bytes[1] = (byte)((timestamp >> 32) & 0xFF);
            bytes[2] = (byte)((timestamp >> 24) & 0xFF);
            bytes[3] = (byte)((timestamp >> 16) & 0xFF);
            bytes[4] = (byte)((timestamp >> 8) & 0xFF);
            bytes[5] = (byte)(timestamp & 0xFF);

            RandomNumberGenerator.Fill(bytes.AsSpan(6, 10));

            return FromBytes(bytes);
        }

        public static Ulid Parse(string value) {
            return TryParse(value, out var ulid) ? ulid : throw new FormatException("Invalid ULID string.");
        }

        public static bool TryParse(string value, out Ulid ulid) {
            ulid = default;
            if (string.IsNullOrEmpty(value) || value.Length != 26) return false;
            if (!UlidRegex.IsMatch(value)) return false;

            if (!DecodeBase32(value, out var bytes)) return false;
            ulid = FromBytes(bytes);
            return true;
        }

        public static Ulid FromBytes(byte[] bytes) {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != 16) throw new ArgumentException("ULID must be 16 bytes.", nameof(bytes));

            ulong most = 0, least = 0;
            for (var i = 0; i < 8; i++) most = (most << 8) | bytes[i];
            for (var i = 8; i < 16; i++) least = (least << 8) | bytes[i];
            return new Ulid(most, least);
        }

        public byte[] ToByteArray() {
            var b = new byte[16];
            var v = _most;
            for (var i = 7; i >= 0; i--) { b[i] = (byte)(v & 0xFF); v >>= 8; }
            v = _least;
            for (var i = 15; i >= 8; i--) { b[i] = (byte)(v & 0xFF); v >>= 8; }
            return b;
        }

        public override string ToString() => ToString(null, null);

        public string ToString(string format, IFormatProvider formatProvider) {
            return EncodeBase32(ToByteArray());
        }

        public int CompareTo(Ulid other) {
            var cmp = _most.CompareTo(other._most);
            return cmp != 0 ? cmp : _least.CompareTo(other._least);
        }

        public bool Equals(Ulid other) => _most == other._most && _least == other._least;

        public override bool Equals(object obj) => obj is Ulid u && Equals(u);

        public override int GetHashCode() => HashCode.Combine(_most, _least);

        public static bool operator ==(Ulid left, Ulid right) => left.Equals(right);
        public static bool operator !=(Ulid left, Ulid right) => !left.Equals(right);

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

        private static bool DecodeBase32(string text, out byte[] bytes) {
            bytes = new byte[16];
            int buffer = 0, bitsLeft = 0, index = 0;

            foreach (var c in text) {
                var val = -1;
                if (c < 128) val = Base32DecodeMap[c];
                if (val < 0) return false;

                buffer = (buffer << 5) | val;
                bitsLeft += 5;

                if (bitsLeft >= 8) {
                    bitsLeft -= 8;
                    if (index >= 16) return false;
                    bytes[index++] = (byte)((buffer >> bitsLeft) & 0xFF);
                }
            }

            return index == 16;
        }

        private static sbyte[] CreateDecodeMap() {
            var map = new sbyte[128];
            for (var i = 0; i < map.Length; i++) map[i] = -1;
            for (var i = 0; i < Base32Alphabet.Length; i++) {
                var ch = Base32Alphabet[i];
                map[ch] = (sbyte)i;
            }
            for (var i = 0; i < Base32Alphabet.Length; i++) {
                var ch = char.ToLowerInvariant(Base32Alphabet[i]);
                map[ch] = (sbyte)i;
            }
            map['O'] = map['0'];
            map['o'] = map['0'];
            map['I'] = map['1'];
            map['i'] = map['1'];
            map['L'] = map['1'];
            map['l'] = map['1'];

            return map;
        }
    }
}
