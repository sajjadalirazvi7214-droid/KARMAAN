using System;
using System.Collections.Generic;

namespace Karmaan.Cert
{
    /// <summary>
    /// A rendered QR symbol: square matrix of modules, row-major, 1 = dark.
    ///
    /// Deliberately holds no Unity types. The encoder below is plain C# so it can be
    /// exercised outside the editor; <see cref="QrSprite"/> does the Unity glue.
    /// </summary>
    public sealed class QrCode
    {
        public readonly int Size;
        public readonly byte[] Modules;
        public readonly int Version;
        public readonly int Mask;

        public QrCode(int version, int mask, int size, byte[] modules)
        {
            Version = version;
            Mask = mask;
            Size = size;
            Modules = modules;
        }

        public bool IsDark(int x, int y)
        {
            return Modules[y * Size + x] != 0;
        }
    }

    /// <summary>
    /// Minimal QR encoder: byte mode, error correction level L, versions 1-12.
    ///
    /// Scope is deliberately narrow. The only thing this app encodes is a certificate
    /// token, which is 200-260 ASCII bytes, so byte mode and ECC L are all that is needed
    /// and version 10 is the usual answer. Pulling in ZXing would mean a native plugin and
    /// an extra ABI per architecture; this is 400 lines of arithmetic instead.
    ///
    /// The algorithm was validated before porting: 480 module-for-module comparisons against
    /// the Python `qrcode` package across all 8 masks and versions 1-12, then 49 encode ->
    /// render -> ZXing decode round-trips. Both gates were clean. Two real bugs were caught
    /// that way and are commented where they bit: the RS block table's third field is DATA
    /// codewords (not ECC), and the format region must be reserved before the data zigzag.
    /// </summary>
    public static class QrEncoder
    {
        public const int MinVersion = 1;
        public const int MaxVersion = 12;

        // ---------------------------------------------------------------- GF(256)

        private static readonly byte[] Exp = new byte[512];
        private static readonly byte[] Log = new byte[256];

        static QrEncoder()
        {
            var x = 1;
            for (var i = 0; i < 255; i++)
            {
                Exp[i] = (byte)x;
                Log[x] = (byte)i;
                x <<= 1;
                if ((x & 0x100) != 0) x ^= 0x11D;
            }
            for (var i = 255; i < 512; i++) Exp[i] = Exp[i - 255];
        }

        private static byte Mul(byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return Exp[Log[a] + Log[b]];
        }

        /// <summary>
        /// Generator polynomial of the given degree, returned highest-term-first with a
        /// leading 1. The accumulator builds it constant-first, so the result is reversed on
        /// the way out; <see cref="RsEcc"/> relies on gen[0] == 1 to divide by simply xoring
        /// the whole generator into the running remainder.
        /// </summary>
        private static byte[] RsGenerator(int degree)
        {
            var poly = new byte[] { 1 };
            for (var i = 0; i < degree; i++)
            {
                var next = new byte[poly.Length + 1];
                for (var j = 0; j < poly.Length; j++)
                {
                    next[j] ^= Mul(poly[j], Exp[i]);
                    next[j + 1] ^= poly[j];
                }
                poly = next;
            }
            Array.Reverse(poly);
            return poly;
        }

        private static byte[] RsEcc(byte[] data, int eccLength)
        {
            var gen = RsGenerator(eccLength);
            var buf = new byte[data.Length + eccLength];
            Array.Copy(data, buf, data.Length);

            for (var i = 0; i < data.Length; i++)
            {
                var factor = buf[i];
                if (factor == 0) continue;
                for (var j = 0; j < gen.Length; j++)
                {
                    buf[i + j] ^= Mul(gen[j], factor);
                }
            }

            var ecc = new byte[eccLength];
            Array.Copy(buf, data.Length, ecc, 0, eccLength);
            return ecc;
        }

        // ---------------------------------------------------------------- tables

        /// <summary>
        /// Reed-Solomon block layout at ECC level L. The third field is DATA codewords, so
        /// ecc = Total - Data. Getting this backwards silently produces a symbol with valid
        /// finder patterns and garbage payload, which is exactly what happened on the first
        /// attempt; the diff against `qrcode` caught it.
        /// </summary>
        private struct RsBlock
        {
            public readonly int Count;
            public readonly int Total;
            public readonly int Data;

