// A way to look at any circle without playing up to it: the title screen's "Отладка" starts a
// chapter with what the crab would carry into it by then (the circles before done, their
// blueprints, weapons, armour, companions; plenty of matter). In the game F7 / F8 take the crab a
// level down / up (the scene starts over with its new form), F6 goes to the next chapter.
// The debug playthrough keeps its own save, so the real one is never touched.
using System.IO;
using Crab.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Crab.Game
{
    public static class DebugMode
    {
        public static bool On { get; private set; }

        public static readonly string[] Chapters = { "Prologue", "Limbo", "Wrath", "Greed", "Envy", "Gloom", "Betrayal", "Violence", "Lies", "Power", "Epilogue" };

        // what each chapter gives the crab (blueprints, and the things made from them it carries on)
        static readonly ItemId[][] Gains =
        {
            new[] { ItemId.Stick },
            new[] { ItemId.Stone, ItemId.Crowbar, ItemId.Knife, ItemId.Pistol, ItemId.Ammo },
            new[] { ItemId.MachineGun, ItemId.ScrapArmor, ItemId.ScoutDrone },
            new[] { ItemId.HeavyArmor, ItemId.EnergyRifle, ItemId.EnergyCell, ItemId.EnergyShield, ItemId.Tank },
            new[] { ItemId.SparkCore, ItemId.EnergyBlade, ItemId.GravityGun, ItemId.Portal },
            new[] { ItemId.CyberFrame, ItemId.Hunter },
            new[] { ItemId.Constructor, ItemId.Engineer, ItemId.MageOrb, ItemId.SquadPlating, ItemId.SquadFire },
            new[] { ItemId.Factory },
            new[] { ItemId.TruthLens, ItemId.Bridgework },
            new[] { ItemId.MatterLord },
        };

        /// <summary>The things that make the crab's levels 2..9 (GameState.Level).</summary>
        static readonly ItemId[] LevelItems = { ItemId.ScrapArmor, ItemId.HeavyArmor, ItemId.SparkCore, ItemId.CyberFrame, ItemId.Constructor, ItemId.Factory, ItemId.Bridgework, ItemId.MatterLord };

        static string realSave;
        static string SavePath => Path.Combine(Application.persistentDataPath, "debug-save.json");

        /// <summary>Back to the real save (the title screen).</summary>
        public static void Leave()
        {
            if (!On) return;
            On = false;
            GameSession.SavePathOverride = realSave;
        }

        /// <summary>Start a chapter as if the crab had come to it.</summary>
        public static void Start(string scene)
        {
            if (!On) realSave = GameSession.SavePathOverride;
            On = true;
            GameSession.SavePathOverride = SavePath;
            var session = GameSession.Ensure();
            session.ResetState();
            Prepare(session.State, System.Array.IndexOf(Chapters, scene));
            if (!session.GetComponent<DebugKeys>()) session.gameObject.AddComponent<DebugKeys>();
            session.Go(scene);
        }

        /// <summary>The state of a crab entering chapter `at` (0 the prologue … 10 the end).</summary>
        public static void Prepare(GameState st, int at)
        {
            if (at <= 0) return;
            st.SetFlag("spark");
            for (int c = 1; c < at && c <= 9; c++) st.SetFlag($"circle{c}_done");
            if (at >= 10) st.SetFlag("game_done");
            for (int c = 0; c < at && c < Gains.Length; c++)
                foreach (var id in Gains[c])
                {
                    st.Learn(id);
                    st.Items.Add(id, st.Items.Limit(id));
                }
            st.Matter.Set(60, 60, 60, 60, 60, 60);
        }

        /// <summary>Make the crab this level (1..9): the things of the levels above go, those below come.</summary>
        public static void SetLevel(GameState st, int level)
        {
            level = Mathf.Clamp(level, 1, 9);
            for (int i = 0; i < LevelItems.Length; i++)
            {
                var id = LevelItems[i];
                bool want = i + 2 <= level;
                if (want && !st.Items.Has(id)) { st.Learn(id); st.Items.Add(id); }
                else if (!want && st.Items.Has(id)) st.Items.Take(id, st.Items.Count(id));
            }
        }

        sealed class DebugKeys : MonoBehaviour
        {
            void Update()
            {
                var k = Keyboard.current;
                var session = GameSession.Instance;
                if (!On || k == null || !session || session.Changing || session.State.Scene == "Menu") return;
                var st = session.State;
                if (k.f7Key.wasPressedThisFrame || k.f8Key.wasPressedThisFrame)
                {
                    SetLevel(st, st.Level + (k.f8Key.wasPressedThisFrame ? 1 : -1));
                    session.Go(st.Scene, 0.2f);
                }
                else if (k.f6Key.wasPressedThisFrame)
                {
                    int i = System.Array.IndexOf(Chapters, st.Scene);
                    Start(Chapters[(i + 1) % Chapters.Length]);
                }
            }
        }
    }
}
