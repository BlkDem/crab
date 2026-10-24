// The crab's gift from the Spark: sensing the matter around it, drawing it in (hold E at a
// source), and making things of it (Tab opens the circle of blueprints; the item gathers out
// of glowing motes in the claw). Also the plain uses of E: eat, light a beacon, seal a rift.
using System;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class MatterPower : MonoBehaviour
    {
        public Hero hero;
        [System.NonSerialized] public bool locked;
        /// <summary>The power works only after the Spark (the prologue's ordinary crab can only eat).</summary>
        public bool Awakened => hero.State.Flag("spark");
        public bool MenuOpen { get; private set; }
        public event Action<bool> MenuToggled;
        public event Action<ItemId> Created;

        Interactable current, channel;
        float createT;

        public Interactable Current => current;

        void Update()
        {
            if (Time.timeScale == 0 && !MenuOpen) return;
            if (locked || !hero.Alive)
            {
                StopChannel();
                Story.Prompt = null;
                if (MenuOpen) SetMenu(false);
                return;
            }
            createT -= Time.unscaledDeltaTime;
            if (Controls.Create.WasPressedThisFrame() && Awakened) SetMenu(!MenuOpen);
            if (MenuOpen) { Story.Prompt = null; return; }

            current = Nearest();
            Story.Prompt = current ? current.Prompt : null;
            if (channel)
            {
                if (!Controls.Interact.IsPressed() || !channel.Usable || Vector3.Distance(channel.transform.position, transform.position) > channel.reach + 1.5f || channel.Channel(hero, Time.deltaTime))
                    StopChannel();
                return;
            }
            if (current && Controls.Interact.WasPressedThisFrame())
            {
                if (current.Hold) StartChannel(current);
                else current.Use(hero);
            }
        }

        Interactable Nearest()
        {
            Interactable best = null;
            float bd = float.MaxValue;
            var p = transform.position;
            var fwd = transform.forward;
            foreach (var i in Interactable.All)
            {
                if (!i || !i.isActiveAndEnabled || !i.Usable) continue;
                if (i is MatterSource && !Awakened) continue;   // an ordinary crab cannot draw matter
                var d = i.transform.position - p;
                d.y = 0;
                float dist = d.magnitude;
                if (dist > i.reach) continue;
                // prefer what is in front of the crab
                float score = dist - Vector3.Dot(fwd, d.normalized) * 0.5f;
                if (score < bd) { bd = score; best = i; }
            }
            return best;
        }

        void StartChannel(Interactable i)
        {
            channel = i;
            hero.Rig.Hold(ClawPose.Absorb);
            hero.Controller.speedScale = 0.25f;
            hero.Rig.lookAt = i.Point;
        }

        void StopChannel()
        {
            if (!channel) return;
            channel.StopChannel(hero);
            channel = null;
            hero.Rig.Release(ClawPose.Absorb);
            hero.Controller.speedScale = 1;
            hero.Rig.lookAt = null;
        }

        public void SetMenu(bool open)
        {
            if (open == MenuOpen) return;
            MenuOpen = open;
            Time.timeScale = open ? 0.2f : 1;
            hero.Cam.inputLocked = open;
            UnityEngine.Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = open;
            if (open) hero.Rig.Hold(ClawPose.Create); else hero.Rig.Release(ClawPose.Create);
            MenuToggled?.Invoke(open);
        }

        /// <summary>Make an item (the menu calls this); explains why not if it cannot.</summary>
        public GameState.CreateResult Create(ItemId id)
        {
            var st = hero.State;
            var bp = Blueprints.Get(id);
            var r = st.Create(id);
            switch (r)
            {
                case GameState.CreateResult.NoMatter:
                    Story.Hint($"Не хватает материи: нужно {bp.cost}");
                    Sfx.Play("click", null, 1, 0.5f);
                    break;
                case GameState.CreateResult.NeedWorkshop:
                    Story.Hint($"{bp.title}: это собирается только в мастерской");
                    Sfx.Play("click", null, 1, 0.5f);
                    break;
                case GameState.CreateResult.Full:
                    Story.Hint(bp.stack ? $"{bp.title}: больше не унести" : $"{bp.title} уже есть");
                    break;
                case GameState.CreateResult.Done:
                {
                    var at = hero.Rig.gripRight ? hero.Rig.gripRight : transform;
                    Fx.Assemble(at, new Color(1f, 0.85f, 0.45f), 0.7f);
                    Fx.Flash(at.position, new Color(1f, 0.85f, 0.5f), 2.5f, 3, 0.4f);
                    Sfx.Play("create", at.position, 0.8f);
                    if (bp.kind != WeaponKind.None) st.Items.Equip(id);
                    if (st.SetFlag("made_" + id)) Story.Think($"{bp.title}. {bp.hint}", 4);
                    Created?.Invoke(id);
                    break;
                }
            }
            return r;
        }
    }
}