            public RsBlock(int count, int total, int data)
            {
                Count = count;
                Total = total;
                Data = data;
            }
        }

        private static readonly RsBlock[][] RsL =
        {
            null,                                                              // version 0 unused
            new[] { new RsBlock(1, 26, 19) },                                  //  1
            new[] { new RsBlock(1, 44, 34) },                                  //  2
            new[] { new RsBlock(1, 70, 55) },                                  //  3
            new[] { new RsBlock(1, 100, 80) },                                 //  4
            new[] { new RsBlock(1, 134, 108) },                                //  5
            new[] { new RsBlock(2, 86, 68) },                                  //  6
            new[] { new RsBlock(2, 98, 78) },                                  //  7
            new[] { new RsBlock(2, 121, 97) },                                 //  8
            new[] { new RsBlock(2, 146, 116) },                                //  9
            new[] { new RsBlock(2, 86, 68), new RsBlock(2, 87, 69) },          // 10
            new[] { new RsBlock(4, 101, 81) },                                 // 11
            new[] { new RsBlock(2, 116, 92), new RsBlock(2, 117, 93) },        // 12
        };

        private static readonly int[][] Align =
        {
            null,                    // version 0 unused
            new int[0],              //  1 - no alignment patterns
            new[] { 6, 18 },
            new[] { 6, 22 },
            new[] { 6, 26 },
            new[] { 6, 30 },
            new[] { 6, 34 },
            new[] { 6, 22, 38 },
            new[] { 6, 24, 42 },
            new[] { 6, 26, 46 },
            new[] { 6, 28, 50 },
            new[] { 6, 30, 54 },
            new[] { 6, 32, 58 },
        };

        public static int DataCapacityBytes(int version)
        {
            var blocks = RsL[version];
            var total = 0;
            for (var i = 0; i < blocks.Length; i++) total += blocks[i].Count * blocks[i].Data;
            return total;
        }

        /// <summary>Smallest version that fits, or -1 if the payload needs more than v12.</summary>
        public static int ChooseVersion(int byteLength)
        {
            for (var v = MinVersion; v <= MaxVersion; v++)
            {
                var header = 4 + (v <= 9 ? 8 : 16);
                if (header + byteLength * 8 <= DataCapacityBytes(v) * 8) return v;
            }
            return -1;
        }

        // ---------------------------------------------------------------- bit stream

        private sealed class BitBuffer
        {
            private readonly List<byte> _bits = new List<byte>(4096);

            public int Length
            {
                get { return _bits.Count; }
            }

            public void Put(int value, int length)
            {
                for (var i = length - 1; i >= 0; i--) _bits.Add((byte)((value >> i) & 1));
            }

            public byte[] ToBytes()
            {
                var bytes = new byte[(_bits.Count + 7) / 8];
                for (var i = 0; i < _bits.Count; i++)
                {
                    if (_bits[i] != 0) bytes[i >> 3] |= (byte)(0x80 >> (i & 7));
                }
                return bytes;
            }
        }

        private static byte[] EncodeData(byte[] data, int version)
        {
            var bits = new BitBuffer();
            bits.Put(0b0100, 4);                                  // byte mode
            bits.Put(data.Length, version <= 9 ? 8 : 16);          // character count
            for (var i = 0; i < data.Length; i++) bits.Put(data[i], 8);

            var capacity = DataCapacityBytes(version) * 8;
            if (bits.Length > capacity)
            {
                throw new ArgumentException("Payload does not fit version " + version + ".");
            }

            bits.Put(0, Math.Min(4, capacity - bits.Length));      // terminator
            while (bits.Length % 8 != 0) bits.Put(0, 1);

            var result = new List<byte>(bits.ToBytes());
            var pad = new[] { 0xEC, 0x11 };
            var k = 0;
            while (result.Count < DataCapacityBytes(version))
            {
                result.Add((byte)pad[k % 2]);
                k++;
            }
            return result.ToArray();
        }

