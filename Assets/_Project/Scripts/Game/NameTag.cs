using UnityEngine;

namespace Suraksha.Game
{
    /// <summary>
    /// A floating label above a prop, billboarded to the camera.
    ///
    /// This is not decoration. The build is a grey-box of coloured primitives, and a blue cylinder
    /// means nothing to a worker unless it says "नीली CO2". The name tag is what makes a cheap
    /// shape teachable, and it is also the accessibility path for anyone who cannot read the
    /// prompt text at the top of the screen.
    ///
    /// Uses TextMesh rather than a world-space Canvas on purpose: one draw call per label, no
    /// layout rebuild, no extra EventSystem surface. On a 3 GB phone that difference is real.
    /// </summary>
    public sealed class NameTag : MonoBehaviour
    {
        /// <summary>Label height in world metres before any scaling.</summary>
        private const float BaseCharacterSize = 0.05f;

        private TextMesh _mesh;
        private Transform _camera;

        /// <summary>
        /// Multiplier on the label's world size. AR sets this above 1 because the room is a
        /// two-metre hologram: a label sized for a full-scale room becomes unreadable at that
        /// distance, and an unreadable label defeats the point of having one.
        /// </summary>
        public static float SizeMultiplier = 1f;

        public static NameTag Attach(GameObject target, string text, Color color, float extraHeight = 0.18f)
        {
            if (target == null) return null;

            // A label parented to a scaled object inherits that scale. The diorama is built at
            // roughly 0.28, so without compensation every label would render at 28% size and sit
            // 72% too close to its prop. Divide both the glyph size and the offset by the parent's
            // scale to keep the label at a constant, readable size in world space.
            var parentScale = target.transform.lossyScale.x;
            if (parentScale < 0.0001f) parentScale = 1f;

            var tagObject = new GameObject("NameTag");
            tagObject.transform.SetParent(target.transform, false);
            tagObject.transform.localPosition =
                new Vector3(0f, MeasureLocalHeight(target, parentScale) + extraHeight / parentScale, 0f);

            var mesh = tagObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = Fonts.Devanagari;
            mesh.fontSize = 72;
            mesh.characterSize = (BaseCharacterSize * SizeMultiplier) / parentScale;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            mesh.richText = false;

            var renderer = tagObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mesh.font != null ? mesh.font.material : null;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var tag = tagObject.AddComponent<NameTag>();
            tag._mesh = mesh;
            return tag;
        }

        public void SetText(string text)
        {
            if (_mesh != null) _mesh.text = text;
        }

        public void SetColor(Color color)
        {
            if (_mesh != null) _mesh.color = color;
        }

        public void SetVisible(bool visible)
        {
            if (_mesh != null && _mesh.gameObject.activeSelf != visible)
            {
                _mesh.gameObject.SetActive(visible);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                var main = Camera.main;
                if (main == null) return;
                _camera = main.transform;
            }

            // Face the camera, upright. LookRotation(pos - camPos) points the text's forward
            // away from the viewer, which is what makes it readable rather than mirrored.
            transform.rotation = Quaternion.LookRotation(transform.position - _camera.position, Vector3.up);
        }

        /// <summary>
        /// Height above the prop's origin at which the label should sit, in the prop's LOCAL units.
        ///
        /// Renderer bounds are world-space, so they are divided by the parent's scale before being
        /// used as a local offset. Without that, an AR diorama at 0.28 scale would push every label
        /// 3.6x too high and out through the (now removed) ceiling.
        /// </summary>
        private static float MeasureLocalHeight(GameObject target, float parentScale)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();

            // Invisible hitboxes (the muster ring, the buddy's tap target) have no renderer to
            // measure. Half a metre above the origin is a sensible default in room units, and it
            // scales with the room instead of floating a fixed distance in the air.
            if (renderers.Length == 0) return 0.5f;

            var top = float.NegativeInfinity;
            var bottom = float.PositiveInfinity;

            for (var i = 0; i < renderers.Length; i++)
            {
                var bounds = renderers[i].bounds;
                top = Mathf.Max(top, bounds.max.y);
                bottom = Mathf.Min(bottom, bounds.min.y);
            }

            var centreY = (top + bottom) * 0.5f;
            var worldOffset = (top - centreY) + 0.05f;

            return worldOffset / parentScale;
        }
    }
}
