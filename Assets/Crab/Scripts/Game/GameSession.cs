// The playthrough across scenes: the game state, saving and loading, scene changes with a fade.
using System;
using System.Collections;
using System.IO;
using Crab.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crab.Game
{
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        public GameState State { get; private set; } = new GameState();
        /// <summary>Tests keep their saves elsewhere.</summary>
        public static string SavePathOverride;
        public static string SavePath => SavePathOverride ?? Path.Combine(Application.persistentDataPath, "save.json");
        /// <summary>0..1 black over the screen (the interface draws it).</summary>
        public float fade;
        public bool Changing { get; private set; }
        public event Action<string> SceneStarted;

        public static GameSession Ensure()
        {
            if (Instance) return Instance;
            var go = new GameObject("GameSession");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameSession>();
            return Instance;
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SceneManager.sceneLoaded += (s, _) => SceneStarted?.Invoke(s.name);
            Settings.Apply();
        }

        void Update()
        {
            if (Time.timeScale > 0) State.PlaySeconds += Time.unscaledDeltaTime;
        }

        public static bool HasSave => File.Exists(SavePath);

        /// <summary>Holds the game's time from outside ("1") or lets it go ("0"): in a browser,
        /// unityInstance.SendMessage("GameSession", "Freeze", "1"). For tools that play the game
        /// step by step and look between the steps.</summary>
        public void Freeze(string on)
        {
            bool hold = on == "1";
            Time.timeScale = hold ? 0 : 1;
            AudioListener.pause = hold;
        }

        /// <summary>A fresh playthrough without changing scene (tests, debugging).</summary>
        public void ResetState() => State = new GameState();

        public void NewGame()
        {
            State = new GameState();
            Go("Prologue");
        }

        public void Continue()
        {
            if (!Load()) { NewGame(); return; }
            Go(State.Scene);
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(State.ToSave(), true));
            }
            catch (IOException e) { Debug.LogWarning($"save: {e.Message}"); }
        }

        public bool Load()
        {
            try
            {
                if (!HasSave) return false;
                var s = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (s == null) return false;
                State = new GameState();
                State.Load(s);
                return true;
            }
            catch (Exception e) { Debug.LogWarning($"load: {e.Message}"); return false; }
        }

        /// <summary>Fade out, load a scene, fade in.</summary>
        public void Go(string scene, float fadeTime = 0.8f)
        {
            if (Changing) return;
            StartCoroutine(GoRoutine(scene, fadeTime));
        }

        IEnumerator GoRoutine(string scene, float fadeTime)
        {
            Changing = true;
            float from = fade;   // it may already be dark (the end)
            for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime) { fade = Mathf.Max(from, t / fadeTime); yield return null; }
            fade = 1;
            Time.timeScale = 1;
            if (scene != "Menu") State.Enter(scene);
            // loaded at once behind the black: an async load keeps uploading the scene's many
            // textures for seconds after it opens, and the top mips show garbage until then
            yield return null;
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime) { fade = 1 - t / fadeTime; yield return null; }
            fade = 0;
            Changing = false;
        }
    }
}