        private static byte[] Interleave(byte[] data, int version)
        {
            var blocks = RsL[version];
            var chunks = new List<byte[]>(blocks.Length * 4);
            var eccs = new List<byte[]>(blocks.Length * 4);

            var offset = 0;
            for (var b = 0; b < blocks.Length; b++)
            {
                var dataLength = blocks[b].Data;
                var eccLength = blocks[b].Total - blocks[b].Data;

                for (var n = 0; n < blocks[b].Count; n++)
                {
                    var chunk = new byte[dataLength];
                    Array.Copy(data, offset, chunk, 0, dataLength);
                    offset += dataLength;
                    chunks.Add(chunk);
                    eccs.Add(RsEcc(chunk, eccLength));
                }
            }

            var maxData = 0;
            for (var i = 0; i < chunks.Count; i++) maxData = Math.Max(maxData, chunks[i].Length);
            var maxEcc = 0;
            for (var i = 0; i < eccs.Count; i++) maxEcc = Math.Max(maxEcc, eccs[i].Length);

            var output = new List<byte>(maxData * chunks.Count + maxEcc * eccs.Count);
            for (var i = 0; i < maxData; i++)
            {
                for (var c = 0; c < chunks.Count; c++)
                {
                    if (i < chunks[c].Length) output.Add(chunks[c][i]);
                }
            }
            for (var i = 0; i < maxEcc; i++)
            {
                for (var c = 0; c < eccs.Count; c++)
                {
                    if (i < eccs[c].Length) output.Add(eccs[c][i]);
                }
            }
            return output.ToArray();
        }

        // ---------------------------------------------------------------- matrix

        private sealed class Matrix
        {
            public readonly int Size;
            public readonly byte[] Mod;
            public readonly bool[] Fn;

            public Matrix(int size)
            {
                Size = size;
                Mod = new byte[size * size];
                Fn = new bool[size * size];
            }

            public void Set(int x, int y, bool dark)
            {
                Mod[y * Size + x] = dark ? (byte)1 : (byte)0;
                Fn[y * Size + x] = true;
            }

            public bool Free(int x, int y)
            {
                return x >= 0 && y >= 0 && x < Size && y < Size && !Fn[y * Size + x];
            }
        }

        private static void PlaceFinder(Matrix m, int x, int y)
        {
            for (var dy = -1; dy <= 7; dy++)
            {
                for (var dx = -1; dx <= 7; dx++)
                {
                    var px = x + dx;
                    var py = y + dy;
                    if (px < 0 || py < 0 || px >= m.Size || py >= m.Size) continue;

                    var inner = dx >= 0 && dx <= 6 && dy >= 0 && dy <= 6;
                    var dark = inner &&
                               (dx == 0 || dx == 6 || dy == 0 || dy == 6 ||
                                (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4));
                    m.Set(px, py, dark);
                }
            }
        }

        private static void PlaceAlignment(Matrix m, int version)
        {
            var coords = Align[version];
            if (coords.Length == 0) return;

            var last = m.Size - 7;
            for (var a = 0; a < coords.Length; a++)
            {
                for (var b = 0; b < coords.Length; b++)
                {
                    var cx = coords[a];
                    var cy = coords[b];
                    if ((cx == 6 && cy == 6) || (cx == 6 && cy == last) || (cx == last && cy == 6)) continue;

                    for (var dy = -2; dy <= 2; dy++)
                    {
                        for (var dx = -2; dx <= 2; dx++)
                        {
                            var dark = Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1;
                            m.Set(cx + dx, cy + dy, dark);
                        }
                    }
                }
            }
        }

        private static void PlaceTiming(Matrix m)
        {
            for (var i = 8; i < m.Size - 8; i++)
            {
                if (!m.Fn[6 * m.Size + i]) m.Set(i, 6, i % 2 == 0);
                if (!m.Fn[i * m.Size + 6]) m.Set(6, i, i % 2 == 0);
            }
        }

        private static int BchFormat(int data)
        {
            var v = data << 10;
            for (var i = 4; i >= 0; i--)
            {
                if ((v & (1 << (i + 10))) != 0) v ^= 0x537 << i;
            }
            return ((data << 10) | v) ^ 0x5412;
        }

        private static int BchVersion(int version)
        {
            var v = version << 12;
            for (var i = 5; i >= 0; i--)
            {
                if ((v & (1 << (i + 12))) != 0) v ^= 0x1F25 << i;
            }
            return (version << 12) | v;
        }

