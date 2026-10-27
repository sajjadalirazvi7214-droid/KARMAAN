"""Round-trip gate: encode with qr_ref, render, decode with ZXing.

Agreeing with the `qrcode` package proves two encoders concur; it does not prove either
produces a symbol a scanner can read. This renders the matrix the way the game will
(quiet zone + integer module scale) and asks an independent decoder for the bytes back.

ZXing is authoritative here. OpenCV's QRCodeDetector is reported alongside it for
information only: it fails to detect some perfectly valid symbols (including ones
produced by the `qrcode` package itself), so it is not used as a pass/fail signal.
"""

import base64
import hashlib
import hmac
import json
import os
import random
import sys
import uuid

import cv2
import numpy as np
import zxingcpp

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import qr_ref


def render(matrix, scale=8, quiet=4):
    """Matrix -> uint8 image, dark = 0. The quiet zone is mandatory for detection."""
    size = len(matrix)
    width = (size + quiet * 2) * scale
    img = np.full((width, width), 255, dtype=np.uint8)
    for y in range(size):
        for x in range(size):
            if matrix[y][x]:
                img[(y + quiet) * scale:(y + quiet + 1) * scale,
                    (x + quiet) * scale:(x + quiet + 1) * scale] = 0
    return img


def decode_zxing(img):
    result = zxingcpp.read_barcode(img)
    return result.text if result else None


def decode_opencv(img):
    text, points, _ = cv2.QRCodeDetector().detectAndDecode(img)
    return text if points is not None else None


def real_token():
    """The exact payload shape CertService emits."""
    payload = {
        "v": 1,
        "wid": "JH-DEMO-101",
        "lid": "fire",
        "score": 95,
        "issuedAt": "2026-10-27T00:15:00Z",
        "centre": "DEMO-CENTRE",
        "attempt": str(uuid.uuid4()),
    }
    canonical = json.dumps(payload, separators=(",", ":"), ensure_ascii=False)

    def b64u(b):
        return base64.urlsafe_b64encode(b).decode().rstrip("=")

    body = b64u(canonical.encode("utf-8"))
    sig = b64u(hmac.new(b"dev-only-not-a-real-key", body.encode("ascii"),
                        hashlib.sha256).digest())
    return body + "." + sig


def main():
    failures = 0
    checks = 0
    opencv_misses = 0

    # 1. the real certificate token
    token = real_token()
    version = qr_ref.choose_version(token.encode())
    matrix = qr_ref.encode(token)
    img = render(matrix)
    checks += 1
    got = decode_zxing(img)
    if got != token:
        failures += 1
        print("FAIL real token v%d: %r" % (version, (got or "")[:60]))
    else:
        print("OK   real token  %3d chars  v%-2d %dx%d"
              % (len(token), version, len(matrix), len(matrix)))
    if decode_opencv(img) != token:
        opencv_misses += 1

    # 2. sweep every version with random alphanumeric payloads at several fills
    rng = random.Random(20261027)
    alphabet = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.=+/"
    for version in range(1, 13):
        capacity = qr_ref.data_capacity_bytes(version)
        header = 4 + (8 if version <= 9 else 16)
        max_payload = (capacity * 8 - header) // 8
        for frac in (0.25, 0.5, 0.9, 1.0):
            length = max(1, min(max_payload, int(max_payload * frac)))
            data = bytes(rng.choice(alphabet) for _ in range(length))
            m = qr_ref.encode(data, version=version)
            img = render(m)
            checks += 1
            got = decode_zxing(img)
            if got != data.decode("ascii"):
                failures += 1
                print("FAIL v%-2d len=%3d: %r" % (version, length, (got or "")[:40]))
            if decode_opencv(img) != data.decode("ascii"):
                opencv_misses += 1

    print("\n%d round-trip checks, %d failures" % (checks, failures))
    print("OpenCV missed %d of %d (informational; ZXing is authoritative)"
          % (opencv_misses, checks))
    return 0 if failures == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
