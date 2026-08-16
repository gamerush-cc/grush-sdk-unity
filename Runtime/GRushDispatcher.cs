using System;
using System.Collections.Generic;
using UnityEngine;

namespace GRushSdk
{
    public sealed class GRushDispatcher : MonoBehaviour
    {
        private static GRushDispatcher instance;
        private static readonly Queue<Action> Pending = new Queue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }
            var host = new GameObject("GRushDispatcher");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<GRushDispatcher>();
        }

        public static void Post(Action action)
        {
            if (action == null)
            {
                return;
            }
            lock (Pending)
            {
                Pending.Enqueue(action);
            }
        }

        private void Update()
        {
            while (true)
            {
                Action action;
                lock (Pending)
                {
                    if (Pending.Count == 0)
                    {
                        return;
                    }
                    action = Pending.Dequeue();
                }
                try
                {
                    action();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
