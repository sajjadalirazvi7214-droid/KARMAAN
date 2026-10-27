using System;
using Karmaan.Game;
using UnityEngine;

namespace Karmaan.AR
{
    /// <summary>
    /// AR mode's viewer: the device camera, and taps instead of a reticle.
    ///
    /// There is no hover in AR and there should not be. A worker holding a phone at a hologram of
    /// a workshop is not steering a crosshair; they are pointing at the thing they mean. So the
    /// screen position is used literally, and the same tap that places the room also answers the
    /// first question.
    ///
    /// This class owns no GameObject and no camera — the camera belongs to ArSessionHost, which
    /// outlives it. Dispose is therefore a no-op, which is correct: tearing down a lesson must not
    /// kill the AR session underneath it.
    /// </summary>
    public sealed class ArViewRig : IViewRig
    {
        public Camera Cam { get; private set; }
        public Interactable Hovered { get; private set; }
        public Func<Interactable, bool> Filter { get; set; }

        /// <summary>No crosshair: the finger is the aim.</summary>
        public bool ShowReticle { get { return false; } }

        /// <summary>Far enough for a floor-sized hologram, short enough to ignore the far wall.</summary>
        public float Reach = 25f;

        public static ArViewRig Create(Camera camera)
        {
            return new ArViewRig { Cam = camera };
        }

        public Interactable PickAt(Vector2 screenPoint)
        {
            if (Cam == null) return null;

            var ray = Cam.ScreenPointToRay(screenPoint);

            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, Reach, ~0, QueryTriggerInteraction.Ignore))
            {
                ClearHover();
                return null;
            }

            var found = hit.collider.GetComponentInParent<Interactable>();
            if (found == null || (Filter != null && !Filter(found)))
            {
                ClearHover();
                return null;
            }

            // AR has no hover, so the tap itself has to be the visual acknowledgement: the prop
            // lights up for as long as it is the last thing touched.
            if (Hovered != found)
            {
                ClearHover();
                found.SetHighlighted(true);
            }

            Hovered = found;
            return found;
        }

        public void ClearHover()
        {
            if (Hovered != null) Hovered.SetHighlighted(false);
            Hovered = null;
        }

        public void Dispose()
        {
            // The camera and the session outlive this rig. See class comment.
            ClearHover();
            Cam = null;
        }
    }
}