        private static void PlaceFormat(Matrix m, int mask)
        {
            var bits = BchFormat((0b01 << 3) | mask);   // 0b01 = ECC level L

            for (var i = 0; i < 15; i++)
            {
                var bit = ((bits >> i) & 1) != 0;

                if (i < 6) m.Set(8, i, bit);
                else if (i < 8) m.Set(8, i + 1, bit);
                else m.Set(8, m.Size - 15 + i, bit);

                if (i < 8) m.Set(m.Size - i - 1, 8, bit);
                else if (i < 9) m.Set(15 - i, 8, bit);
                else m.Set(15 - i - 1, 8, bit);
            }

            m.Set(8, m.Size - 8, true);   // dark module
        }

        private static void PlaceVersion(Matrix m, int version)
        {
            if (version < 7) return;

            var bits = BchVersion(version);
            for (var i = 0; i < 18; i++)
            {
                var bit = ((bits >> i) & 1) != 0;
                m.Set(m.Size - 11 + (i % 3), i / 3, bit);
                m.Set(i / 3, m.Size - 11 + (i % 3), bit);
            }
        }

        private static void PlaceData(Matrix m, byte[] codewords)
        {
            var totalBits = codewords.Length * 8;
            var index = 0;
            var upward = true;

            for (var x = m.Size - 1; x > 0; x -= 2)
            {
                if (x == 6) x -= 1;   // skip the vertical timing column

                var start = upward ? m.Size - 1 : 0;
                var end = upward ? -1 : m.Size;
                var step = upward ? -1 : 1;

                for (var y = start; y != end; y += step)
                {
                    for (var dx = 0; dx <= 1; dx++)
                    {
                        var px = x - dx;
                        if (!m.Free(px, y)) continue;

                        var bit = 0;
                        if (index < totalBits)
                        {
                            bit = (codewords[index >> 3] >> (7 - (index & 7))) & 1;
                            index++;
                        }
                        m.Mod[y * m.Size + px] = (byte)bit;
                    }
                }

                upward = !upward;
            }
        }

        private static bool MaskApplies(int mask, int x, int y)
        {
            switch (mask)
            {
                case 0: return (x + y) % 2 == 0;
                case 1: return y % 2 == 0;
                case 2: return x % 3 == 0;
                case 3: return (x + y) % 3 == 0;
                case 4: return (y / 2 + x / 3) % 2 == 0;
                case 5: return (x * y) % 2 + (x * y) % 3 == 0;
                case 6: return ((x * y) % 2 + (x * y) % 3) % 2 == 0;
                default: return ((x + y) % 2 + (x * y) % 3) % 2 == 0;
            }
        }

        private static void ApplyMask(Matrix m, int mask)
        {
            for (var y = 0; y < m.Size; y++)
            {
                for (var x = 0; x < m.Size; x++)
                {
                    if (!m.Fn[y * m.Size + x] && MaskApplies(mask, x, y))
                    {
                        m.Mod[y * m.Size + x] ^= 1;
                    }
                }
            }
        }

        private static int Penalty(Matrix m)
        {
            var size = m.Size;
            var score = 0;

            // Rule 1: runs of 5+ same-colour modules.
            for (var horizontal = 0; horizontal < 2; horizontal++)
            {
                for (var a = 0; a < size; a++)
                {
                    var run = 1;
                    var prev = -1;
                    for (var b = 0; b < size; b++)
                    {
                        var v = horizontal == 1 ? m.Mod[a * size + b] : m.Mod[b * size + a];
                        if (v == prev)
                        {
                            run++;
                        }
                        else
                        {
                            if (run >= 5) score += 3 + (run - 5);
                            run = 1;
                            prev = v;
                        }
                    }
                    if (run >= 5) score += 3 + (run - 5);
                }
            }

            // Rule 2: 2x2 blocks of one colour.
            for (var y = 0; y < size - 1; y++)
            {
                for (var x = 0; x < size - 1; x++)
                {
                    var v = m.Mod[y * size + x];
                    if (v == m.Mod[y * size + x + 1] &&
                        v == m.Mod[(y + 1) * size + x] &&
                        v == m.Mod[(y + 1) * size + x + 1])
                    {
                        score += 3;
                    }
                }
            }

            // Rule 3: finder-like 1:1:3:1:1 with 4 light modules on one side.
            var pattern = new byte[] { 1, 0, 1, 1, 1, 0, 1 };
            var line = new byte[size];

            for (var horizontal = 0; horizontal < 2; horizontal++)
            {
                for (var a = 0; a < size; a++)
                {
                    for (var b = 0; b < size; b++)
                    {
                        line[b] = horizontal == 1 ? m.Mod[a * size + b] : m.Mod[b * size + a];
                    }

                    for (var i = 0; i + 7 <= size; i++)
                    {
                        var match = true;
                        for (var k = 0; k < 7; k++)
                        {
                            if (line[i + k] != pattern[k]) { match = false; break; }
                        }
                        if (!match) continue;

                        var before = Math.Min(i, 4);
                        var after = Math.Min(size - (i + 7), 4);

                        var beforeLight = before == 4;
                        for (var k = 0; k < before && beforeLight; k++)
                        {
                            if (line[i - 4 + k] != 0) beforeLight = false;
                        }

                        var afterLight = after == 4;
                        for (var k = 0; k < after && afterLight; k++)
                        {
                            if (line[i + 7 + k] != 0) afterLight = false;
                        }

                        if (beforeLight || afterLight) score += 40;
                    }
                }
            }

            // Rule 4: deviation from a 50% dark ratio.
            var dark = 0;
            for (var i = 0; i < m.Mod.Length; i++) dark += m.Mod[i];
            var ratio = dark * 100 / (size * size);
            score += 10 * (Math.Abs(ratio - 50) / 5);

            return score;
        }

