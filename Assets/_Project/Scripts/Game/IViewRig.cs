using System;
using UnityEngine;

namespace Karmaan.Game
{
    /// <summary>
    /// "Where is the viewer, and what are they pointing at?"
    ///
    /// This is the entire seam between the lesson and reality. The room, the runner, the scoring
    /// and the HUD talk to this interface and know nothing about ARCore, gyroscopes or mice.
    ///
    /// Two implementations:
    ///   PlayerRig  — first-person, mouse or thumb-drag look, reticle in the middle of the screen.
    ///                This is the required 3D fallback for phones without ARCore.
    ///   ArViewRig  — the device camera is the viewer; a tap anywhere on the screen picks a prop.
    ///
    /// The reason this exists as an interface rather than a branch inside PlayerRig: the PS
    /// promises the same lesson works on a ₹8,000 phone with no ARCore. If AR and fallback share
    /// one code path with an `if`, the fallback rots. With an interface, both are first-class.
    /// </summary>
    public interface IViewRig
    {
        /// <summary>The camera the viewer sees through. Never null once created.</summary>
        Camera Cam { get; }

        /// <summary>The prop currently under the reticle. AR rigs may leave this null.</summary>
        Interactable Hovered { get; }

        /// <summary>Only props this returns true for may be picked. Set once per step.</summary>
        Func<Interactable, bool> Filter { get; set; }

        /// <summary>True only for rigs that aim with a fixed centre reticle.</summary>
        bool ShowReticle { get; }

        /// <summary>
        /// Resolve a screen position to the prop the viewer meant.
        /// 3D mode ignores the point and returns whatever the reticle is on; AR uses it directly.
        /// </summary>
        Interactable PickAt(Vector2 screenPoint);

        /// <summary>Drop hover state without destroying the rig (used between steps).</summary>
        void ClearHover();

        void Dispose();
    }
}
