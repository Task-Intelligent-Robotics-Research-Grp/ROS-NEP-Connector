using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace ROSNEPConnector
{
    public static class Utility
    {
        public static GameObject FindRootObject(string root_object_name)
        {
            return Array.Find(SceneManager.GetActiveScene().GetRootGameObjects(), root_object => root_object.name == root_object_name);
        }

        public static GameObject FindObject(string name, string root_object_name)
        {
            return FindTransform(name, root_object_name)?.gameObject;
        }

        public static Transform FindTransform(string name, string root_object_name)
        {
            var root_object = FindRootObject(root_object_name);
            if (root_object == null)
                return null;
            return FindTransform(name, root_object.transform);
        }

        public static Transform FindTransform(string name, Transform tfm)
        {
            if (tfm.name == name)
                return tfm;

            foreach (Transform child in tfm)
            {
                var found = FindTransform(name, child);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
