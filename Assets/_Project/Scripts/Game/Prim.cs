using UnityEngine;

namespace Suraksha.Game
{
    /// <summary>
    /// Primitive + material helpers. The demo build is a grey-box: every prop is a scaled cube,
    /// cylinder, sphere or capsule with a flat colour. That is a deliberate choice, not a
    /// shortcut — the art budget in the PS is under 50k tris per scene, and a room you can read
    /// at a glance in a dusty shed beats a detailed one you cannot.
    ///
    /// Phase 3 replaces these with Blender glTF models behind the same option ids, so no
    /// lesson logic has to change.
    /// </summary>
    public static class Prim
    {
        private static Shader _lit;
        private static Shader _unlit;

        private static Shader Lit
        {
            get
            {
                if (_lit == null) _lit = Shader.Find("Standard");
                return _lit;
            }
        }

        private static Shader Unlit
        {
            get
            {
                if (_unlit == null) _unlit = Shader.Find("Unlit/Color");
                return _unlit;
            }
        }

        /// <summary>Matte surface, responds to light.</summary>
        public static Material Mat(Color color)
        {
            var m = new Material(Lit) { color = color };
            m.SetFloat("_Glossiness", 0.12f);
            return m;
        }

        /// <summary>Self-lit surface. Used for things a worker must spot instantly: exits, the
        /// danger ring, the fire base, the muster ring.</summary>
        public static Material Glow(Color color)
        {
            return new Material(Unlit) { color = color };
        }

        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Color color, bool glow = false)
        {
            return Make(PrimitiveType.Cube, name, parent, center, size, color, glow);
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 center, Vector3 size, Color color, bool glow = false)
        {
            return Make(PrimitiveType.Cylinder, name, parent, center, size, color, glow);
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 center, Vector3 size, Color color, bool glow = false)
        {
            return Make(PrimitiveType.Sphere, name, parent, center, size, color, glow);
        }

        public static GameObject Capsule(string name, Transform parent, Vector3 center, Vector3 size, Color color, bool glow = false)
        {
            return Make(PrimitiveType.Capsule, name, parent, center, size, color, glow);
        }

        private static GameObject Make(PrimitiveType type, string name, Transform parent,
            Vector3 center, Vector3 size, Color color, bool glow)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = glow ? Glow(color) : Mat(color);

            return go;
        }

        /// <summary>A flat ring of small blocks on the floor. Used for the gas danger zone and the
        /// white muster circle. Reads far better than a thin torus at arm's length in bright sun.</summary>
        public static GameObject Ring(string name, Transform parent, Vector3 center,
            float radius, int segments, float thickness, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;

            var blockSize = new Vector3(thickness, 0.02f, thickness * 0.7f);
            var material = Glow(color);

            for (var i = 0; i < segments; i++)
            {
                var angle = (i / (float)segments) * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);

                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = name + "_seg" + i;
                block.transform.SetParent(root.transform, false);
                block.transform.localPosition = pos;
                block.transform.localScale = blockSize;
                block.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);

                var renderer = block.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = material;

                var collider = block.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }

            return root;
        }

        public static Light PointLight(string name, Transform parent, Vector3 position, Color color, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;

            return light;
        }

        /// <summary>Strips every collider under a root. Used for decorative props that must never
        /// steal a tap from the object the worker is supposed to hit.</summary>
        public static void StripColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
        }
    }
}
