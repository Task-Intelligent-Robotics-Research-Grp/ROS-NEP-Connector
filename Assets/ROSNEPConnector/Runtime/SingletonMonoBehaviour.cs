using UnityEngine;

namespace ROSNEPConnector
{
    // Base class of components which act as singletons.
    public class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError("*** Created multiple singleton[" + typeof(T) + "]!!", this);
                return;
            }

            _instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        protected static T Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = (T)FindObjectOfType(typeof(T));

                if (_instance == null)
                    Debug.LogError("*** Failed to find a singleton[" + typeof(T) + "]!!");

                return _instance;
            }
        }
    }
}
