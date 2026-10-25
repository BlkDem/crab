// What the game tells the player, for the interface to show: the crab's thoughts (its inner
// voice since the Spark), the current task, big title cards, hints and the prompt for the
// thing in reach.
using System;
using UnityEngine;

namespace Crab.Game
{
    public static class Story
    {
        public static event Action<string, float> OnThought;
        public static event Action<string> OnObjective, OnHint;
        public static event Action<string, string> OnTitle;
        public static event Action<UnityEngine.Color, float> OnFlash;
        /// <summary>A voice from the dark (the psychological attacks of the circle of Despondency).</summary>
        public static event Action<string> OnWhisper;
        /// <summary>The crab's AI companion speaking (from the circle of Despondency on).</summary>
        public static event Action<string, float> OnAI;
        public static string Prompt;
        public static string Objective { get; private set; } = "";

        public static void Think(string text, float seconds = 4) => OnThought?.Invoke(text, seconds);

        /// <summary>Where the task leads, if somewhere: the interface marks it on screen.</summary>
        public static Transform Target { get; private set; }
        public static System.Func<Transform> Nearest;

        /// <summary>A new task; `target` (or the nearest of `nearest`) is where it leads.</summary>
        public static void Task(string text, Transform target = null, System.Func<Transform> nearest = null)
        {
            Objective = text;
            Target = target;
            Nearest = nearest;
            OnObjective?.Invoke(text);
        }

        /// <summary>The place to mark now: the given one, or the nearest of a set.</summary>
        public static Transform Goal => Target ? Target : Nearest?.Invoke();

        public static void Title(string main, string sub) => OnTitle?.Invoke(main, sub);
        public static void Hint(string text) => OnHint?.Invoke(text);
        public static void Whisper(string text) => OnWhisper?.Invoke(text);
        public static void AI(string text, float seconds = 4.5f) => OnAI?.Invoke(text, seconds);
        /// <summary>The whole screen flashes a colour and fades over `seconds`.</summary>
        public static void Flash(UnityEngine.Color c, float seconds) => OnFlash?.Invoke(c, seconds);
    }
}
