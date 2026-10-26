using System.Collections.Generic;
using UnityEngine;

namespace Suraksha.Game
{
    /// <summary>Everything a built room hands back to the lesson presenter.</summary>
    public sealed class BuiltRoom
    {
        public GameObject Root;
        public Vector3 PlayerStart = new Vector3(0f, 1.6f, -2.7f);
        public Vector3 PlayerLookAt = new Vector3(0f, 1.3f, 2.5f);

        private readonly Dictionary<string, Interactable> _map =
            new Dictionary<string, Interactable>();

        private readonly List<Interactable> _all = new List<Interactable>();

        public IReadOnlyList<Interactable> All => _all;

        public void Register(Interactable target)
        {
            if (target == null) return;
            _all.Add(target);
            _map[Key(target.stepId, target.optionId)] = target;
        }

        public Interactable Find(string stepId, string optionId)
        {
            Interactable found;
            return _map.TryGetValue(Key(stepId, optionId), out found) ? found : null;
        }

        public List<Interactable> ForStep(string stepId)
        {
            var list = new List<Interactable>();
            for (var i = 0; i < _all.Count; i++)
            {
                if (_all[i].stepId == stepId) list.Add(_all[i]);
            }
            return list;
        }

        public void Destroy()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root);
            _all.Clear();
            _map.Clear();
        }

        private static string Key(string stepId, string optionId)
        {
            return stepId + "|" + optionId;
        }
    }

    /// <summary>
    /// Builds the grey-box room for a lesson at runtime, out of coloured primitives.
    ///
    /// Why procedural instead of a hand-authored scene: a .unity file is a 900-line YAML blob of
    /// fileIDs and GUIDs. One wrong reference and the scene opens empty with no error. Building
    /// from code means the room is reviewable, diffable, and cannot silently lose a prop.
    ///
    /// Cost, per room: ~45 primitives, 2-3 lights, no shadows on point lights, no post-processing.
    /// That is roughly 60 draw calls and well under 5k triangles — comfortably inside the
    /// "3 GB RAM phone, 30 fps" budget the PS asks for.
    ///
    /// Phase 3 swaps these primitives for Blender glTF models behind the same option ids.
    /// </summary>
    public static class RoomBuilder
    {
        private static readonly Color FloorColor = new Color(0.22f, 0.22f, 0.24f);
        private static readonly Color WallColor = new Color(0.34f, 0.35f, 0.38f);
        private static readonly Color CeilingColor = new Color(0.14f, 0.14f, 0.16f);
        private static readonly Color Wood = new Color(0.42f, 0.30f, 0.19f);
        private static readonly Color Red = new Color(0.86f, 0.16f, 0.14f);
        private static readonly Color Green = new Color(0.10f, 0.82f, 0.36f);
        private static readonly Color Blue = new Color(0.16f, 0.46f, 0.95f);
        private static readonly Color Amber = new Color(1f, 0.62f, 0.10f);
        private static readonly Color White = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color Steel = new Color(0.55f, 0.58f, 0.60f);
        private static readonly Color Dark = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color Yellow = new Color(0.95f, 0.78f, 0.12f);

        private const float HalfWidth = 4.5f;
        private const float HalfDepth = 3.5f;
        private const float Height = 3.2f;

        public static BuiltRoom Build(string lessonId, Transform parent)
        {
            var room = new BuiltRoom();
            var root = new GameObject("Room_" + lessonId);
            root.transform.SetParent(parent, false);
            room.Root = root;

            Shell(root);

            if (lessonId == "fire") BuildFire(root, room);
            else if (lessonId == "gas") BuildGas(root, room);
            else throw new System.ArgumentException("No room layout for lesson '" + lessonId + "'.");

            return room;
        }

        // ---------------------------------------------------------------- shell

        private static void Shell(GameObject root)
        {
            var t = root.transform;

            Prim.Box("Floor", t, new Vector3(0f, -0.05f, 0f),
                new Vector3(HalfWidth * 2f, 0.1f, HalfDepth * 2f), FloorColor);
            Prim.Box("Ceiling", t, new Vector3(0f, Height, 0f),
                new Vector3(HalfWidth * 2f, 0.1f, HalfDepth * 2f), CeilingColor);
            Prim.Box("Wall_North", t, new Vector3(0f, Height * 0.5f, HalfDepth),
                new Vector3(HalfWidth * 2f, Height, 0.1f), WallColor);
            Prim.Box("Wall_South", t, new Vector3(0f, Height * 0.5f, -HalfDepth),
                new Vector3(HalfWidth * 2f, Height, 0.1f), WallColor);
            Prim.Box("Wall_East", t, new Vector3(HalfWidth, Height * 0.5f, 0f),
                new Vector3(0.1f, Height, HalfDepth * 2f), WallColor);
            Prim.Box("Wall_West", t, new Vector3(-HalfWidth, Height * 0.5f, 0f),
                new Vector3(0.1f, Height, HalfDepth * 2f), WallColor);

            Prim.PointLight("FillLight", t, new Vector3(0f, 3.0f, 0f), new Color(1f, 0.96f, 0.88f), 12f, 0.85f);
        }

        // ---------------------------------------------------------------- fire

        private static void BuildFire(GameObject root, BuiltRoom room)
        {
            var t = root.transform;

            // The sparking board the fire starts from.
            Prim.Box("WireBoard", t, new Vector3(0f, 1.75f, 3.4f), new Vector3(1.3f, 0.85f, 0.08f), Dark);
            var spark = Prim.Box("WireBoard_Spark", t, new Vector3(0f, 1.75f, 3.33f),
                new Vector3(1.05f, 0.6f, 0.03f), Amber, true);
            Prim.StripColliders(spark);

            // Fire on the floor. This is the PASS target.
            var fireBase = Prim.Box("FireBase", t, new Vector3(0f, 0.04f, 2.45f),
                new Vector3(1.5f, 0.08f, 1.05f), new Color(1f, 0.45f, 0.05f), true);
            Tag(fireBase, room, "fire_pass", "fire_base", "fire.pass.label");

            var flames = new List<Transform>();
            for (var i = 0; i < 7; i++)
            {
                var angle = i * 0.9f;
                var offset = new Vector3(Mathf.Sin(angle) * 0.42f, 0.18f + (i % 3) * 0.16f, Mathf.Cos(angle) * 0.3f);
                var flame = Prim.Sphere("Flame" + i, t, new Vector3(0f, 0.04f, 2.45f) + offset,
                    Vector3.one * (0.26f + (i % 3) * 0.06f), new Color(1f, 0.52f, 0.08f), true);
                Prim.StripColliders(flame);
                flames.Add(flame.transform);
            }

            var fireGlow = Prim.PointLight("FireGlow", t, new Vector3(0f, 0.9f, 2.45f),
                new Color(1f, 0.55f, 0.15f), 7f, 2.2f);

            var flicker = root.AddComponent<Flicker>();
            flicker.parts = flames.ToArray();
            flicker.glow = fireGlow;

            // Alarm bell, high on the wall.
            var alarm = Prim.Sphere("AlarmBell", t, new Vector3(-3.55f, 2.6f, 3.3f),
                Vector3.one * 0.3f, Amber, true);
            Tag(alarm, room, "fire_alarm", "alarm", "fire.alarm.label");

            // Three ways out. Only one of them is a way out.
            var mainDoor = Prim.Box("MainDoor", t, new Vector3(-2.4f, 1.15f, 3.42f),
                new Vector3(1.15f, 2.3f, 0.1f), Red, true);
            Tag(mainDoor, room, "fire_exit", "main_door", "fire.exit.main");

            var sideExit = Prim.Box("SideExit", t, new Vector3(3.3f, 1.15f, 3.42f),
                new Vector3(1.15f, 2.3f, 0.1f), Green, true);
            Tag(sideExit, room, "fire_exit", "side_exit", "fire.exit.side");

            var window = Prim.Box("Window", t, new Vector3(1.15f, 2.0f, 3.42f),
                new Vector3(0.9f, 0.6f, 0.08f), new Color(0.55f, 0.78f, 0.95f), true);
            Tag(window, room, "fire_exit", "window", "fire.exit.window");

            // Bottle table.
            var table = Prim.Box("BottleTable", t, new Vector3(0f, 0.42f, 0.4f),
                new Vector3(2.7f, 0.08f, 0.9f), Wood);
            Prim.StripColliders(table);
            for (var i = 0; i < 4; i++)
            {
                var lx = (i < 2 ? -1.2f : 1.2f);
                var lz = (i % 2 == 0 ? -0.35f : 0.35f);
                var leg = Prim.Box("TableLeg" + i, t, new Vector3(lx, 0.2f, 0.4f + lz),
                    new Vector3(0.08f, 0.4f, 0.08f), Wood);
                Prim.StripColliders(leg);
            }

            var redBottle = Prim.Cylinder("Bottle_RedPowder", t, new Vector3(-0.8f, 0.67f, 0.4f),
                new Vector3(0.26f, 0.21f, 0.26f), Red);
            Tag(redBottle, room, "fire_bottle", "red_abc", "fire.bottle.red");

            var blueBottle = Prim.Cylinder("Bottle_CO2", t, new Vector3(0f, 0.67f, 0.4f),
                new Vector3(0.26f, 0.21f, 0.26f), new Color(0.18f, 0.42f, 0.92f));
            Tag(blueBottle, room, "fire_bottle", "blue_co2", "fire.bottle.blue");

            var waterBucket = Prim.Cylinder("Bottle_Water", t, new Vector3(0.8f, 0.62f, 0.4f),
                new Vector3(0.34f, 0.17f, 0.34f), new Color(0.45f, 0.72f, 0.92f));
            Tag(waterBucket, room, "fire_bottle", "water", "fire.bottle.water");

            // Muster sequence: shut the door, get to the ring, count heads.
            var lever = Prim.Box("MusterLever", t, new Vector3(2.35f, 1.2f, 3.38f),
                new Vector3(0.16f, 0.5f, 0.14f), Red, true);
            Tag(lever, room, "fire_muster", "close_door", "fire.muster.close");

            Prim.Ring("MusterRing", t, new Vector3(0f, 0.03f, -2.0f), 1.2f, 28, 0.16f, White);
            var ringHit = Hitbox("MusterRing_Hit", t, new Vector3(0f, 0.05f, -2.0f),
                new Vector3(2.3f, 0.1f, 2.3f));
            Tag(ringHit, room, "fire_muster", "reach_ring", "fire.muster.ring");

            var board = Prim.Box("RollCallBoard", t, new Vector3(0f, 1.5f, -3.38f),
                new Vector3(0.9f, 0.6f, 0.08f), White, true);
            Tag(board, room, "fire_muster", "roll_call", "fire.muster.roll");

            room.PlayerStart = new Vector3(0f, 1.6f, -2.7f);
            room.PlayerLookAt = new Vector3(0f, 1.2f, 2.5f);
        }

        // ---------------------------------------------------------------- gas

        private static void BuildGas(GameObject root, BuiltRoom room)
        {
            var t = root.transform;

            Prim.Cylinder("GasTank", t, new Vector3(0f, 1.1f, 2.3f),
                new Vector3(1.5f, 1.1f, 1.5f), new Color(0.40f, 0.46f, 0.42f));
            Prim.Ring("DangerRing", t, new Vector3(0f, 0.04f, 2.3f), 1.9f, 32, 0.18f, Red);

            var sensor = Prim.Sphere("BeepSensor", t, new Vector3(1.05f, 2.3f, 2.3f),
                Vector3.one * 0.28f, Amber, true);
            Tag(sensor, room, "gas_beep", "beep", "gas.beep.label");

            // PPE table.
            var table = Prim.Box("PpeTable", t, new Vector3(0f, 0.42f, -0.8f),
                new Vector3(3.0f, 0.08f, 1.0f), Wood);
            Prim.StripColliders(table);

            var helmet = Prim.Sphere("Ppe_Helmet", t, new Vector3(-1.2f, 0.60f, -0.8f),
                new Vector3(0.38f, 0.30f, 0.38f), Yellow);
            Tag(helmet, room, "gas_ppe", "helmet", "gas.ppe.helmet");

            var mask = Prim.Box("Ppe_Mask", t, new Vector3(-0.6f, 0.58f, -0.8f),
                new Vector3(0.30f, 0.24f, 0.22f), Dark);
            Tag(mask, room, "gas_ppe", "mask", "gas.ppe.mask");

            var lamp = Prim.Cylinder("Ppe_Lamp", t, new Vector3(0f, 0.62f, -0.8f),
                new Vector3(0.16f, 0.26f, 0.16f), Amber, true);
            Tag(lamp, room, "gas_ppe", "lamp", "gas.ppe.lamp");

            var meter = Prim.Box("Ppe_Meter", t, new Vector3(0.6f, 0.60f, -0.8f),
                new Vector3(0.22f, 0.28f, 0.14f), Steel);
            Tag(meter, room, "gas_ppe", "meter", "gas.ppe.meter");

            var towel = Prim.Box("Ppe_Towel", t, new Vector3(1.25f, 0.54f, -0.8f),
                new Vector3(0.42f, 0.10f, 0.32f), White);
            Tag(towel, room, "gas_ppe", "towel", "gas.ppe.towel");

            // Buddy. The body is decoration; only the hitbox answers a tap, otherwise aiming at
            // the capsule would find a collider with no Interactable and the tap would die.
            var mohanBody = Prim.Capsule("Mohan", t, new Vector3(-2.7f, 0.9f, -1.8f),
                new Vector3(0.5f, 0.9f, 0.5f), Blue);
            Prim.StripColliders(mohanBody);

            var mohanHit = Hitbox("Mohan_Hit", t, new Vector3(-2.7f, 1.0f, -1.8f),
                new Vector3(0.8f, 2.0f, 0.8f));
            Tag(mohanHit, room, "gas_buddy", "mohan", "gas.buddy.mohan");

            var aloneSign = Prim.Box("AloneSign", t, new Vector3(2.7f, 1.2f, -1.8f),
                new Vector3(0.5f, 1.0f, 0.08f), Red, true);
            Tag(aloneSign, room, "gas_buddy", "alone", "gas.buddy.alone");

            // Pre-entry checks. Spread along the walls so each one needs a deliberate look.
            var fan = Prim.Box("Fan", t, new Vector3(4.4f, 2.7f, 2.4f),
                new Vector3(0.08f, 0.7f, 0.7f), Steel);
            Tag(fan, room, "gas_checks", "fan", "gas.checks.fan");

            var permit = Prim.Box("PermitBoard", t, new Vector3(-4.4f, 1.7f, 2.2f),
                new Vector3(0.08f, 0.5f, 0.7f), White, true);
            Tag(permit, room, "gas_checks", "permit", "gas.checks.permit");

            var panel = Prim.Box("MeterPanel", t, new Vector3(4.4f, 1.9f, 0.3f),
                new Vector3(0.08f, 0.5f, 0.8f), Dark);
            Tag(panel, room, "gas_checks", "meter", "gas.checks.meter");

            // The decision. Going in is a fail; refusing is a pass.
            // Placed together on the north wall so "door, then barrier" reads as one action.
            var doorway = Prim.Box("Doorway", t, new Vector3(1.9f, 1.15f, 3.42f),
                new Vector3(1.2f, 2.3f, 0.1f), Dark);
            Tag(doorway, room, "gas_high_meter", "enter", "gas.high.enter");

            var barrier = Prim.Box("NoEntryBarrier", t, new Vector3(3.25f, 1.15f, 3.42f),
                new Vector3(1.0f, 2.3f, 0.1f), Red, true);
            Tag(barrier, room, "gas_high_meter", "no_entry", "gas.high.noentry");

            Prim.PointLight("GasGlow", t, new Vector3(0f, 1.2f, 2.3f),
                new Color(1f, 0.72f, 0.25f), 8f, 1.5f);

            room.PlayerStart = new Vector3(0f, 1.6f, -2.9f);
            room.PlayerLookAt = new Vector3(0f, 1.2f, 2.0f);
        }

        // ---------------------------------------------------------------- helpers

        private static void Tag(GameObject target, BuiltRoom room, string stepId, string optionId, string labelKey)
        {
            var interactable = target.AddComponent<Interactable>();
            interactable.Bind(stepId, optionId, labelKey);
            room.Register(interactable);
        }

        /// <summary>Invisible collider so a flat decal (a ring, a person) can still be aimed at.</summary>
        private static GameObject Hitbox(string name, Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;

            var collider = go.AddComponent<BoxCollider>();
            collider.size = size;

            return go;
        }
    }
}
