// The one object every built scene has: which scene this is and the library it was built with.
// It makes sure the session exists when a scene is started straight from the editor.
using UnityEngine;

namespace Crab.Game
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SceneSetup : MonoBehaviour
    {
        public Library library;
        public string sceneName;
        [Tooltip("Where the hero appears when the scene starts without a checkpoint")]
        public Transform start;

        void Awake()
        {
            Library.Current = library;
            GameSession.Ensure();
        }
    }
}
