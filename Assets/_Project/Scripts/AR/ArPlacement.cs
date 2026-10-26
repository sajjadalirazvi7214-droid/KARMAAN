using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Suraksha.AR
{
    /// <summary>
    /// Keeps a placed object glued to the plane it was dropped on.
    ///
    /// Why not just parent the room to the plane GameObject? Because planes get subsumed. When
    /// ARCore merges two detected planes, the loser is destroyed, and anything parented to it goes
    /// with it — mid-lesson, on the step the worker is answering. Instead we remember the offset in
    /// the plane's local space and re-derive the world pose every frame. If the plane disappears,
    /// the room freezes where it last was rather than vanishing.
    ///
    /// Deliberately not using ARAnchor. The anchor API in AR Foundation 6 is async
    /// (TryAddAnchorAsync -> Result&lt;ARAnchor&gt;) and awaits are a poor fit for a MonoBehaviour that
    /// can be torn down between the call and the result. A plane-relative offset gives the same
    /// stability for a two-minute lesson with none of that.
    /// </summary>
    public sealed class ArPlacement : MonoBehaviour
    {
        private ARPlane _plane;
        private Vector3 _localPosition;
        private Quaternion _localRotation;

        /// <summary>
        /// Create a root that will follow <paramref name="plane"/> and start at <paramref name="worldPose"/>.
        /// </summary>
        public static ArPlacement Create(Transform parent, ARPlane plane, Pose worldPose, float yawDegrees)
        {
            var go = new GameObject("ArPlacement");
            go.transform.SetParent(parent, false);

            var placement = go.AddComponent<ArPlacement>();
            placement.Bind(plane, worldPose, yawDegrees);
            return placement;
        }

        private void Bind(ARPlane plane, Pose worldPose, float yawDegrees)
        {
            _plane = plane;

            var worldRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            if (plane != null)
            {
                // Store the offset in the plane's frame so we can rebuild the world pose later.
                _localPosition = plane.transform.InverseTransformPoint(worldPose.position);
                _localRotation = Quaternion.Inverse(plane.transform.rotation) * worldRotation;
            }
            else
            {
                _localPosition = worldPose.position;
                _localRotation = worldRotation;
            }

            Apply();

            // No plane to follow: the pose is already final, so stop paying for LateUpdate.
            if (_plane == null) enabled = false;
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (_plane != null)
            {
                transform.SetPositionAndRotation(
                    _plane.transform.TransformPoint(_localPosition),
                    _plane.transform.rotation * _localRotation);
                return;
            }

            // Plane is gone. Hold the last pose rather than snapping to the world origin.
            transform.SetPositionAndRotation(_localPosition, _localRotation);
        }
    }
}
