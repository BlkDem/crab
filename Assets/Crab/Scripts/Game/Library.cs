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
        public List<Named> items = new List<Named>();
        public GameObject shade, crawler, guardian, gull, morsel, stone;
        public Material particle, particleAdditive, rift, beam, tracer, water;
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
