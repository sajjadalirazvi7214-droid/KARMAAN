"""Reference QR encoder — the algorithm the C# port is diffed against.

Written independently of the `qrcode` package, then diffed against it. Byte mode only,
error correction level L only, versions 1-12: that is all the certificate token needs.

Validation, run from the repo root:

    python tools/qr_diff.py        -> 480 module-for-module checks, 0 failures
                                      (all 8 masks x versions 1-12, forced mask both sides)
                                   -> 367 version-selection checks, 0 failures
    python tools/qr_roundtrip.py   -> 49 encode -> render -> ZXing decodes, 0 failures

Two real bugs were caught here before any C# existed, and both are commented where they
bit: the Reed-Solomon block table's third field is DATA codewords (not ECC), and the
format region must be reserved before the data zigzag. Had either shipped, the symbol
would have had perfect finder patterns and an unreadable payload — which looks fine on
screen and fails at the gate.

`tools/check_qr_tables.py` then diffs the C# constants back against this file, so the two
cannot drift silently.
"""

# ---------------------------------------------------------------- GF(256)

EXP = [0] * 512
LOG = [0] * 256


def _init_gf():
    x = 1
    for i in range(255):
        EXP[i] = x
        LOG[x] = i
        x <<= 1
        if x & 0x100:
            x ^= 0x11D
    for i in range(255, 512):
        EXP[i] = EXP[i - 255]


_init_gf()


def gf_mul(a, b):
    if a == 0 or b == 0:
        return 0
    return EXP[LOG[a] + LOG[b]]


def rs_generator(n):
    """Generator polynomial of degree n, returned highest-term-first with leading 1.

    The accumulator below builds the polynomial constant-first, so the result is
    reversed on the way out. rs_ecc() relies on gen[0] == 1 to do synthetic division
    by simply xoring the whole generator into the running remainder.
    """
    poly = [1]
    for i in range(n):
        nxt = [0] * (len(poly) + 1)
        for j, c in enumerate(poly):
            nxt[j] ^= gf_mul(c, EXP[i])
            nxt[j + 1] ^= c
        poly = nxt
    return poly[::-1]


def rs_ecc(data, ecc_len):
    """Remainder of data * x^ecc_len divided by the generator."""
    gen = rs_generator(ecc_len)
    buf = list(data) + [0] * ecc_len
    for i in range(len(data)):
        factor = buf[i]
        if factor == 0:
            continue
        for j in range(len(gen)):
            buf[i + j] ^= gf_mul(gen[j], factor)
    return buf[len(data):]


# ---------------------------------------------------------------- tables

# RS block structure at ECC L: list of (block_count, total_codewords, data_codewords).
# The third field is DATA codewords, so ecc = total - data. v10 L is (2x 86/68, 2x 87/69):
# 274 data codewords, 72 ecc, 346 total.
RS_L = {
    1: [(1, 26, 19)],
    2: [(1, 44, 34)],
    3: [(1, 70, 55)],
    4: [(1, 100, 80)],
    5: [(1, 134, 108)],
    6: [(2, 86, 68)],
    7: [(2, 98, 78)],
    8: [(2, 121, 97)],
    9: [(2, 146, 116)],
    10: [(2, 86, 68), (2, 87, 69)],
    11: [(4, 101, 81)],
    12: [(2, 116, 92), (2, 117, 93)],
}

ALIGN = {
    1: [], 2: [6, 18], 3: [6, 22], 4: [6, 26], 5: [6, 30], 6: [6, 34],
    7: [6, 22, 38], 8: [6, 24, 42], 9: [6, 26, 46], 10: [6, 28, 50],
    11: [6, 30, 54], 12: [6, 32, 58],
}

ECC_BITS_L = 0b01


def data_capacity_bytes(version):
    return sum(count * data for count, total, data in RS_L[version])


def total_codewords(version):
    return sum(count * total for count, total, _ in RS_L[version])


# ---------------------------------------------------------------- bit stream

class Bits:
    def __init__(self):
        self.bits = []

    def put(self, value, length):
        for i in range(length - 1, -1, -1):
            self.bits.append((value >> i) & 1)

    def to_bytes(self):
        out = []
        for i in range(0, len(self.bits), 8):
            byte = 0
            for b in self.bits[i:i + 8]:
                byte = (byte << 1) | b
            out.append(byte)
        return out


