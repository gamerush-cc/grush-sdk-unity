using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GRushSdk.Editor
{
    /// <summary>
    /// <see cref="EditorApplication.update"/> で <c>IEnumerator</c> を回す。
    /// エディタでは <c>async</c> の再開が同期コンテキスト任せで、再コンパイルや
    /// バッチモードでの挙動が読めないため、進行は自前のポンプで持つ。
    /// </summary>
    internal static class GRushEditorRoutine
    {
        private sealed class Entry
        {
            public Stack<IEnumerator> Frames;
            public AsyncOperation Waiting;
            public Action<Exception> OnError;
        }

        private static readonly List<Entry> Running = new List<Entry>();
        private static bool pumping;

        /// <summary>
        /// 再コンパイルとエディタ終了でも後始末を通す。ここを抜けると、承認待ちの
        /// <c>HttpListener</c> がポートを握ったまま残る。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void HookShutdown()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopAll;
            EditorApplication.quitting += StopAll;
        }

        public static void StopAll()
        {
            foreach (var entry in Running)
            {
                Release(entry);
            }
            Running.Clear();
            if (pumping)
            {
                pumping = false;
                EditorApplication.update -= Pump;
            }
        }

        public static void Start(IEnumerator routine, Action<Exception> onError)
        {
            if (routine == null)
            {
                return;
            }
            var frames = new Stack<IEnumerator>();
            frames.Push(routine);
            Running.Add(new Entry { Frames = frames, OnError = onError });
            if (!pumping)
            {
                pumping = true;
                EditorApplication.update += Pump;
            }
        }

        private static void Pump()
        {
            for (var index = Running.Count - 1; index >= 0; index--)
            {
                if (!Step(Running[index]))
                {
                    Release(Running[index]);
                    Running.RemoveAt(index);
                }
            }
            if (Running.Count == 0)
            {
                pumping = false;
                EditorApplication.update -= Pump;
            }
        }

        private static bool Step(Entry entry)
        {
            try
            {
                return Advance(entry);
            }
            catch (Exception error)
            {
                if (entry.OnError != null)
                {
                    entry.OnError(error);
                }
                else
                {
                    Debug.LogException(error);
                }
                return false;
            }
        }

        /// <summary>
        /// 中断したイテレータの <c>finally</c> を走らせる。**捨てるだけでは
        /// 走らない** — <c>using</c> と <c>finally</c> で閉じているもの
        /// （loopback の <c>HttpListener</c>、送信中の <c>UnityWebRequest</c>）が
        /// 開いたまま残る。内側の入れ子から順に閉じる。
        /// </summary>
        private static void Release(Entry entry)
        {
            entry.Waiting = null;
            while (entry.Frames.Count > 0)
            {
                var disposable = entry.Frames.Pop() as IDisposable;
                if (disposable == null)
                {
                    continue;
                }
                try
                {
                    disposable.Dispose();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
        }

        private static bool Advance(Entry entry)
        {
            if (entry.Waiting != null)
            {
                if (!entry.Waiting.isDone)
                {
                    return true;
                }
                entry.Waiting = null;
            }
            while (entry.Frames.Count > 0)
            {
                var frame = entry.Frames.Peek();
                if (!frame.MoveNext())
                {
                    entry.Frames.Pop();
                    continue;
                }
                var nested = frame.Current as IEnumerator;
                if (nested != null)
                {
                    entry.Frames.Push(nested);
                    continue;
                }
                var operation = frame.Current as AsyncOperation;
                if (operation != null && !operation.isDone)
                {
                    entry.Waiting = operation;
                }
                return true;
            }
            return false;
        }
    }
}
