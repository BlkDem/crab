// Things the crab can use with E: draw matter out of debris, eat, light a beacon, seal a rift.
// Some are used at once, others are channelled while the key is held.
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();
        public float reach = 2.4f;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public abstract string Prompt { get; }
        public virtual bool Usable => true;
        /// <summary>Channelled while held (true) or used once on press.</summary>
        public virtual bool Hold => false;
        /// <summary>Once, on press (Hold false).</summary>
        public virtual void Use(Hero hero) { }
        /// <summary>Every frame while held; return true when finished.</summary>
        public virtual bool Channel(Hero hero, float dt) => true;
        public virtual void StopChannel(Hero hero) { }
        /// <summary>Where streams of matter come from.</summary>
        public virtual Vector3 Point => transform.position + Vector3.up * 0.4f;
    }
}
