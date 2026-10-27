using UnityEngine;

namespace Karmaan.Cert
{
    /// <summary>
    /// Turns a <see cref="QrCode"/> matrix into a Unity texture and sprite.
    ///
    /// The quiet zone is not decoration: a scanner that cannot find four light modules around
    /// the symbol will not lock on. It is drawn as part of the texture rather than as UI padding
    /// so that the white margin is guaranteed even if the surrounding panel is dark.
    /// </summary>
    public static class QrSprite
    {
        public const int DefaultPixelsPerModule = 4;
        public const int DefaultQuietModules = 4;

        /// <summary>White background, black modules, point-filtered so edges stay crisp.</summary>
        public static Texture2D ToTexture(QrCode code, int pixelsPerModule = DefaultPixelsPerModule,
            int quietModules = DefaultQuietModules)
        {
            if (code == null) return null;
            if (pixelsPerModule < 1) pixelsPerModule = 1;
            if (quietModules < 0) quietModules = 0;

            var size = code.Size;
            var side = (size + quietModules * 2) * pixelsPerModule;

            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            var light = new Color32(255, 255, 255, 255);
            var dark = new Color32(0, 0, 0, 255);

            var pixels = new Color32[side * side];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = light;

            for (var my = 0; my < size; my++)
            {
                // Texture origin is bottom-left, matrix row 0 is the top, so flip.
                var baseY = side - (my + quietModules + 1) * pixelsPerModule;
                var baseX = quietModules * pixelsPerModule;

                for (var mx = 0; mx < size; mx++)
                {
                    if (code.Modules[my * size + mx] == 0) continue;

                    var x0 = baseX + mx * pixelsPerModule;
                    for (var dy = 0; dy < pixelsPerModule; dy++)
                    {
                        var rowStart = (baseY + dy) * side;
                        for (var dx = 0; dx < pixelsPerModule; dx++)
                        {
                            pixels[rowStart + x0 + dx] = dark;
                        }
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        public static Sprite ToSprite(QrCode code, int pixelsPerModule = DefaultPixelsPerModule,
            int quietModules = DefaultQuietModules)
        {
            var texture = ToTexture(code, pixelsPerModule, quietModules);
            if (texture == null) return null;

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Convenience for the result screen: encode a token straight to a sprite, or null if
        /// the token will not fit. Returning null rather than throwing keeps a certificate
        /// layout bug from taking down the whole result screen.
        /// </summary>
        public static Sprite ForToken(string token, out string error,
            int pixelsPerModule = DefaultPixelsPerModule, int quietModules = DefaultQuietModules)
        {
            var code = QrEncoder.TryEncode(token, out error);
            return code == null ? null : ToSprite(code, pixelsPerModule, quietModules);
        }
    }
}
