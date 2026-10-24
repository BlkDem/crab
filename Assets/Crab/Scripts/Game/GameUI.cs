// The interface during play (UI Toolkit, built in code; styles in UI/Crab.uss): health and matter,
// the task, the weapon in the claw, the crab's thoughts and hints, the prompt for what is in
// reach, a crosshair when aiming, the boss's bar, title cards, flashes and fades, a red edge
// when hurt; the circle of creation (Tab) and the pause menu (Esc).
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Crab.Game
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameUI : MonoBehaviour
    {
        VisualElement root, hpFill, objective, thought, hint, prompt, crosshair, boss, bossFill, titleCard, flash, fadeEl, vignette, create, ring, pause, slots;
        Label hpText, objText, thoughtText, hintText, promptText, weaponName, weaponCount, bossName, titleMain, titleSub, createSub;
        readonly Dictionary<Matter, Label> matterLabels = new Dictionary<Matter, Label>();
        readonly Dictionary<Matter, VisualElement> matterChips = new Dictionary<Matter, VisualElement>();
        Label shieldText;
        IVisualElementScheduledItem thoughtTimer, hintTimer, titleTimer;
        float flashA, flashFade = 1, hurtA;
        Color flashColor = Color.white;
        Hero hero;
        IBoss guardian;
        Label levelText;
        float lastHp = -1;

        static VisualElement E(string cls = null, VisualElement parent = null)
        {
            var e = new VisualElement();
            if (cls != null) foreach (var c in cls.Split(' ')) e.AddToClassList(c);
            parent?.Add(e);
            return e;
        }

        static Label L(string text, string cls = null, VisualElement parent = null)
        {
            var l = new Label(text);
            if (cls != null) foreach (var c in cls.Split(' ')) l.AddToClassList(c);
            parent?.Add(l);
            return l;
        }

        static Button B(string text, string cls, System.Action click, VisualElement parent)
        {
            var b = new Button(click) { text = text };
            foreach (var c in cls.Split(' ')) b.AddToClassList(c);
            parent.Add(b);
            return b;
        }

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.AddToClassList("screen");
            root.pickingMode = PickingMode.Ignore;

            vignette = E("overlay vignette", root);
            // status
            var status = E("status", root);
            var hpRow = E("hp-row", status);
            var heart = E("hp-icon", hpRow);
            heart.generateVisualContent += DrawHeart;
            var bar = E("hp-bar", hpRow);
            hpFill = E("hp-fill", bar);
            hpText = L("", "hp-text", hpRow);
            levelText = L("", "level", status);
            var mrow = E("matter-row", status);
            foreach (var m in MatterStore.Kinds)
            {
                var chip = E("matter", mrow);
                var dot = E("dot", chip);
                dot.style.backgroundColor = MatterSource.ColorOf(m);
                matterLabels[m] = L("0", null, chip);
                matterChips[m] = chip;
                L(MatterStore.Title(m).ToLower(), "name", chip);
            }
            // task
            objective = E("objective", root);
            L("ЗАДАЧА", "head", objective);
            objText = L("", "text", objective);
            // weapon
            var weapon = E("weapon", root);
            weaponName = L("", "name", weapon);
            weaponCount = L("", "count", weapon);
            shieldText = L("", "count", weapon);
            slots = E("slots", weapon);
            // texts
            thought = E("thought", root);
            thoughtText = L("", null, thought);
            hint = E("hint", root);
            hintText = L("", null, hint);
            prompt = E("prompt", root);
            promptText = L("", null, prompt);
            crosshair = E("crosshair", root);
            crosshair.generateVisualContent += DrawCrosshair;
            // boss
            boss = E("boss", root);
            bossName = L("СТРАЖ ЛИМБА", "name", boss);
            var bb = E("bar", boss);
            bossFill = E("fill", bb);
            // title
            titleCard = E("titlecard", root);
            titleMain = L("", "main", titleCard);
            titleSub = L("", "sub", titleCard);
            // creation
            create = E("create", root);
            ring = E("ring", create);
            var center = E("center", ring);
            L("СОЗДАТЬ", "head", center);
            createSub = L("", "sub", center);
            // pause
            pause = BuildPause();
            // overlays on top
            flash = E("overlay", root);
            flash.pickingMode = PickingMode.Ignore;
            fadeEl = E("overlay", root);
            fadeEl.style.backgroundColor = Color.black;
            fadeEl.pickingMode = PickingMode.Ignore;
            foreach (var el in new[] { vignette, status, objective, weapon, thought, hint, prompt, crosshair, boss, titleCard }) el.pickingMode = PickingMode.Ignore;

            Story.OnThought += Thought;
            Story.OnObjective += Objective;
            Story.OnHint += Hint;
            Story.OnTitle += Title;
            Story.OnFlash += Flash;
            Objective(Story.Objective);
        }

        void OnDisable()
        {
            Story.OnThought -= Thought;
            Story.OnObjective -= Objective;
            Story.OnHint -= Hint;
            Story.OnTitle -= Title;
            Story.OnFlash -= Flash;
        }

        void Start()
        {
            hero = Hero.Instance;
            guardian = FindObjectsByType<MonoBehaviour>().OfType<IBoss>().FirstOrDefault();
            if (hero)
            {
                hero.Power.MenuToggled += ShowCreate;
                hero.State.Health.Damaged += _ => hurtA = 0.8f;
            }
        }

        // ------------------------------------------------------------------ messages
        void Thought(string text, float seconds)
        {
            thoughtText.text = text;
            thought.AddToClassList("show");
            thoughtTimer?.Pause();
            thoughtTimer = thought.schedule.Execute(() => thought.RemoveFromClassList("show")).StartingIn((long)(seconds * 1000));
        }

        void Objective(string text)
        {
            objText.text = text;
            objective.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void Hint(string text)
        {
            hintText.text = text;
            hint.AddToClassList("show");
            hintTimer?.Pause();
            hintTimer = hint.schedule.Execute(() => hint.RemoveFromClassList("show")).StartingIn(4500);
        }

        void Title(string main, string sub)
        {
            titleMain.text = main;
            titleSub.text = sub;
            titleCard.AddToClassList("show");
            titleTimer?.Pause();
            titleTimer = titleCard.schedule.Execute(() => titleCard.RemoveFromClassList("show")).StartingIn(3500);
        }

        void Flash(Color c, float seconds)
        {
            flashColor = c;
            flashA = 1;
            flashFade = Mathf.Max(0.05f, seconds);
        }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var s = GameSession.Instance;
            fadeEl.style.opacity = s ? s.fade : 0;
            flashA = Mathf.MoveTowards(flashA, 0, dt / flashFade);
            flash.style.backgroundColor = new Color(flashColor.r, flashColor.g, flashColor.b, flashA);
            hurtA = Mathf.MoveTowards(hurtA, 0, dt * 1.5f);
            if (!hero || !s) return;
            var st = s.State;
            float hp = st.Health.Value;
            vignette.style.opacity = Mathf.Max(hurtA, hp < 30 ? 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 4) : 0);
            if (!Mathf.Approximately(hp, lastHp))
            {
                lastHp = hp;
                hpFill.style.width = Length.Percent(100 * hp / st.Health.Max);
                hpText.text = $"{Mathf.CeilToInt(hp)}";
            }
            foreach (var kv in matterLabels) kv.Value.text = st.Matter[kv.Key].ToString();
            foreach (var kv in matterChips) kv.Value.style.display = Shown(st, kv.Key) ? DisplayStyle.Flex : DisplayStyle.None;
            var sh = hero.Shield;
            shieldText.text = !sh ? "" : sh.Active ? "ЩИТ АКТИВЕН" : sh.Cooldown > 0 ? $"щит [Q]: {Mathf.CeilToInt(sh.Cooldown)} с" : "щит [Q]: готов";
            var pt = hero.Portal;
            if (pt) shieldText.text += (shieldText.text.Length > 0 ? "\n" : "") + (pt.Cooldown > 0 ? $"портал [F]: {Mathf.CeilToInt(pt.Cooldown)} с" : "портал [F]: готов");
            levelText.text = st.Flag("spark") ? $"Уровень {st.Level} — {GameState.LevelTitle(st.Level)}" : "";
            var eq = Blueprints.Get(st.Items.Equipped);
            weaponName.text = eq != null ? eq.title : st.Flag("spark") ? "Голые клешни" : "";
            weaponCount.text = eq?.kind == WeaponKind.Gun ? (eq.ammo == ItemId.EnergyCell ? $"ячейки: {st.Items.Count(ItemId.EnergyCell)}" : $"патроны: {st.Items.Count(ItemId.Ammo)}") : eq?.kind == WeaponKind.Throw ? $"камней: {st.Items.Count(ItemId.Stone)}" : "";
            UpdateSlots(st);
            promptText.text = Story.Prompt ?? "";
            prompt.style.display = string.IsNullOrEmpty(Story.Prompt) ? DisplayStyle.None : DisplayStyle.Flex;
            bool aim = hero.Combat && (hero.Combat.Aiming || hero.Combat.firstPerson);
            crosshair.style.display = aim ? DisplayStyle.Flex : DisplayStyle.None;
            bool fight = guardian != null && guardian.Fighting;
            if (fight) bossName.text = guardian.Title;
            boss.style.display = fight ? DisplayStyle.Flex : DisplayStyle.None;
            if (fight) bossFill.style.width = Length.Percent(100 * guardian.Health01);
            if (create.style.display == DisplayStyle.Flex) RefreshCreate();
            if (Controls.Pause.WasPressedThisFrame())
            {
                if (hero.Power.MenuOpen) hero.Power.SetMenu(false);
                else TogglePause();
            }
        }

        string slotsKey;

        void UpdateSlots(GameState st)
        {
            var list = st.Items.Weapons.ToList();
            string key = string.Join(",", list) + "|" + st.Items.Equipped;
            if (key == slotsKey) return;
            slotsKey = key;
            slots.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var sl = E("slot" + (list[i] == st.Items.Equipped ? " on" : ""), slots);
                L($"{i + 1}", null, sl);
                sl.tooltip = Blueprints.Get(list[i]).title;
            }
        }

        /// <summary>Gold, crystals and energy appear on the panel once the crab has met them.</summary>
        static bool Shown(GameState st, Matter m) => MatterStore.Basic(m) || st.Matter[m] > 0 || st.Flag("met_" + m);

        // ------------------------------------------------------------------ creation
        void ShowCreate(bool open)
        {
            create.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) BuildCreate();
        }

        readonly List<(VisualElement el, Blueprint bp, Label have)> bps = new List<(VisualElement, Blueprint, Label)>();

        void BuildCreate()
        {
            foreach (var (el, _, _) in bps) el.RemoveFromHierarchy();
            bps.Clear();
            // what the crab knows, and a couple of shapes still to be understood
            var st0 = hero.State;
            int unknown = 0;
            var all = Blueprints.All.Where(b => st0.Knows(b.id) || unknown++ < 2).ToList();
            float radius = all.Count > 10 ? 235 : 205;
            for (int i = 0; i < all.Count; i++)
            {
                var bp = all[i];
                float a = -Mathf.PI / 2 + i * Mathf.PI * 2 / all.Count;
                var el = E("bp", ring);
                el.style.left = 280 + Mathf.Cos(a) * radius;
                el.style.top = 280 + Mathf.Sin(a) * radius;
                var name = L(bp.title, "name", el);
                var cost = L(bp.cost.ToString(), "cost", el);
                var have = L("", "have", el);
                var b = bp;
                el.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!hero.State.Knows(b.id)) return;
                    hero.Power.Create(b.id);
                    RefreshCreate();
                });
                bps.Add((el, bp, have));
            }
            RefreshCreate();
        }

        void RefreshCreate()
        {
            var st = hero.State;
            foreach (var (el, bp, have) in bps)
            {
                bool known = st.Knows(bp.id);
                el.EnableInClassList("locked", !known);
                el.EnableInClassList("cannot", known && (!st.Matter.CanPay(st.Mage ? st.FromEnergy(bp.cost) : bp.cost) || st.Items.Full(bp.id) || (bp.workshop && !st.AtWorkshop)));
                el.Q<Label>(className: "name").text = known ? bp.title : "???";
                el.Q<Label>(className: "cost").text = !known ? "ещё не понял, как это делать" : bp.workshop && !st.AtWorkshop ? $"{bp.cost}\nтолько в мастерской" : bp.cost.ToString();
                int n = st.Items.Count(bp.id);
                have.text = !known ? "" : bp.stack ? $"есть: {n}" : n > 0 ? "есть" : "";
            }
            createSub.text = string.Join(" · ", MatterStore.Kinds.Where(m => Shown(st, m)).Select(m => $"{MatterStore.Title(m).ToLower()} {st.Matter[m]}")) + "\nклик — создать, Tab — закрыть";
        }

        // ------------------------------------------------------------------ pause
        VisualElement BuildPause()
        {
            var m = E("menu", root);
            var box = E("menu-box", m);
            L("ПАУЗА", "menu-title", box);
            B("Продолжить", "menu-btn", TogglePause, box);
            Slider(box, "Громкость", Sfx.volume, v => { Sfx.volume = v; PlayerPrefs.SetFloat("crab.volume", v); });
            Slider(box, "Музыка", Sfx.music, v => { Sfx.music = v; PlayerPrefs.SetFloat("crab.music", v); });
            Slider(box, "Чувствительность мыши", Controls.sensitivity / 2, v => { Controls.sensitivity = Mathf.Max(0.1f, v * 2); PlayerPrefs.SetFloat("crab.sens", Controls.sensitivity); });
            B("Сохранить и выйти в меню", "menu-btn", () => { TogglePause(); GameSession.Instance.Save(); GameSession.Instance.Go("Menu"); }, box);
            L("WASD — идти · мышь — смотреть · Shift — бежать · Пробел — прыжок\nЛКМ — удар/выстрел · ПКМ — прицел · E — втянуть/использовать · Tab — создать · 1–9, X/Z — оружие · Q — щит · F — портал", "menu-note", box);
            return m;
        }

        static void Slider(VisualElement parent, string title, float value, System.Action<float> changed)
        {
            var row = E("menu-row", parent);
            L(title, null, row);
            var s = new Slider(0, 1) { value = value };
            s.RegisterValueChangedCallback(e => changed(e.newValue));
            row.Add(s);
        }

        public bool Paused { get; private set; }

        void TogglePause()
        {
            Paused = !Paused;
            pause.style.display = Paused ? DisplayStyle.Flex : DisplayStyle.None;
            Time.timeScale = Paused ? 0 : 1;
            UnityEngine.Cursor.lockState = Paused ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = Paused;
            AudioListener.pause = Paused;
        }

        // ------------------------------------------------------------------ icons
        static void DrawHeart(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = ctx.visualElement.contentRect;
            float s = r.width / 24;
            p.fillColor = new Color(1f, 0.45f, 0.2f);
            p.BeginPath();
            p.MoveTo(new Vector2(12, 21) * s);
            p.BezierCurveTo(new Vector2(4, 15) * s, new Vector2(1, 10) * s, new Vector2(4, 6) * s);
            p.BezierCurveTo(new Vector2(7, 2) * s, new Vector2(11, 4) * s, new Vector2(12, 7) * s);
            p.BezierCurveTo(new Vector2(13, 4) * s, new Vector2(17, 2) * s, new Vector2(20, 6) * s);
            p.BezierCurveTo(new Vector2(23, 10) * s, new Vector2(20, 15) * s, new Vector2(12, 21) * s);
            p.ClosePath();
            p.Fill();
        }

        static void DrawCrosshair(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = ctx.visualElement.contentRect;
            var c = r.center;
            p.strokeColor = new Color(1, 1, 1, 0.85f);
            p.lineWidth = 2;
            foreach (var d in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                p.BeginPath();
                p.MoveTo(c + d * 4);
                p.LineTo(c + d * 10);
                p.Stroke();
            }
            p.fillColor = new Color(1f, 0.6f, 0.2f);
            p.BeginPath();
            p.Arc(c, 1.6f, Angle.Degrees(0), Angle.Degrees(360));
            p.Fill();
        }
    }
}