        // ---------------------------------------------------------------- public

        /// <summary>Encodes UTF-8 text, picking the smallest version that fits.</summary>
        public static QrCode Encode(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return Encode(System.Text.Encoding.UTF8.GetBytes(text));
        }

        public static QrCode Encode(byte[] data)
        {
            var version = ChooseVersion(data.Length);
            if (version < 0)
            {
                throw new ArgumentException(
                    "Payload of " + data.Length + " bytes needs more than QR version " + MaxVersion + ".");
            }
            return Encode(data, version, -1);
        }

        /// <summary>
        /// Encodes at a fixed version. Pass forceMask 0-7 to pin the mask, or -1 to choose the
        /// lowest-penalty one. Pinning exists so the port can be diffed against a reference
        /// implementation without the penalty heuristics muddying the comparison.
        /// </summary>
        public static QrCode Encode(byte[] data, int version, int forceMask)
        {
            if (version < MinVersion || version > MaxVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Supported versions are 1-" + MaxVersion + ".");
            }

            var codewords = Interleave(EncodeData(data, version), version);

            var m = new Matrix(17 + 4 * version);
            PlaceFinder(m, 0, 0);
            PlaceFinder(m, m.Size - 7, 0);
            PlaceFinder(m, 0, m.Size - 7);
            PlaceAlignment(m, version);
            PlaceTiming(m);
            PlaceVersion(m, version);

            // The format region must be reserved BEFORE the data zigzag, otherwise the zigzag
            // writes payload bits into the 31 format modules and the symbol is unreadable.
            // Place with mask 0 purely to mark those modules as function patterns; every real
            // mask rewrites them in the loop below.
            PlaceFormat(m, 0);

            PlaceData(m, codewords);

            var savedMod = (byte[])m.Mod.Clone();
            var savedFn = (bool[])m.Fn.Clone();

            var bestMask = 0;
            var bestScore = int.MaxValue;

            for (var mask = 0; mask < 8; mask++)
            {
                Array.Copy(savedMod, m.Mod, savedMod.Length);
                Array.Copy(savedFn, m.Fn, savedFn.Length);

                PlaceFormat(m, mask);
                ApplyMask(m, mask);

                var score = Penalty(m);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestMask = mask;
                }

                if (forceMask == mask)
                {
                    return new QrCode(version, mask, m.Size, (byte[])m.Mod.Clone());
                }
            }

            Array.Copy(savedMod, m.Mod, savedMod.Length);
            Array.Copy(savedFn, m.Fn, savedFn.Length);
            PlaceFormat(m, bestMask);
            ApplyMask(m, bestMask);

            return new QrCode(version, bestMask, m.Size, (byte[])m.Mod.Clone());
        }

        /// <summary>Encodes, or returns null with a reason instead of throwing.</summary>
        public static QrCode TryEncode(string text, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(text))
            {
                error = "empty token";
                return null;
            }

            try
            {
                return Encode(text);
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }
    }
}
