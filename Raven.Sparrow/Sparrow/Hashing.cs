﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Sparrow
{
    public unsafe static partial class Hashing
    {
        public struct XXHash32Values
        {
            public uint V1;
            public uint V2;
            public uint V3;
            public uint V4;
        }

        public struct XXHash64Values
        {
            public ulong V1;
            public ulong V2;
            public ulong V3;
            public ulong V4;
        }

        internal static class XXHash32Constants
        {
            internal static uint PRIME32_1 = 2654435761U;
            internal static uint PRIME32_2 = 2246822519U;
            internal static uint PRIME32_3 = 3266489917U;
            internal static uint PRIME32_4 = 668265263U;
            internal static uint PRIME32_5 = 374761393U;
        }

        internal static class XXHash64Constants
        {
            internal static ulong PRIME64_1 = 11400714785074694791UL;
            internal static ulong PRIME64_2 = 14029467366897019727UL;
            internal static ulong PRIME64_3 = 1609587929392839161UL;
            internal static ulong PRIME64_4 = 9650029242287828579UL;
            internal static ulong PRIME64_5 = 2870177450012600261UL;
        }

        internal static class XXHashHelpers
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static uint RotateLeft32(uint value, int count)
            {
                return (value << count) | (value >> (32 - count));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static ulong RotateLeft64(ulong value, int count)
            {
                return (value << count) | (value >> (64 - count));
            }
        }

        /// <summary>
        /// A port of the original XXHash algorithm from Google in 32bits 
        /// </summary>
        /// <<remarks>The 32bits and 64bits hashes for the same data are different. In short those are 2 entirely different algorithms</remarks>
        public static class XXHash32
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe uint CalculateInline(byte* buffer, int len, uint seed = 0)
            {
                unchecked
                {
                    uint h32;

                    byte* bEnd = buffer + len;

                    if (len >= 16)
                    {
                        byte* limit = bEnd - 16;

                        uint v1 = seed + XXHash32Constants.PRIME32_1 + XXHash32Constants.PRIME32_2;
                        uint v2 = seed + XXHash32Constants.PRIME32_2;
                        uint v3 = seed + 0;
                        uint v4 = seed - XXHash32Constants.PRIME32_1;

                        do
                        {
                            v1 += *((uint*)buffer) * XXHash32Constants.PRIME32_2;
                            buffer += sizeof(uint);
                            v2 += *((uint*)buffer) * XXHash32Constants.PRIME32_2;
                            buffer += sizeof(uint);
                            v3 += *((uint*)buffer) * XXHash32Constants.PRIME32_2;
                            buffer += sizeof(uint);
                            v4 += *((uint*)buffer) * XXHash32Constants.PRIME32_2;
                            buffer += sizeof(uint);

                            v1 = XXHashHelpers.RotateLeft32(v1, 13);
                            v2 = XXHashHelpers.RotateLeft32(v2, 13);
                            v3 = XXHashHelpers.RotateLeft32(v3, 13);
                            v4 = XXHashHelpers.RotateLeft32(v4, 13);

                            v1 *= XXHash32Constants.PRIME32_1;
                            v2 *= XXHash32Constants.PRIME32_1;
                            v3 *= XXHash32Constants.PRIME32_1;
                            v4 *= XXHash32Constants.PRIME32_1;
                        }
                        while (buffer <= limit);

                        h32 = XXHashHelpers.RotateLeft32(v1, 1) + XXHashHelpers.RotateLeft32(v2, 7) + XXHashHelpers.RotateLeft32(v3, 12) + XXHashHelpers.RotateLeft32(v4, 18);
                    }
                    else
                    {
                        h32 = seed + XXHash32Constants.PRIME32_5;
                    }

                    h32 += (uint)len;


                    while (buffer + 4 <= bEnd)
                    {
                        h32 += *((uint*)buffer) * XXHash32Constants.PRIME32_3;
                        h32 = XXHashHelpers.RotateLeft32(h32, 17) * XXHash32Constants.PRIME32_4;
                        buffer += 4;
                    }

                    while (buffer < bEnd)
                    {
                        h32 += (uint)(*buffer) * XXHash32Constants.PRIME32_5;
                        h32 = XXHashHelpers.RotateLeft32(h32, 11) * XXHash32Constants.PRIME32_1;
                        buffer++;
                    }

                    h32 ^= h32 >> 15;
                    h32 *= XXHash32Constants.PRIME32_2;
                    h32 ^= h32 >> 13;
                    h32 *= XXHash32Constants.PRIME32_3;
                    h32 ^= h32 >> 16;

                    return h32;
                }
            }

            public static unsafe uint Calculate(byte* buffer, int len, uint seed = 0)
            {
                return CalculateInline(buffer, len, seed);
            }

            public static uint Calculate(string value, Encoding encoder, uint seed = 0)
            {
                var buf = encoder.GetBytes(value);

                fixed (byte* buffer = buf)
                {
                    return CalculateInline(buffer, buf.Length, seed);
                }
            }
            public static uint CalculateRaw(string buf, uint seed = 0)
            {
                fixed (char* buffer = buf)
                {
                    return CalculateInline((byte*)buffer, buf.Length * sizeof(char), seed);
                }
            }

            public static uint Calculate(byte[] buf, int len = -1, uint seed = 0)
            {
                if (len == -1)
                    len = buf.Length;

                fixed (byte* buffer = buf)
                {
                    return CalculateInline(buffer, len, seed);
                }
            }

            public static uint Calculate(int[] buf, int len = -1, uint seed = 0)
            {
                if (len == -1)
                    len = buf.Length;

                fixed (int* buffer = buf)
                {
                    return Calculate((byte*)buffer, len * sizeof(int), seed);
                }
            }
        }

        /// <summary>
        /// A port of the original XXHash algorithm from Google in 64bits 
        /// </summary>
        /// <<remarks>The 32bits and 64bits hashes for the same data are different. In short those are 2 entirely different algorithms</remarks>
        public static class XXHash64
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe ulong CalculateInline(byte* buffer, int len, ulong seed = 0)
            {
                ulong h64;

                byte* bEnd = buffer + len;

                if (len >= 32)
                {
                    byte* limit = bEnd - 32;

                    ulong v1 = seed + XXHash64Constants.PRIME64_1 + XXHash64Constants.PRIME64_2;
                    ulong v2 = seed + XXHash64Constants.PRIME64_2;
                    ulong v3 = seed + 0;
                    ulong v4 = seed - XXHash64Constants.PRIME64_1;

                    do
                    {
                        v1 += *((ulong*)buffer) * XXHash64Constants.PRIME64_2;
                        buffer += sizeof(ulong);
                        v2 += *((ulong*)buffer) * XXHash64Constants.PRIME64_2;
                        buffer += sizeof(ulong);
                        v3 += *((ulong*)buffer) * XXHash64Constants.PRIME64_2;
                        buffer += sizeof(ulong);
                        v4 += *((ulong*)buffer) * XXHash64Constants.PRIME64_2;
                        buffer += sizeof(ulong);

                        v1 = XXHashHelpers.RotateLeft64(v1, 31);
                        v2 = XXHashHelpers.RotateLeft64(v2, 31);
                        v3 = XXHashHelpers.RotateLeft64(v3, 31);
                        v4 = XXHashHelpers.RotateLeft64(v4, 31);

                        v1 *= XXHash64Constants.PRIME64_1;
                        v2 *= XXHash64Constants.PRIME64_1;
                        v3 *= XXHash64Constants.PRIME64_1;
                        v4 *= XXHash64Constants.PRIME64_1;
                    }
                    while (buffer <= limit);

                    h64 = XXHashHelpers.RotateLeft64(v1, 1) + XXHashHelpers.RotateLeft64(v2, 7) + XXHashHelpers.RotateLeft64(v3, 12) + XXHashHelpers.RotateLeft64(v4, 18);

                    v1 *= XXHash64Constants.PRIME64_2;
                    v1 = XXHashHelpers.RotateLeft64(v1, 31);
                    v1 *= XXHash64Constants.PRIME64_1;
                    h64 ^= v1;
                    h64 = h64 * XXHash64Constants.PRIME64_1 + XXHash64Constants.PRIME64_4;

                    v2 *= XXHash64Constants.PRIME64_2;
                    v2 = XXHashHelpers.RotateLeft64(v2, 31);
                    v2 *= XXHash64Constants.PRIME64_1;
                    h64 ^= v2;
                    h64 = h64 * XXHash64Constants.PRIME64_1 + XXHash64Constants.PRIME64_4;

                    v3 *= XXHash64Constants.PRIME64_2;
                    v3 = XXHashHelpers.RotateLeft64(v3, 31);
                    v3 *= XXHash64Constants.PRIME64_1;
                    h64 ^= v3;
                    h64 = h64 * XXHash64Constants.PRIME64_1 + XXHash64Constants.PRIME64_4;

                    v4 *= XXHash64Constants.PRIME64_2;
                    v4 = XXHashHelpers.RotateLeft64(v4, 31);
                    v4 *= XXHash64Constants.PRIME64_1;
                    h64 ^= v4;
                    h64 = h64 * XXHash64Constants.PRIME64_1 + XXHash64Constants.PRIME64_4;
                }
                else
                {
                    h64 = seed + XXHash64Constants.PRIME64_5;
                }

                h64 += (ulong)len;


                while (buffer + 8 <= bEnd)
                {
                    ulong k1 = *((ulong*)buffer);
                    k1 *= XXHash64Constants.PRIME64_2;
                    k1 = XXHashHelpers.RotateLeft64(k1, 31);
                    k1 *= XXHash64Constants.PRIME64_1;
                    h64 ^= k1;
                    h64 = XXHashHelpers.RotateLeft64(h64, 27) * XXHash64Constants.PRIME64_1 + XXHash64Constants.PRIME64_4;
                    buffer += 8;
                }

                if (buffer + 4 <= bEnd)
                {
                    h64 ^= *(uint*)buffer * XXHash64Constants.PRIME64_1;
                    h64 = XXHashHelpers.RotateLeft64(h64, 23) * XXHash64Constants.PRIME64_2 + XXHash64Constants.PRIME64_3;
                    buffer += 4;
                }

                while (buffer < bEnd)
                {
                    h64 ^= ((ulong)*buffer) * XXHash64Constants.PRIME64_5;
                    h64 = XXHashHelpers.RotateLeft64(h64, 11) * XXHash64Constants.PRIME64_1;
                    buffer++;
                }

                h64 ^= h64 >> 33;
                h64 *= XXHash64Constants.PRIME64_2;
                h64 ^= h64 >> 29;
                h64 *= XXHash64Constants.PRIME64_3;
                h64 ^= h64 >> 32;

                return h64;
            }

            public static unsafe ulong Calculate(byte* buffer, int len, ulong seed = 0)
            {
                return CalculateInline(buffer, len, seed);
            }

            public static ulong Calculate(string value, Encoding encoder, ulong seed = 0)
            {
                var buf = encoder.GetBytes(value);

                fixed (byte* buffer = buf)
                {
                    return CalculateInline(buffer, buf.Length, seed);
                }
            }
            public static ulong CalculateRaw(string buf, ulong seed = 0)
            {
                fixed (char* buffer = buf)
                {
                    return CalculateInline((byte*)buffer, buf.Length * sizeof(char), seed);
                }
            }

            public static ulong Calculate(byte[] buf, int len = -1, ulong seed = 0)
            {
                if (len == -1)
                    len = buf.Length;

                fixed (byte* buffer = buf)
                {
                    return CalculateInline(buffer, len, seed);
                }
            }

            public static ulong Calculate(int[] buf, int len = -1, ulong seed = 0)
            {
                if (len == -1)
                    len = buf.Length;

                fixed (int* buffer = buf)
                {
                    return CalculateInline((byte*)buffer, len * sizeof(int), seed);
                }
            }


        }

        public static int Combine(int x, int y)
        {
            return CombineInline(x, y);
        }

        public static uint Combine(uint x, uint y)
        {
            return CombineInline(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CombineInline(int x, int y)
        {
            ulong ex = (ulong)x;
            ulong ey = (ulong)y;

            ulong key = ex << 32 | ey;

            key = (~key) + (key << 18); // key = (key << 18) - key - 1;
            key = key ^ (key >> 31);
            key = key * 21; // key = (key + (key << 2)) + (key << 4);
            key = key ^ (key >> 11);
            key = key + (key << 6);
            key = key ^ (key >> 22);

            return (int)key;
        }

        private static readonly ulong kMul = 0x9ddfea08eb382d69UL;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CombineInline(ulong x, ulong y)
        {
            // This is the Hash128to64 function from Google's CityHash (available
            // under the MIT License).  We use it to reduce multiple 64 bit hashes
            // into a single hash.

            // Murmur-inspired hashing.
            ulong a = (y ^ x) * kMul;
            a ^= (a >> 47);
            ulong b = (x ^ a) * kMul;
            b ^= (b >> 47);
            b *= kMul;

            return b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CombineInline(long upper, long lower)
        {
            // This is the Hash128to64 function from Google's CityHash (available
            // under the MIT License).  We use it to reduce multiple 64 bit hashes
            // into a single hash.

            ulong x = (ulong)upper;
            ulong y = (ulong)lower;

            // Murmur-inspired hashing.
            ulong a = (y ^ x) * kMul;
            a ^= (a >> 47);
            ulong b = (x ^ a) * kMul;
            b ^= (b >> 47);
            b *= kMul;

            return b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CombineInline(uint x, uint y)
        {
            ulong ex = (ulong)x;
            ulong ey = (ulong)y;

            ulong key = ex << 32 | ey;

            key = (~key) + (key << 18); // key = (key << 18) - key - 1;
            key = key ^ (key >> 31);
            key = key * 21; // key = (key + (key << 2)) + (key << 4);
            key = key ^ (key >> 11);
            key = key + (key << 6);
            key = key ^ (key >> 22);

            return (uint)key;
        }

    }
}
