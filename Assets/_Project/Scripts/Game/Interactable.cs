using UnityEngine;

namespace Suraksha.Game
{
    /// <summary>
    /// A thing in the room the worker can aim at and tap. It knows which lesson step it answers
    /// and which option id it represents — nothing else. All judging happens in LessonRunner,
    /// so a prop can never accidentally "win" a step on its own.
    ///
    /// Option ids here must match the ids in the lesson JSON. A mismatch is a silent dead tap,
    /// which is why the room builder and the packs are edited together.
    /// </summary>
    public sealed class Interactable : MonoBehaviour
    {
        public string stepId;
        public string optionId;

        /// <summary>Localisation key for the floating name tag. Null means "no tag".</summary>
        public string labelKey;

        public Color highlightColor = new Color(1f, 0.86f, 0.25f);

        private Renderer[] _renderers;
        private Material[] _materials;
        private Color[] _baseColors;
        private Vector3 _baseScale;
        private bool _highlighted;

        private void Awake()
        {
            _baseScale = transform.localScale;
            Cache();
        }

        private void Cache()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _materials = new Material[_renderers.Length];
            _baseColors = new Color[_renderers.Length];

            for (var i = 0; i < _renderers.Length; i++)
            {
                _materials[i] = _renderers[i].sharedMaterial;
                _baseColors[i] = _materials[i] != null ? _materials[i].color : Color.white;
            }
        }

        public Interactable Bind(string forStepId, string forOptionId, string forLabelKey = null)
        {
            stepId = forStepId;
            optionId = forOptionId;
            labelKey = forLabelKey;
            return this;
        }

        public void SetHighlighted(bool on)
        {
            if (_highlighted == on) return;
            _highlighted = on;

            if (_materials == null) Cache();

            for (var i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] == null) continue;
                _materials[i].color = on ? highlightColor : _baseColors[i];
            }

            transform.localScale = on ? _baseScale * 1.06f : _baseScale;
        }

        /// <summary>Permanent selection tint, used by the multi-pick PPE step.</summary>
        public void SetSelected(bool on)
        {
            if (_materials == null) Cache();

            var target = on ? new Color(0.35f, 1f, 0.55f) : (_highlighted ? highlightColor : Color.white);
            for (var i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] == null) continue;
                _materials[i].color = on ? target : _baseColors[i];
            }
        }

        /// <summary>Renders an object as "already used up" after it answered a step.</summary>
        public void Fade()
        {
            if (_materials == null) Cache();

            for (var i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] == null) continue;
                var c = _baseColors[i];
                _materials[i].color = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, c.a);
            }
        }
    }
}
