using System.Collections.Generic;
using System.IO;
using System.Text;
using Karmaan.Cert;
using UnityEditor;
using UnityEngine;

namespace Karmaan.EditorTools
{
    /// <summary>
    /// Encodes a fixed set of payloads to PNG and writes a manifest of expected text beside them.
    ///
    /// This exists because the encoder cannot be trusted on table parity alone: the tables can be
    /// perfect while the Reed-Solomon division, the data zigzag or the BCH fields are wrong. The
    /// only proof that a symbol scans is to scan one. Run this, then run:
    ///
    ///     python tools/check_qr.py
    ///
    /// which decodes every PNG with ZXing and compares against the manifest. The payloads include
    /// a real CertService token, so the gate covers the exact byte range the app ships.
    /// </summary>
    public static class QrSelfTest
    {
        private const string OutputFolder = "Builds/qr-selftest";
        private const int PixelsPerModule = 4;
        private const int QuietModules = 4;

        [MenuItem("Karmaan/Run QR self-test (writes PNGs)", false, 40)]
        public static void Run()
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), OutputFolder);
            Directory.CreateDirectory(folder);

            var cases = BuildCases();
            var manifest = new StringBuilder();
            manifest.Append("[\n");

            var failures = 0;
            for (var i = 0; i < cases.Count; i++)
            {
                var item = cases[i];
                string error;
                var code = QrEncoder.TryEncode(item.Value, out error);

                if (code == null)
                {
                    Debug.LogError("[QrSelfTest] '" + item.Key + "' failed to encode: " + error);
                    failures++;
                    continue;
                }

                var texture = QrSprite.ToTexture(code, PixelsPerModule, QuietModules);
                var png = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);

                var file = item.Key + ".png";
                File.WriteAllBytes(Path.Combine(folder, file), png);

                manifest.Append("  {\"file\": \"").Append(file).Append("\", ");
                manifest.Append("\"version\": ").Append(code.Version).Append(", ");
                manifest.Append("\"mask\": ").Append(code.Mask).Append(", ");
                manifest.Append("\"length\": ").Append(item.Value.Length).Append(", ");
                manifest.Append("\"text\": \"").Append(JsonEscape(item.Value)).Append("\"}");
                manifest.Append(i == cases.Count - 1 ? "\n" : ",\n");

                Debug.Log(string.Format("[QrSelfTest] {0,-18} v{1,-2} mask {2}  {3}x{3}  {4} chars",
                    item.Key, code.Version, code.Mask, code.Size, item.Value.Length));
            }

            manifest.Append("]\n");
            File.WriteAllText(Path.Combine(folder, "manifest.json"), manifest.ToString());

            AssetDatabase.Refresh();

            if (failures > 0)
            {
                Debug.LogError("[QrSelfTest] " + failures + " payload(s) failed to encode. See above.");
            }
            else
            {
                Debug.Log("[QrSelfTest] Wrote " + cases.Count + " PNGs to " + OutputFolder +
                          ". Now run: python tools/check_qr.py");
            }
        }

        private static List<KeyValuePair<string, string>> BuildCases()
        {
            var cases = new List<KeyValuePair<string, string>>();

            // The real thing: a signed certificate exactly as CertService.Issue produces it.
            var cert = new CertService("karmaan-selftest-secret");
            var token = cert.Issue(new CertPayload
            {
                wid = "JH-DEMO-101",
                lid = "fire",
                score = 95,
                issuedAt = 1793000000L,
                centre = "DEMO-CENTRE",
                attempt = "00000000-0000-4000-8000-000000000001"
            });
            cases.Add(new KeyValuePair<string, string>("cert-real", token));

            // A Santali-length worker id and a long centre code, to push the version up.
            var longToken = cert.Issue(new CertPayload
            {
                wid = "JH-PAKUR-GIRIDIH-0042",
                lid = "gas",
                score = 78,
                issuedAt = 1793000000L,
                centre = "PAKUR-DISTRICT-TRAINING-CENTRE-NORTH",
                attempt = "00000000-0000-4000-8000-000000000002"
            });
            cases.Add(new KeyValuePair<string, string>("cert-long", longToken));

            // Version boundaries, so the gate exercises more than one block layout.
            cases.Add(new KeyValuePair<string, string>("v1", "KARMAAN"));
            cases.Add(new KeyValuePair<string, string>("v2", Repeat("A", 30)));
            cases.Add(new KeyValuePair<string, string>("v5", Repeat("Karmaan-", 10)));
            cases.Add(new KeyValuePair<string, string>("v7", Repeat("Suraksha", 10)));
            cases.Add(new KeyValuePair<string, string>("v10", Repeat("jh-karmaan-", 22)));
            cases.Add(new KeyValuePair<string, string>("v12", Repeat("z", 367)));

            // Non-ASCII must survive UTF-8 encoding.
            cases.Add(new KeyValuePair<string, string>("utf8", "सुरक्षा Karmaan"));

            return cases;
        }

        private static string Repeat(string unit, int times)
        {
            var sb = new StringBuilder(unit.Length * times);
            for (var i = 0; i < times; i++) sb.Append(unit);
            return sb.ToString();
        }

        private static string JsonEscape(string value)
        {
            var sb = new StringBuilder(value.Length + 8);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
