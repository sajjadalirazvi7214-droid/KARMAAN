using UnityEngine;

namespace Suraksha.Game
{
    /// <summary>
    /// Cheap animated flicker for fire and warning lights.
    ///
    /// Replaces a ParticleSystem on purpose: particle shaders are the single most common source of
    /// magenta geometry in a fresh project, and a handful of bobbing glowing spheres reads as
    /// "fire" from two metres away.
    ///
    /// Positions are recomputed from a stored base every frame rather than incremented, because
    /// incremental offsets accumulate and the flames slowly fly out of the room.
    /// </summary>
    public sealed class Flicker : MonoBehaviour
    {
        public Transform[] parts;
        public float speed = 6f;
        public float amount = 0.18f;
        public Light glow;

        private Vector3[] _baseScales;
        private Vector3[] _basePositions;
        private float _seed;

        private void Start()
        {
            _seed = Random.Range(0f, 100f);

            if (parts == null) return;

            _baseScales = new Vector3[parts.Length];
            _basePositions = new Vector3[parts.Length];

            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                _baseScales[i] = parts[i].localScale;
                _basePositions[i] = parts[i].localPosition;
            }
        }

        private void Update()
        {
            var t = Time.time * speed + _seed;

            if (parts != null && _baseScales != null)
            {
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (part == null) continue;

                    var wobble = 1f + Mathf.Sin(t + i * 1.7f) * amount;
                    part.localScale = _baseScales[i] * wobble;

                    var lift = Mathf.Sin(t * 0.7f + i) * 0.012f;
                    part.localPosition = _basePositions[i] + Vector3.up * lift;
                }
            }

            if (glow != null)
            {
                glow.intensity = Mathf.Max(0.4f, 1.6f + Mathf.Sin(t * 1.3f) * 0.5f);
            }
        }
    }
}
