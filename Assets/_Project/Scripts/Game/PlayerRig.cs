using UnityEngine;

namespace Suraksha.Game
{
    /// <summary>
    /// First-person camera + reticle raycast.
    ///
    /// This is the "3D mode" rig. It is not a stand-in for AR — it is the required fallback for
    /// phones without ARCore, and the PS says a plain phone must still be able to learn and pass.
    /// AR mode reuses the exact same rig, room and lesson runner; only the camera's transform
    /// source changes (device pose instead of mouse look).
    /// </summary>
    public sealed class PlayerRig : MonoBehaviour, IViewRig
    {
        public Camera Cam { get; private set; }
        public Interactable Hovered { get; private set; }

        /// <summary>Only objects this returns true for can be highlighted or tapped.</summary>
        public System.Func<Interactable, bool> Filter { get; set; }

        /// <summary>3D mode aims with a fixed reticle, so it always shows one.</summary>
        public bool ShowReticle { get { return true; } }

        public float Sensitivity = 3.0f;
        public float Reach = 7f;

        private float _yaw;
        private float _pitch;
        private Interactable _highlighted;

        public static PlayerRig Create(Transform parent, Vector3 position, Vector3 lookAt)
        {
            var go = new GameObject("PlayerRig");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(go.transform, false);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.fieldOfView = 68f;

            cameraObject.tag = "MainCamera";

            var rig = go.AddComponent<PlayerRig>();
            rig.Cam = camera;
            rig.LookAt(lookAt);
            return rig;
        }

        public void LookAt(Vector3 target)
        {
            var direction = (target - transform.position).normalized;
            _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            _pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
            Apply();
        }

        private void Update()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var delta = InputBridge.LookDelta;
                _yaw += delta.x * Sensitivity;
                _pitch -= delta.y * Sensitivity;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);
                Apply();
            }

            UpdateHover();
        }

        private void Apply()
        {
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (Cam != null) Cam.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void UpdateHover()
        {
            Interactable candidate = null;

            if (Cam != null)
            {
                RaycastHit hit;
                var ray = new Ray(Cam.transform.position, Cam.transform.forward);

                if (Physics.Raycast(ray, out hit, Reach, ~0, QueryTriggerInteraction.Ignore))
                {
                    var found = hit.collider.GetComponentInParent<Interactable>();
                    if (found != null && (Filter == null || Filter(found))) candidate = found;
                }
            }

            if (_highlighted == candidate) return;

            if (_highlighted != null) _highlighted.SetHighlighted(false);
            _highlighted = candidate;
            if (_highlighted != null) _highlighted.SetHighlighted(true);

            Hovered = candidate;
        }

        /// <summary>
        /// In 3D mode the reticle is already the answer: whatever the centre ray is on. The screen
        /// position is ignored on purpose — a mouse click and a thumb tap must behave identically,
        /// and the reticle is what the worker is actually aiming with.
        /// </summary>
        public Interactable PickAt(Vector2 screenPoint)
        {
            return Hovered;
        }

        /// <summary>Clears hover state without destroying the rig (used between steps).</summary>
        public void ClearHover()
        {
            if (_highlighted != null) _highlighted.SetHighlighted(false);
            _highlighted = null;
            Hovered = null;
        }

        public void Dispose()
        {
            Destroy(gameObject);
        }
    }
}
