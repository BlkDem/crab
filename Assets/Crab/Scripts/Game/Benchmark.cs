// A frame-time run through every scene of a player build: start it with -crab-bench (and
// optionally -crab-quality 0|1|2). Each scene plays a few seconds, the camera turns around the
// crab, and the frame times go to the log (BENCH lines), then the player quits.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crab.Game
{
    public sealed class Benchmark : MonoBehaviour
    {
        static readonly string[] Scenes = { "Menu", "Prologue", "Limbo", "Wrath", "Greed", "Envy", "Gloom", "Betrayal", "Violence", "Lies", "Power", "Epilogue" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("-crab-bench")) return;
            int qi = Array.IndexOf(args, "-crab-quality");
            if (qi >= 0 && qi + 1 < args.Length && int.TryParse(args[qi + 1], out int q)) Settings.Quality = q;
            Settings.VSync = false;
            var go = new GameObject("Benchmark");
            DontDestroyOnLoad(go);
            go.AddComponent<Benchmark>();
        }

        IEnumerator Start()
        {
            Debug.Log($"BENCH {SystemInfo.graphicsDeviceName}, {Screen.width}x{Screen.height}, quality {Settings.Qualities[Settings.Quality]}, scale {Settings.RenderScale:0.00}");
            var session = GameSession.Ensure();
            session.ResetState();
            var all = new List<float>();
            foreach (var scene in Scenes)
            {
                session.State.Scene = scene;
                SceneManager.LoadScene(scene);
                yield return null;
                yield return null;
                if (Hero.Instance) Hero.Instance.Invulnerable = true;
                for (float t = 0; t < 3; t += Time.unscaledDeltaTime) yield return null;   // uploads settle
                var times = new List<float>();
                var cpu = new List<double>();
                var gpu = new List<double>();
                var timing = new FrameTiming[1];
                for (float t = 0; t < 6; t += Time.unscaledDeltaTime)
                {
                    if (Hero.Instance) Hero.Instance.Cam.Face(t * 60, 12);
                    times.Add(Time.unscaledDeltaTime * 1000);
                    // the work itself, apart from waiting for the screen (a driver may hold the frame rate)
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                    {
                        if (timing[0].cpuMainThreadFrameTime > 0) cpu.Add(timing[0].cpuMainThreadFrameTime);
                        if (timing[0].gpuFrameTime > 0) gpu.Add(timing[0].gpuFrameTime);
                    }
                    yield return null;
                }
                times.Sort();
                float avg = times.Average(), p95 = times[(int)(times.Count * 0.95f)];
                double c = cpu.Count > 0 ? cpu.Average() : 0, g = gpu.Count > 0 ? gpu.Average() : 0;
                all.Add((float)System.Math.Max(c, g));
                Debug.Log($"BENCH {scene,-9} frame {avg,5:0.0} ms ({1000 / avg,4:0} fps), 95% {p95,5:0.0} ms · cpu {c,5:0.0} ms · gpu {g,5:0.0} ms");
            }
            Debug.Log($"BENCH work (max of cpu, gpu): avg {all.Average():0.0} ms, worst {all.Max():0.0} ms");
            Application.Quit();
        }
    }
}
