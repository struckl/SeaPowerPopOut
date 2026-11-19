using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>
/// Things to set up once the game has built them: a window or view is loaded before its template is applied, so the
/// setup is tried again every so often, a limited number of times.
/// </summary>
internal sealed class Retries<T> where T : class
{
    private sealed class Attempt
    {
        public WeakReference<T> Target;
        public float Next;
        public int Tries;
    }

    private readonly List<Attempt> pending = new List<Attempt>();
    private readonly float every;
    private readonly int maxTries;

    public Retries(float every, int maxTries)
    {
        this.every = every;
        this.maxTries = maxTries;
    }

    public void Add(T target) => pending.Add(new Attempt { Target = new WeakReference<T>(target) });

    /// <summary>
    /// Tries the targets that are due; <paramref name="setUp"/> returns true when done (set up, or nothing to do). A
    /// target that is gone or was tried too often is dropped.
    /// </summary>
    public void Run(Func<T, bool> setUp)
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            Attempt attempt = pending[i];
            if (Time.unscaledTime < attempt.Next)
                continue;
            attempt.Next = Time.unscaledTime + every;
            if (!attempt.Target.TryGetTarget(out T target) || ++attempt.Tries > maxTries || setUp(target))
                pending.RemoveAt(i);
        }
    }
}
