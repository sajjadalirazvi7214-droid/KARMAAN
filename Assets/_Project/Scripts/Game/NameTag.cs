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
        private TextMesh _mesh;
        private Transform _camera;

        public static NameTag Attach(GameObject target, string text, Color color, float extraHeight = 0.18f)
        {
            if (target == null) return null;

            var tagObject = new GameObject("NameTag");
            tagObject.transform.SetParent(target.transform, false);
            tagObject.transform.localPosition = new Vector3(0f, MeasureHeight(target) + extraHeight, 0f);

            var mesh = tagObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = Fonts.Devanagari;
            mesh.fontSize = 72;
            mesh.characterSize = 0.045f;
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

        private static float MeasureHeight(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
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
            return (top - centreY) + 0.05f;
        }
    }
}
