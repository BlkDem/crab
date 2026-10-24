// What the game tells the player, for the interface to show: the crab's thoughts (its inner
// voice since the Spark), the current task, big title cards, hints and the prompt for the
// thing in reach.
using System;

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

        public static void Task(string text)
        {
            Objective = text;
            OnObjective?.Invoke(text);
        }

        public static void Title(string main, string sub) => OnTitle?.Invoke(main, sub);
        public static void Hint(string text) => OnHint?.Invoke(text);
        public static void Whisper(string text) => OnWhisper?.Invoke(text);
        public static void AI(string text, float seconds = 4.5f) => OnAI?.Invoke(text, seconds);
        /// <summary>The whole screen flashes a colour and fades over `seconds`.</summary>
        public static void Flash(UnityEngine.Color c, float seconds) => OnFlash?.Invoke(c, seconds);
    }
}
