using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Ee4v.AssetManager.Infrastructure.Files
{
    internal static class UnityPackageGuidReader
    {
        private const int TarBlockSize = 512;
        private const int TarNameLength = 100;
        private const int TarSizeOffset = 124;
        private const int TarSizeLength = 12;
        private const int UnityGuidLength = 32;

        internal static IReadOnlyList<string> ReadGuids(
            string packagePath)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(packagePath) ||
                !File.Exists(packagePath))
            {
                return result;
            }

            try
            {
                using (var file = File.Open(
                           packagePath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite))
                using (var gzip = new GZipStream(
                           file,
                           CompressionMode.Decompress))
                {
                    ReadTarGuids(gzip, result);
                }
            }
            catch (InvalidDataException)
            {
                return Array.Empty<string>();
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (OverflowException)
            {
                return Array.Empty<string>();
            }

            return result;
        }

        private static void ReadTarGuids(
            Stream stream,
            ICollection<string> result)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var header = new byte[TarBlockSize];
            while (ReadBlock(stream, header))
            {
                if (IsEmptyBlock(header))
                {
                    break;
                }

                var entryName = ReadString(
                    header,
                    0,
                    TarNameLength);
                var separatorIndex = entryName.IndexOf('/');
                var candidate = separatorIndex >= 0
                    ? entryName.Substring(0, separatorIndex)
                    : entryName;
                if (IsUnityGuid(candidate))
                {
                    candidate = candidate.ToLowerInvariant();
                    if (seen.Add(candidate))
                    {
                        result.Add(candidate);
                    }
                }

                var size = ReadOctal(
                    header,
                    TarSizeOffset,
                    TarSizeLength);
                Skip(stream, RoundUpToTarBlock(size));
            }
        }

        private static bool ReadBlock(Stream stream, byte[] buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = stream.Read(
                    buffer,
                    offset,
                    buffer.Length - offset);
                if (read == 0)
                {
                    return offset == buffer.Length;
                }

                offset += read;
            }

            return true;
        }

        private static bool IsEmptyBlock(byte[] buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static string ReadString(
            byte[] buffer,
            int offset,
            int length)
        {
            var end = offset;
            var maximum = Math.Min(buffer.Length, offset + length);
            while (end < maximum && buffer[end] != 0)
            {
                end++;
            }

            return Encoding.ASCII.GetString(
                buffer,
                offset,
                end - offset);
        }

        private static long ReadOctal(
            byte[] buffer,
            int offset,
            int length)
        {
            var value = ReadString(buffer, offset, length)
                .Trim('\0', ' ');
            if (string.IsNullOrEmpty(value))
            {
                return 0L;
            }

            long result = 0L;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character < '0' || character > '7')
                {
                    throw new InvalidDataException(
                        "The UnityPackage contains an invalid TAR entry size.");
                }

                result = checked(result * 8L + character - '0');
            }

            return result;
        }

        private static long RoundUpToTarBlock(long value)
        {
            return value <= 0
                ? 0
                : ((value + TarBlockSize - 1) / TarBlockSize) *
                  TarBlockSize;
        }

        private static void Skip(Stream stream, long count)
        {
            var buffer = new byte[8192];
            while (count > 0)
            {
                var read = stream.Read(
                    buffer,
                    0,
                    (int)Math.Min(buffer.Length, count));
                if (read == 0)
                {
                    break;
                }

                count -= read;
            }
        }

        private static bool IsUnityGuid(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length != UnityGuidLength)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (!(character >= '0' && character <= '9') &&
                    !(character >= 'a' && character <= 'f') &&
                    !(character >= 'A' && character <= 'F'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
