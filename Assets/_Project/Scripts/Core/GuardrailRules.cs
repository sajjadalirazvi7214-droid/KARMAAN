using System.Collections.Generic;

namespace Karmaan.Core
{
    /// <summary>
    /// The six killer myths. Hard-coded on purpose: these are never content-managed,
    /// never overridable by score, and never localised away. Each one is a specific
    /// way people die in Jharkhand mines, steel plants and mica units.
    /// </summary>
    public static class GuardrailRules
    {
        public sealed class Guardrail
        {
            public string id;
            public string nameKey;
            public string blockedActionKey;
            public string correctionKey;
        }

        public const string WaterOnElectrical = "G1";
        public const string WaterOnOil = "G2";
        public const string SparkInGas = "G3";
        public const string LooseClothNearMachine = "G4";
        public const string TapeOnCrackedHelmet = "G5";
        public const string RunInClothFire = "G6";

        private static readonly Dictionary<string, Guardrail> Rules =
            new Dictionary<string, Guardrail>
            {
                {
                    WaterOnElectrical, new Guardrail
                    {
                        id = WaterOnElectrical,
                        nameKey = "guard.g1.name",
                        blockedActionKey = "guard.g1.blocked",
                        correctionKey = "guard.g1.fix"
                    }
                },
                {
                    WaterOnOil, new Guardrail
                    {
                        id = WaterOnOil,
                        nameKey = "guard.g2.name",
                        blockedActionKey = "guard.g2.blocked",
                        correctionKey = "guard.g2.fix"
                    }
                },
                {
                    SparkInGas, new Guardrail
                    {
                        id = SparkInGas,
                        nameKey = "guard.g3.name",
                        blockedActionKey = "guard.g3.blocked",
                        correctionKey = "guard.g3.fix"
                    }
                },
                {
                    LooseClothNearMachine, new Guardrail
                    {
                        id = LooseClothNearMachine,
                        nameKey = "guard.g4.name",
                        blockedActionKey = "guard.g4.blocked",
                        correctionKey = "guard.g4.fix"
                    }
                },
                {
                    TapeOnCrackedHelmet, new Guardrail
                    {
                        id = TapeOnCrackedHelmet,
                        nameKey = "guard.g5.name",
                        blockedActionKey = "guard.g5.blocked",
                        correctionKey = "guard.g5.fix"
                    }
                },
                {
                    RunInClothFire, new Guardrail
                    {
                        id = RunInClothFire,
                        nameKey = "guard.g6.name",
                        blockedActionKey = "guard.g6.blocked",
                        correctionKey = "guard.g6.fix"
                    }
                }
            };

        public static bool IsGuardrail(string id)
        {
            return !string.IsNullOrEmpty(id) && Rules.ContainsKey(id);
        }

        public static Guardrail Get(string id)
        {
            Guardrail g;
            return Rules.TryGetValue(id, out g) ? g : null;
        }

        public static IEnumerable<Guardrail> All => Rules.Values;
    }
}
