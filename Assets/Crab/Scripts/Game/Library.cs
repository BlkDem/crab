// Everything the game spawns at run time, filled in by the editor's scene builder: the models
// made in Blender (Tools/blender), materials for effects, the fonts and styles of the interface.
using System;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    [CreateAssetMenu(menuName = "Crab/Library")]
    public sealed class Library : ScriptableObject
    {
        [Serializable]
        public sealed class Named
        {
            public string name;
            public GameObject model;
        }

        public GameObject hero;
        /// <summary>The crab's later forms by level (2..6; null where the form before goes on).</summary>
        public GameObject[] heroForms = new GameObject[0];
        public List<Named> items = new List<Named>();
        public GameObject shade, crawler, guardian, gull, morsel, stone;
        public GameObject imp, brute, burrower, colossus, armor, drone;
        public GameObject collector, goldGuard, king, heavyArmor, tank, coin;
        public GameObject dweller, envious, sparkCore;
        public GameObject wraith, devourer, cyberFrame, hunter;
        public GameObject engineer, mageOrb, turret, legionary, janus, guide;
        public GameObject battleBot, warlord;
        public GameObject liar, mimic;
        public GameObject hellLord, matterLord;
        public Material particle, particleAdditive, rift, beam, tracer, water, fire, gold, shield, mirror, bridge;
        public Mesh quad;

        public GameObject Item(ItemId id) => items.Find(i => i.name == id.ToString())?.model;

        static Library cached;

        /// <summary>The library the current scene was built with (its GameSession holds it).</summary>
        public static Library Current
        {
            get
            {
                if (cached) return cached;
                var holder = UnityEngine.Object.FindAnyObjectByType<SceneSetup>();
                cached = holder ? holder.library : null;
                return cached;
            }
            set => cached = value;
        }
    }
}
