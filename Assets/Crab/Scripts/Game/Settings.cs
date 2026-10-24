// The player's settings: sound, the mouse, the picture. Kept in PlayerPrefs, applied at start, and
// one panel for them shared by the title screen and the pause menu.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Crab.Game
{
    public static class Settings
    {
        public static readonly string[] Qualities = { "Низкое", "Среднее", "Высокое" };

        public static float Volume { get => Get("volume", 0.8f); set => Set("volume", value); }
        public static float Music { get => Get("music", 0.5f); set => Set("music", value); }
        public static float Sensitivity { get => Get("sens", 1f); set => Set("sens", value); }
        public static bool InvertY { get => Get("invert", 0f) > 0.5f; set => Set("invert", value ? 1 : 0); }
        /// <summary>0 low, 1 medium, 2 high.</summary>
        public static int Quality { get => Mathf.RoundToInt(Get("quality", DefaultQuality)); set => Set("quality", value); }
        /// <summary>The share of the screen's pixels drawn, 0.5..1.</summary>
        public static float RenderScale { get => Get("scale", 1f); set => Set("scale", value); }
        public static bool Fullscreen { get => Get("full", 1f) > 0.5f; set => Set("full", value ? 1 : 0); }
        public static bool VSync { get => Get("vsync", 1f) > 0.5f; set => Set("vsync", value ? 1 : 0); }
        public static bool ShowFps { get => Get("fps", 0f) > 0.5f; set => Set("fps", value ? 1 : 0); }
        public static event Action Changed;

        static float DefaultQuality => Application.platform == RuntimePlatform.WebGLPlayer ? 0 : SystemInfo.graphicsMemorySize >= 4000 ? 2 : 1;

        static float Get(string key, float fallback) => PlayerPrefs.GetFloat("crab." + key, fallback);

        static void Set(string key, float v)
        {
            PlayerPrefs.SetFloat("crab." + key, v);
            Apply();
        }

        static UniversalRenderPipelineAsset pipeline;

        /// <summary>Puts everything in force: sound, the mouse, the window and the picture.</summary>
        public static void Apply()
        {
            Sfx.volume = Volume;
            Sfx.music = Music;
            Controls.sensitivity = Sensitivity;
            Controls.invertY = InvertY;
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 240;
            if (!Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer)
            {
                var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
            }
            // the picture: a copy of the pipeline asset, so the project's own one is never changed
            // (in the editor the asset on disk would be)
            if (!Application.isEditor)
            {
                if (!pipeline && QualitySettings.renderPipeline is UniversalRenderPipelineAsset own)
                {
                    pipeline = UnityEngine.Object.Instantiate(own);
                    QualitySettings.renderPipeline = pipeline;
                }
                if (pipeline)
                {
                    int q = Quality;
                    pipeline.renderScale = Mathf.Clamp(RenderScale, 0.5f, 1f);
                    pipeline.shadowDistance = new[] { 35f, 60f, 80f }[q];
                    pipeline.shadowCascadeCount = q == 0 ? 1 : 2;
                    pipeline.mainLightShadowmapResolution = new[] { 1024, 2048, 4096 }[q];
                    pipeline.msaaSampleCount = q == 2 ? 2 : 1;
                    pipeline.upscalingFilter = UpscalingFilterSelection.FSR;
                }
            }
            QualitySettings.lodBias = new[] { 0.8f, 1.5f, 2f }[Mathf.Clamp(Quality, 0, 2)];
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ the panel
        /// <summary>The settings as rows of sliders and switches, with a way back.</summary>
        public static VisualElement Panel(Action back)
        {
            var box = new VisualElement();
            box.AddToClassList("menu-box");
            box.AddToClassList("settings-box");
            var title = new Label("НАСТРОЙКИ");
            title.AddToClassList("menu-title");
            box.Add(title);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("settings-scroll");
            box.Add(scroll);
            Head(scroll, "Звук");
            Slide(scroll, "Громкость", 0, 1, Volume, v => Volume = v, v => $"{v * 100:0}%");
            Slide(scroll, "Музыка", 0, 1, Music, v => Music = v, v => $"{v * 100:0}%");
            Head(scroll, "Управление");
            Slide(scroll, "Чувствительность мыши", 0.1f, 3, Sensitivity, v => Sensitivity = v, v => $"{v:0.0}×");
            Switch(scroll, "Инверсия по вертикали", InvertY, v => InvertY = v);
            Head(scroll, "Изображение");
            Choice(scroll, "Качество", Qualities, Quality, v => Quality = v);
            Slide(scroll, "Чёткость (масштаб кадра)", 0.5f, 1, RenderScale, v => RenderScale = v, v => $"{v * 100:0}%");
            if (Application.platform != RuntimePlatform.WebGLPlayer) Switch(scroll, "Полный экран", Fullscreen, v => Fullscreen = v);
            Switch(scroll, "Вертикальная синхронизация", VSync, v => VSync = v);
            Switch(scroll, "Счётчик кадров", ShowFps, v => ShowFps = v);
            var b = new Button(() => { Sfx.Play("click"); back(); }) { text = "Назад" };
            b.AddToClassList("menu-btn");
            box.Add(b);
            return box;
        }

        static void Head(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.AddToClassList("settings-head");
            parent.Add(l);
        }

        static VisualElement Row(VisualElement parent, string title, out Label value)
        {
            var row = new VisualElement();
            row.AddToClassList("menu-row");
            var top = new VisualElement();
            top.AddToClassList("settings-top");
            top.Add(new Label(title));
            value = new Label();
            value.AddToClassList("settings-value");
            top.Add(value);
            row.Add(top);
            parent.Add(row);
            return row;
        }

        static void Slide(VisualElement parent, string title, float min, float max, float v, Action<float> set, Func<float, string> show)
        {
            var row = Row(parent, title, out var value);
            value.text = show(v);
            var s = new Slider(min, max) { value = v };
            s.RegisterValueChangedCallback(e => { set(e.newValue); value.text = show(e.newValue); });
            row.Add(s);
        }

        static void Switch(VisualElement parent, string title, bool v, Action<bool> set)
        {
            var row = Row(parent, title, out var value);
            value.text = v ? "вкл" : "выкл";
            row.AddToClassList("settings-switch");
            row.EnableInClassList("on", v);
            row.RegisterCallback<ClickEvent>(_ =>
            {
                v = !v;
                set(v);
                Sfx.Play("click");
                value.text = v ? "вкл" : "выкл";
                row.EnableInClassList("on", v);
            });
        }

        static void Choice(VisualElement parent, string title, string[] options, int v, Action<int> set)
        {
            var row = Row(parent, title, out var value);
            value.text = "";
            var line = new VisualElement();
            line.AddToClassList("settings-choice");
            row.Add(line);
            var buttons = new Button[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int k = i;
                buttons[i] = new Button(() =>
                {
                    set(k);
                    Sfx.Play("click");
                    for (int j = 0; j < buttons.Length; j++) buttons[j].EnableInClassList("on", j == k);
                }) { text = options[i] };
                buttons[i].EnableInClassList("on", i == v);
                line.Add(buttons[i]);
            }
        }
    }
}
