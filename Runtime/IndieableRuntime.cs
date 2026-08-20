using System.Collections;
using UnityEngine;

namespace IndieableSdk
{
    internal sealed class IndieableRuntime : MonoBehaviour
    {
        private static IndieableRuntime _instance;

        internal static IndieableRuntime Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var gameObject = new GameObject("Indieable Runtime");
                gameObject.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(gameObject);
                _instance = gameObject.AddComponent<IndieableRuntime>();
                return _instance;
            }
        }

        internal void Run(IEnumerator routine)
        {
            if (routine != null) StartCoroutine(routine);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
