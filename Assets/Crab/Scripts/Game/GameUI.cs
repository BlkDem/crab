// The interface during play (UI Toolkit, built in code; styles in UI/Crab.uss): health and matter,
// the task, the weapon in the claw, the crab's thoughts and hints, the prompt for what is in
// reach, a crosshair when aiming, the boss's bar, title cards, flashes and fades, a red edge
// when hurt; the circle of creation (Tab), the pause menu (Esc), the map (corner, and M).
using System.Collections;
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
        VisualElement root, settings, pauseBox, hpFill, objective, thought, hint, prompt, crosshair, boss, bossFill, titleCard, flash, fadeEl, vignette, create, ring, pause, slots;
        Label hpText, objText, thoughtText, hintText, promptText, weaponName, weaponCount, bossName, titleMain, titleSub, createSub;
        readonly Dictionary<Matter, Label> matterLabels = new Dictionary<Matter, Label>();
        readonly Dictionary<Matter, VisualElement> matterChips = new Dictionary<Matter, VisualElement>();
        Label shieldText;
        IVisualElementScheduledItem thoughtTimer, hintTimer, titleTimer, whisperTimer, aiTimer;
        VisualElement whisper, ai, willRow, willFill;
        Label whisperText, aiText;
        float flashA, flashFade = 1, hurtA;
        Color flashColor = Color.white;
        Hero hero;
        IBoss guardian;
        Label levelText;
        float lastHp = -1;
        /// <summary>A film moment: only the words stay on screen (the end).</summary>
        public static bool Cinema;

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
            willRow = E("will-row", status);
            L("воля", "will-name", willRow);
            var wb = E("will-bar", willRow);
            willFill = E("will-fill", wb);
            willRow.style.display = DisplayStyle.None;
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
            whisper = E("whisper", root);
            whisperText = L("", null, whisper);
            ai = E("ai", root);
            L("ИИ", "ai-head", ai);
            aiText = L("", "ai-text", ai);
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
            BuildMaps();
            goal = E("goal", root);
            goal.pickingMode = PickingMode.Ignore;
            E("goal-mark", goal).pickingMode = PickingMode.Ignore;
            goalText = L("", "goal-text", goal);
            fps = L("", "fps", root);
            fps.pickingMode = PickingMode.Ignore;
            // overlays on top
            flash = E("overlay", root);
            flash.pickingMode = PickingMode.Ignore;
            fadeEl = E("overlay", root);
            fadeEl.style.backgroundColor = Color.black;
            fadeEl.pickingMode = PickingMode.Ignore;
            foreach (var el in new[] { vignette, status, objective, weapon, thought, hint, prompt, crosshair, boss, titleCard, whisper, ai }) el.pickingMode = PickingMode.Ignore;
            Story.OnWhisper += Whisper;
            Story.OnAI += Ai;

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
            Story.OnWhisper -= Whisper;
            Story.OnAI -= Ai;
            Story.OnObjective -= Objective;
            Story.OnHint -= Hint;
            Story.OnTitle -= Title;
            Story.OnFlash -= Flash;
        }

        void Start()
        {
            hero = Hero.Instance;
            StartCoroutine(TakeMap());
            guardian = FindObjectsByType<MonoBehaviour>().OfType<IBoss>().FirstOrDefault();
            if (hero)
            {
                hero.Power.MenuToggled += ShowCreate;
                hero.State.Health.Damaged += _ => hurtA = 0.8f;
            }
        }

        // ------------------------------------------------------------------ messages
        void Whisper(string text)
        {
            whisperText.text = text;
            whisper.style.left = Length.Percent(UnityEngine.Random.Range(10f, 55f));
            whisper.style.top = Length.Percent(UnityEngine.Random.Range(18f, 40f));
            whisper.AddToClassList("show");
            whisperTimer?.Pause();
            whisperTimer = whisper.schedule.Execute(() => whisper.RemoveFromClassList("show")).StartingIn(2600);
        }

        void Ai(string text, float seconds)
        {
            aiText.text = text;
            ai.AddToClassList("show");
            aiTimer?.Pause();
            aiTimer = ai.schedule.Execute(() => ai.RemoveFromClassList("show")).StartingIn((long)(seconds * 1000));
        }

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
            root.EnableInClassList("cinema", Cinema);
            if (hero) UpdateGoal();
            fpsTime += dt;
            fpsFrames++;
            if (fpsTime > 0.5f)
            {
                fps.text = Settings.ShowFps ? $"{fpsFrames / fpsTime:0} к/с · {1000 * fpsTime / fpsFrames:0.0} мс" : "";
                fpsTime = 0;
                fpsFrames = 0;
            }
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
            var despair = Despair.Instance;
            willRow.style.display = despair ? DisplayStyle.Flex : DisplayStyle.None;
            if (despair) willFill.style.width = Length.Percent(despair.Will);
            foreach (var kv in matterChips) kv.Value.style.display = Shown(st, kv.Key) ? DisplayStyle.Flex : DisplayStyle.None;
            var sh = hero.Shield;
            shieldText.text = !sh ? "" : sh.Active ? "ЩИТ АКТИВЕН" : sh.Cooldown > 0 ? $"щит [Q]: {Mathf.CeilToInt(sh.Cooldown)} с" : "щит [Q]: готов";
            var pt = hero.Portal;
            if (pt) shieldText.text += (shieldText.text.Length > 0 ? "\n" : "") + (pt.Cooldown > 0 ? $"портал [F]: {Mathf.CeilToInt(pt.Cooldown)} с" : "портал [F]: готов");
            var ln = hero.Lens;
            if (ln) shieldText.text += (shieldText.text.Length > 0 ? "\n" : "") + (ln.On ? "ВЗГЛЯД ИСТИНЫ" : ln.Cooldown > 0 ? $"истина [T]: {Mathf.CeilToInt(ln.Cooldown)} с" : "истина [T]: готов");
            if (hero.Bridges) shieldText.text += (shieldText.text.Length > 0 ? "\n" : "") + "мост [B]";
            var lord = hero.Lord;
            if (lord) shieldText.text += (shieldText.text.Length > 0 ? "\n" : "") + (lord.Cooldown > 0 ? $"буря [R]: {Mathf.CeilToInt(lord.Cooldown)} с" : "буря материи [R]: готова");
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
            if (Controls.Map.WasPressedThisFrame() && !Paused && !hero.Power.MenuOpen) ShowBigMap(!bigMapOpen);
            UpdateMaps();
            if (Controls.Pause.WasPressedThisFrame())
            {
                if (bigMapOpen) ShowBigMap(false);
                else if (hero.Power.MenuOpen) hero.Power.SetMenu(false);
                else if (settings != null) CloseSettings();
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
            var box = pauseBox = E("menu-box", m);
            L("ПАУЗА", "menu-title", box);
            B("Продолжить", "menu-btn", TogglePause, box);
            B("Настройки", "menu-btn", OpenSettings, box);
            B("Карта (M)", "menu-btn", () => { TogglePause(); ShowBigMap(true); }, box);
            B("Сохранить и выйти в меню", "menu-btn", () => { TogglePause(); GameSession.Instance.Save(); GameSession.Instance.Go("Menu"); }, box);
            L("WASD — идти · мышь — смотреть · Shift — бежать · Пробел — прыжок (держать в воздухе — парить)\nЛКМ — удар/выстрел · ПКМ — прицел · E — втянуть/использовать · Tab — создать · 1–9, X/Z — оружие\nQ — щит · F — портал · G/H/J — приказы отряду · T — взгляд истины · B — мост · R — буря материи", "menu-note", box);
            return m;
        }

        // ------------------------------------------------------------------ the map
        VisualElement mini, miniImg, big, bigImg, miniPlayer, bigPlayer;
        readonly List<VisualElement> miniMarks = new List<VisualElement>(), bigMarks = new List<VisualElement>();
        bool bigMapOpen;
        const float MiniSize = 180, MiniSpan = 80;   // the small map shows 80 m across

        IEnumerator TakeMap()
        {
            yield return null;
            yield return null;   // the crab is at its beacon by now
            WorldMap.Take(hero.transform.position);
            if (!WorldMap.Ready) yield break;
            var bg = new StyleBackground(Background.FromRenderTexture(WorldMap.Picture));
            miniImg.style.backgroundImage = bg;
            bigImg.style.backgroundImage = bg;
        }

        void BuildMaps()
        {
            mini = E("minimap", root);
            mini.pickingMode = PickingMode.Ignore;
            miniImg = E("map-img", mini);
            miniPlayer = E("map-player", mini);
            L("M — карта", "minimap-hint", mini);
            big = E("bigmap", root);
            var frame = E("bigmap-frame", big);
            bigImg = E("map-img", frame);
            bigImg.style.width = Length.Percent(100);
            bigImg.style.height = Length.Percent(100);
            bigPlayer = E("map-player", frame);
            var legend = E("bigmap-legend", big);
            foreach (var (cls, text) in new[] { ("goal", "цель"), ("exit", "выход"), ("rift", "разлом"), ("beacon", "маяк") })
            {
                var row = E("legend-row", legend);
                E("map-mark " + cls, row).AddToClassList("static");
                L(text, null, row);
            }
            L("M или Esc — закрыть", "bigmap-hint", big);
            big.style.display = DisplayStyle.None;
            mini.style.display = DisplayStyle.None;
        }

        public void ShowBigMap(bool on)
        {
            bigMapOpen = on && WorldMap.Ready;
            big.style.display = bigMapOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string MarkClass(WorldMap.Mark m) => m switch
        {
            WorldMap.Mark.Goal => "goal",
            WorldMap.Mark.Exit => "exit",
            WorldMap.Mark.Rift => "rift",
            WorldMap.Mark.Beacon => "beacon",
            _ => "boss",
        };

        static VisualElement Pooled(List<VisualElement> pool, VisualElement parent, int i)
        {
            while (pool.Count <= i) { var m = E("map-mark", parent); m.pickingMode = PickingMode.Ignore; pool.Add(m); }
            pool[i].style.display = DisplayStyle.Flex;
            return pool[i];
        }

        void UpdateMaps()
        {
            bool ready = WorldMap.Ready && !Cinema;
            mini.style.display = ready && !bigMapOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!ready) { if (bigMapOpen) ShowBigMap(false); return; }
            var area = WorldMap.Area;
            var me = WorldMap.ToMap(hero.transform.position);
            float yaw = hero.Cam ? hero.Cam.Yaw : hero.transform.eulerAngles.y;
            var marks = WorldMap.Marks().ToList();
            // the small one: the picture moves under the crab, which stays in the middle
            float px = MiniSize * area.width / MiniSpan;
            miniImg.style.width = px;
            miniImg.style.height = px;
            miniImg.style.left = MiniSize / 2 - me.x * px;
            miniImg.style.top = MiniSize / 2 - me.y * px;
            miniPlayer.style.rotate = new Rotate(yaw);
            int n = 0;
            foreach (var (kind, at) in marks)
            {
                var d = (WorldMap.ToMap(at) - me) * px;
                float r = MiniSize / 2 - 8;
                if (d.magnitude > r)
                {
                    if (kind != WorldMap.Mark.Goal && kind != WorldMap.Mark.Exit) continue;
                    d = d.normalized * r;   // at the rim, towards it
                }
                var m = Pooled(miniMarks, mini, n++);
                m.ClearClassList();
                m.AddToClassList("map-mark");
                m.AddToClassList(MarkClass(kind));
                m.style.left = MiniSize / 2 + d.x;
                m.style.top = MiniSize / 2 + d.y;
            }
            for (int i = n; i < miniMarks.Count; i++) miniMarks[i].style.display = DisplayStyle.None;
            miniPlayer.BringToFront();
            if (!bigMapOpen) return;
            // the big one: the whole scene
            var frame = bigImg.parent;
            float side = Mathf.Min(root.resolvedStyle.width, root.resolvedStyle.height - 90) * 0.85f;
            if (!float.IsNaN(side) && side > 100) { frame.style.width = side; frame.style.height = side; }
            float w = frame.resolvedStyle.width, h = frame.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0) return;
            bigPlayer.style.left = me.x * w;
            bigPlayer.style.top = me.y * h;
            bigPlayer.style.rotate = new Rotate(yaw);
            n = 0;
            foreach (var (kind, at) in marks)
            {
                var p = WorldMap.ToMap(at);
                var m = Pooled(bigMarks, frame, n++);
                m.ClearClassList();
                m.AddToClassList("map-mark");
                m.AddToClassList(MarkClass(kind));
                m.AddToClassList("big");
                m.style.left = p.x * w;
                m.style.top = p.y * h;
            }
            for (int i = n; i < bigMarks.Count; i++) bigMarks[i].style.display = DisplayStyle.None;
            bigPlayer.BringToFront();
        }

        void OnDestroy() => WorldMap.Clear();

        Label fps, goalText;
        VisualElement goal;

        /// <summary>The task's place on screen: a mark over it with the distance, or at the edge,
        /// turned towards it, when it is off screen or behind.</summary>
        void UpdateGoal()
        {
            var t = Story.Goal;
            var cam = Camera.main;
            bool show = t && cam && hero && !Cinema && !Paused && t.gameObject.activeInHierarchy;
            goal.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            var p = t.position + Vector3.up * 2.2f;
            float dist = Vector3.Distance(hero.transform.position, t.position);
            var sp = cam.WorldToScreenPoint(p);
            bool behind = sp.z < 0;
            if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }
            float mx = Screen.width * 0.06f, my = Screen.height * 0.1f;
            bool edge = behind || sp.x < mx || sp.x > Screen.width - mx || sp.y < my || sp.y > Screen.height - my;
            if (behind) sp.y = Mathf.Min(sp.y, Screen.height * 0.3f);   // behind: along the bottom
            sp.x = Mathf.Clamp(sp.x, mx, Screen.width - mx);
            sp.y = Mathf.Clamp(sp.y, my, Screen.height - my);
            var panel = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y));
            goal.style.left = panel.x;
            goal.style.top = panel.y;
            goal.EnableInClassList("edge", edge);
            goalText.text = dist < 4 ? "" : $"{dist:0} м";
        }
        float fpsTime;
        int fpsFrames;

        void OpenSettings()
        {
            pauseBox.style.display = DisplayStyle.None;
            settings = Settings.Panel(CloseSettings);
            pause.Add(settings);
        }

        void CloseSettings()
        {
            settings?.RemoveFromHierarchy();
            settings = null;
            pauseBox.style.display = DisplayStyle.Flex;
        }

        public bool Paused { get; private set; }

        void TogglePause()
        {
            Paused = !Paused;
            if (!Paused) CloseSettings();
            else pauseBox.Q<Button>()?.Focus();
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