def encode_data(data, version):
    bits = Bits()
    bits.put(0b0100, 4)                                   # byte mode
    bits.put(len(data), 8 if version <= 9 else 16)        # character count
    for byte in data:
        bits.put(byte, 8)

    capacity = data_capacity_bytes(version) * 8
    if len(bits.bits) > capacity:
        raise ValueError("payload does not fit version %d" % version)

    bits.put(0, min(4, capacity - len(bits.bits)))        # terminator
    while len(bits.bits) % 8:
        bits.bits.append(0)

    out = bits.to_bytes()
    pad = [0xEC, 0x11]
    i = 0
    while len(out) < data_capacity_bytes(version):
        out.append(pad[i % 2])
        i += 1
    return out


def interleave(data, version):
    blocks = []
    offset = 0
    for count, total, data_len in RS_L[version]:
        ecc_len = total - data_len
        for _ in range(count):
            chunk = data[offset:offset + data_len]
            offset += data_len
            blocks.append((chunk, rs_ecc(chunk, ecc_len)))

    out = []
    max_data = max(len(b[0]) for b in blocks)
    for i in range(max_data):
        for chunk, _ in blocks:
            if i < len(chunk):
                out.append(chunk[i])

    max_ecc = max(len(b[1]) for b in blocks)
    for i in range(max_ecc):
        for _, ecc in blocks:
            if i < len(ecc):
                out.append(ecc[i])

    return out


# ---------------------------------------------------------------- matrix

class Matrix:
    def __init__(self, size):
        self.size = size
        self.mod = [[0] * size for _ in range(size)]      # module value
        self.fn = [[False] * size for _ in range(size)]   # reserved (function pattern)

    def set(self, x, y, value):
        self.mod[y][x] = 1 if value else 0
        self.fn[y][x] = True

    def get(self, x, y):
        return self.mod[y][x]

    def free(self, x, y):
        return 0 <= x < self.size and 0 <= y < self.size and not self.fn[y][x]


def place_finder(m, x, y):
    for dy in range(-1, 8):
        for dx in range(-1, 8):
            px, py = x + dx, y + dy
            if not (0 <= px < m.size and 0 <= py < m.size):
                continue
            inner = 0 <= dx <= 6 and 0 <= dy <= 6
            dark = inner and (dx in (0, 6) or dy in (0, 6) or (2 <= dx <= 4 and 2 <= dy <= 4))
            m.set(px, py, dark)


def place_alignment(m, version):
    coords = ALIGN[version]
    if not coords:
        return
    last = m.size - 7
    for cx in coords:
        for cy in coords:
            if (cx, cy) in ((6, 6), (6, last), (last, 6)):
                continue
            for dy in range(-2, 3):
                for dx in range(-2, 3):
                    dark = max(abs(dx), abs(dy)) != 1
                    m.set(cx + dx, cy + dy, dark)


def place_timing(m):
    for i in range(8, m.size - 8):
        if not m.fn[6][i]:
            m.set(i, 6, i % 2 == 0)
        if not m.fn[i][6]:
            m.set(6, i, i % 2 == 0)


def bch_format(data):
    v = data << 10
    for i in range(4, -1, -1):
        if v & (1 << (i + 10)):
            v ^= 0x537 << i
    return ((data << 10) | v) ^ 0x5412


def bch_version(version):
    v = version << 12
    for i in range(5, -1, -1):
        if v & (1 << (i + 12)):
            v ^= 0x1F25 << i
    return (version << 12) | v


def place_format(m, mask):
    bits = bch_format((ECC_BITS_L << 3) | mask)
    for i in range(15):
        bit = (bits >> i) & 1
        # copy 1
        if i < 6:
            m.set(8, i, bit)
        elif i < 8:
            m.set(8, i + 1, bit)
        else:
            m.set(8, m.size - 15 + i, bit)
        # copy 2
        if i < 8:
            m.set(m.size - i - 1, 8, bit)
        elif i < 9:
            m.set(15 - i, 8, bit)
        else:
            m.set(15 - i - 1, 8, bit)
    m.set(8, m.size - 8, 1)   # dark module


