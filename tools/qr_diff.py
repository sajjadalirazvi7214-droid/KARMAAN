"""Diff qr_ref.py against the `qrcode` package, module for module.

Mask is forced on both sides, so this compares the encoder core (GF arithmetic, RS
blocks, interleaving, function patterns, format/version BCH, data placement) and not
the penalty heuristics, which are allowed to differ.
"""

import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import qrcode
from qrcode.util import QRData, MODE_8BIT_BYTE

import qr_ref


def ref_matrix(data, version, mask):
    return qr_ref.encode(data, version=version, force_mask=mask)


def lib_matrix(data, version, mask):
    q = qrcode.QRCode(
        version=version,
        error_correction=qrcode.constants.ERROR_CORRECT_L,
        border=0,
        mask_pattern=mask,
    )
    q.add_data(QRData(data, mode=MODE_8BIT_BYTE))
    q.make(fit=False)
    return [[1 if c else 0 for c in row] for row in q.modules]


def diff(a, b):
    if len(a) != len(b):
        return "size %d vs %d" % (len(a), len(b))
    bad = []
    for y in range(len(a)):
        if len(a[y]) != len(b[y]):
            return "row %d width %d vs %d" % (y, len(a[y]), len(b[y]))
        for x in range(len(a[y])):
            if a[y][x] != b[y][x]:
                bad.append((x, y))
    if not bad:
        return None
    return "%d modules differ, first at %s" % (len(bad), bad[:8])


def main():
    rng = random.Random(20261026)
    failures = 0
    checks = 0

    for version in range(1, 13):
        capacity = qr_ref.data_capacity_bytes(version)
        # true byte-mode payload limit: subtract the 4-bit mode + count field
        header_bits = 4 + (8 if version <= 9 else 16)
        max_payload = (capacity * 8 - header_bits) // 8
        lengths = sorted({
            1,
            max(1, max_payload // 3),
            max(1, max_payload // 2),
            max(1, max_payload - 1),
            max_payload,
        })
        lengths = [n for n in lengths if 1 <= n <= max_payload]

        for length in lengths:
            data = bytes(rng.randrange(256) for _ in range(length))
            for mask in range(8):
                checks += 1
                a = ref_matrix(data, version, mask)
                b = lib_matrix(data, version, mask)
                d = diff(a, b)
                if d:
                    failures += 1
                    print("FAIL v%d len=%d mask=%d: %s" % (version, length, mask, d))
                    if failures > 20:
                        print("too many failures, stopping")
                        return 1

    print("%d checks, %d failures" % (checks, failures))

    # auto version selection must agree with the library's own fit, within v1-12
    v12_max = (qr_ref.data_capacity_bytes(12) * 8 - 20) // 8
    fit_failures = 0
    for length in range(1, v12_max + 1):
        data = bytes(rng.randrange(256) for _ in range(length))
        try:
            mine = qr_ref.choose_version(data)
        except ValueError:
            mine = None
        q = qrcode.QRCode(
            version=None,
            error_correction=qrcode.constants.ERROR_CORRECT_L,
            border=0,
        )
        q.add_data(QRData(data, mode=MODE_8BIT_BYTE))
        try:
            q.make(fit=True)
            theirs = q.version
        except Exception:
            theirs = None
        if mine != theirs:
            fit_failures += 1
            print("FIT FAIL len=%d mine=%s theirs=%s" % (length, mine, theirs))
            if fit_failures > 10:
                break

    print("version-fit checks: %d failures" % fit_failures)
    return 0 if failures == 0 and fit_failures == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
