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