def place_version(m, version):
    if version < 7:
        return
    bits = bch_version(version)
    for i in range(18):
        bit = (bits >> i) & 1
        m.set(m.size - 11 + (i % 3), i // 3, bit)
        m.set(i // 3, m.size - 11 + (i % 3), bit)


def place_data(m, codewords):
    total_bits = len(codewords) * 8
    idx = 0
    upward = True
    x = m.size - 1
    while x > 0:
        if x == 6:
            x -= 1
        rows = range(m.size - 1, -1, -1) if upward else range(m.size)
        for y in rows:
            for dx in (0, 1):
                px = x - dx
                if not m.free(px, y):
                    continue
                bit = 0
                if idx < total_bits:
                    bit = (codewords[idx >> 3] >> (7 - (idx & 7))) & 1
                    idx += 1
                m.mod[y][px] = bit
        upward = not upward
        x -= 2


MASK_FUNCS = [
    lambda x, y: (x + y) % 2 == 0,
    lambda x, y: y % 2 == 0,
    lambda x, y: x % 3 == 0,
    lambda x, y: (x + y) % 3 == 0,
    lambda x, y: (y // 2 + x // 3) % 2 == 0,
    lambda x, y: (x * y) % 2 + (x * y) % 3 == 0,
    lambda x, y: ((x * y) % 2 + (x * y) % 3) % 2 == 0,
    lambda x, y: ((x + y) % 2 + (x * y) % 3) % 2 == 0,
]


def apply_mask(m, mask):
    fn = MASK_FUNCS[mask]
    for y in range(m.size):
        for x in range(m.size):
            if not m.fn[y][x] and fn(x, y):
                m.mod[y][x] ^= 1


def penalty(m):
    size = m.size
    score = 0

    # rule 1: runs of 5+ same-colour modules
    for horizontal in (True, False):
        for a in range(size):
            run = 1
            prev = None
            for b in range(size):
                v = m.mod[a][b] if horizontal else m.mod[b][a]
                if v == prev:
                    run += 1
                else:
                    if run >= 5:
                        score += 3 + (run - 5)
                    run = 1
                    prev = v
            if run >= 5:
                score += 3 + (run - 5)

    # rule 2: 2x2 blocks
    for y in range(size - 1):
        for x in range(size - 1):
            v = m.mod[y][x]
            if v == m.mod[y][x + 1] == m.mod[y + 1][x] == m.mod[y + 1][x + 1]:
                score += 3

    # rule 3: finder-like 1:1:3:1:1 patterns with 4 light modules on one side
    pat = [1, 0, 1, 1, 1, 0, 1]
    for horizontal in (True, False):
        for a in range(size):
            line = [m.mod[a][b] if horizontal else m.mod[b][a] for b in range(size)]
            for i in range(size - 6):
                if line[i:i + 7] == pat:
                    before = line[max(0, i - 4):i]
                    after = line[i + 7:i + 11]
                    if (len(before) == 4 and not any(before)) or (len(after) == 4 and not any(after)):
                        score += 40

    # rule 4: dark ratio
    dark = sum(sum(row) for row in m.mod)
    total = size * size
    ratio = dark * 100 // total
    score += 10 * (abs(ratio - 50) // 5)

    return score


def choose_version(data):
    for v in range(1, 13):
        bits_needed = 4 + (8 if v <= 9 else 16) + len(data) * 8
        if bits_needed <= data_capacity_bytes(v) * 8:
            return v
    raise ValueError("payload too large for version 12")


def encode(data, version=None, force_mask=None):
    if isinstance(data, str):
        data = data.encode("utf-8")
    if version is None:
        version = choose_version(data)

    codewords = interleave(encode_data(data, version), version)

    m = Matrix(17 + 4 * version)
    place_finder(m, 0, 0)
    place_finder(m, m.size - 7, 0)
    place_finder(m, 0, m.size - 7)
    place_alignment(m, version)
    place_timing(m)
    place_version(m, version)

    # The format region must be reserved BEFORE the data zigzag, otherwise the
    # zigzag writes payload bits into the 31 format modules and the symbol is
    # unreadable. Place with mask 0 purely to mark those modules as function
    # patterns; every real mask rewrites them below.
    place_format(m, 0)

    place_data(m, codewords)

    saved = [row[:] for row in m.mod]
    saved_fn = [row[:] for row in m.fn]

    best, best_score = None, None
    for mask in range(8):
        for y in range(m.size):
            m.mod[y] = saved[y][:]
        m.fn = [row[:] for row in saved_fn]
        place_format(m, mask)
        apply_mask(m, mask)
        s = penalty(m)
        if best_score is None or s < best_score:
            best, best_score = mask, s
        if force_mask is not None and mask == force_mask:
            return [row[:] for row in m.mod]

    for y in range(m.size):
        m.mod[y] = saved[y][:]
    m.fn = [row[:] for row in saved_fn]
    place_format(m, best)
    apply_mask(m, best)
    return [row[:] for row in m.mod]
