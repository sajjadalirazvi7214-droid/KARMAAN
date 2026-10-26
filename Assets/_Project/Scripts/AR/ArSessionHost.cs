using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Karmaan.AR
{
    /// <summary>
    /// Builds and owns an ARCore session at runtime, and honestly reports when it cannot.
    ///
    /// The component layout below is not invented. It is the same hierarchy Unity's own
    /// "XR Origin (Mobile AR)" menu item creates (com.unity.xr.arfoundation,
    /// Editor/ARFoundation/XROriginCreateUtil.cs), rebuilt in code so the app has no scene YAML
    /// to corrupt and no prefab reference to lose:
    ///
    ///     AR Session            ARSession + ARInputManager   (input subsystem lifetime)
    ///     XR Origin             XROrigin + ARRaycastManager + ARPlaneManager
    ///       Camera Offset
    ///         AR Camera       Camera + ARCameraManager + ARCameraBackground + TrackedPoseDriver
    ///
    /// The TrackedPoseDriver bindings are copied verbatim from that same file. They are not
    /// optional: without `<XRHMD>/centerEyePosition` and `<HandheldARInputDevice>/devicePosition`
    /// the camera sits at the origin and the world appears pinned to your nose.
    ///
    /// Failure is a first-class outcome, not an exception. Three ways this ends:
    ///   Ready      — tracking is live, the presenter can place the room.
    ///   Failure    — ARCore is missing, refused, or did not come up in time.
    ///   neither    — still starting; the presenter keeps showing the scan prompt.
    /// </summary>
    public sealed class ArSessionHost : MonoBehaviour
    {
        /// <summary>
        /// How long to wait for tracking before giving up. A mid-range phone with a cold ARCore
        /// usually needs 1-3 s; anything past six is a device that is not going to work, and the
        /// worker should be learning in 3D instead of staring at a spinner.
        /// </summary>
        public const float TimeoutSeconds = 6f;

        public Camera Camera { get; private set; }
        public ARRaycastManager Raycasts { get; private set; }
        public ARPlaneManager Planes { get; private set; }

        /// <summary>True once the AR camera is actually tracking the room around you.</summary>
        public bool Ready { get; private set; }

        /// <summary>Null while starting. Set to a stable reason code once AR is ruled out.</summary>
        public string Failure { get; private set; }

        /// <summary>True while the camera feed is live but no plane has been seen yet.</summary>
        public bool AnyPlaneSeen { get; private set; }

        private float _elapsed;
        private readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();

        public static ArSessionHost Create(Transform parent)
        {
            var go = new GameObject("ArSession");
            go.transform.SetParent(parent, false);

            var host = go.AddComponent<ArSessionHost>();
            host.Build();
            return host;
        }

        private void Build()
        {
            // --- session + input subsystem -------------------------------------
            // ARInputManager is required, not optional: "Without the AR Input Manager component,
            // XROrigin can't acquire a world-space pose for the device." (AR Foundation docs)
            gameObject.AddComponent<ARSession>();
            gameObject.AddComponent<ARInputManager>();

            // --- XR Origin ------------------------------------------------------
            var originObject = new GameObject("XR Origin");
            originObject.transform.SetParent(transform, false);

            var origin = originObject.AddComponent<XROrigin>();

            var offsetObject = new GameObject("Camera Offset");
            offsetObject.transform.SetParent(originObject.transform, false);

            // --- camera ---------------------------------------------------------
            var cameraObject = new GameObject("AR Camera");
            cameraObject.transform.SetParent(offsetObject.transform, false);
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30f;
            camera.fieldOfView = 60f;

            // The app already owns an AudioListener on the AppFlow object; a second one makes
            // Unity log a warning every frame and can mute the tones.
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();

            ConfigurePoseDriver(cameraObject);

            origin.CameraFloorOffsetObject = offsetObject;
            origin.Camera = camera;

            // --- trackables -----------------------------------------------------
            // Both managers need XROrigin on their own GameObject ([RequireComponent]).
            Raycasts = originObject.AddComponent<ARRaycastManager>();

            Planes = originObject.AddComponent<ARPlaneManager>();
            Planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;

            // planePrefab is left null on purpose. AR Foundation then spawns bare GameObjects with
            // only an ARPlane component — tracking with nothing drawn. Drawing planes would cost a
            // mesh per plane and, worse, put a MeshCollider in front of every tap.

            Camera = camera;
        }

        /// <summary>
        /// Exactly the bindings Unity's XROriginCreateUtil assigns. A TrackedPoseDriver added at
        /// runtime has no bindings at all, so this must be explicit.
        /// </summary>
        private static void ConfigurePoseDriver(GameObject cameraObject)
        {
            var driver = cameraObject.AddComponent<TrackedPoseDriver>();

            var position = new InputAction("Position", binding: "<XRHMD>/centerEyePosition",
                expectedControlType: "Vector3");
            position.AddBinding("<HandheldARInputDevice>/devicePosition");

            var rotation = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion");
            rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");

            driver.positionInput = new InputActionProperty(position);
            driver.rotationInput = new InputActionProperty(rotation);
        }

        /// <summary>Call once per frame until Ready or Failure is set.</summary>
        public void Tick(float deltaTime)
        {
            if (Ready || Failure != null) return;

            _elapsed += deltaTime;

            if (!AnyPlaneSeen && Planes != null && Planes.trackables.count > 0)
            {
                AnyPlaneSeen = true;
            }

            var state = ARSession.state;

            if (state == ARSessionState.SessionTracking)
            {
                Ready = true;
                return;
            }

            if (state == ARSessionState.Unsupported)
            {
                Fail("unsupported");
                return;
            }

            if (_elapsed >= TimeoutSeconds)
            {
                Fail("timeout");
            }
        }

        public void Fail(string reason)
        {
            if (Failure != null) return;
            Failure = reason;
            Debug.LogWarning("[ArSessionHost] AR unavailable: " + reason);
        }

        /// <summary>
        /// Screen point -> a pose on a horizontal plane. Returns false when the tap missed every
        /// detected plane, which is the normal case for the first second or two.
        /// </summary>
        public bool TryRaycastPlane(Vector2 screenPoint, out Pose pose, out ARPlane plane)
        {
            pose = Pose.identity;
            plane = null;

            if (Raycasts == null) return false;

            _hits.Clear();
            if (!Raycasts.Raycast(screenPoint, _hits, TrackableType.PlaneWithinPolygon)) return false;
            if (_hits.Count == 0) return false;

            var hit = _hits[0];
            pose = hit.pose;

            if (Planes != null) plane = Planes.GetPlane(hit.trackableId);

            return true;
        }

        /// <summary>Yaw of the device, flattened. Used to face the placed room at the viewer.</summary>
        public float ViewerYaw
        {
            get
            {
                if (Camera == null) return 0f;
                var forward = Camera.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) return 0f;
                return Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;
            }
        }

        /// <summary>
        /// Turn plane detection off once the room is placed. Planes keep being discovered and
        /// refined for as long as the manager runs, and every one of those is work the phone does
        /// not need to do while a worker is answering a question about a fire extinguisher.
        /// </summary>
        public void StopPlaneDetection()
        {
            if (Planes != null) Planes.enabled = false;
        }

        private void OnDestroy()
        {
            // The TrackedPoseDriver's InputActions were created with `new`, so nothing else owns
            // them. Without this they leak one native action map per lesson start.
            if (Camera == null) return;

            var driver = Camera.GetComponent<TrackedPoseDriver>();
            if (driver == null) return;

            DisposeAction(driver.positionInput);
            DisposeAction(driver.rotationInput);
        }

        private static void DisposeAction(InputActionProperty property)
        {
            if (property.action != null) property.action.Dispose();
        }
    }
}
