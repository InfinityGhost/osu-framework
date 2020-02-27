// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using osu.Framework.Development;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Logging;
using osu.Framework.Threading;

namespace osu.Framework.Platform
{
    /// <summary>
    /// Runs a game host in a specifc threading mode.
    /// </summary>
    internal class ThreadRunner
    {
        private readonly InputThread mainThread;

        private readonly List<GameThread> threads = new List<GameThread>();

        public IReadOnlyCollection<GameThread> Threads
        {
            get
            {
                lock (threads)
                    return threads.ToArray();
            }
        }

        private double maximumUpdateHz;

        public double MaximumUpdateHz
        {
            set
            {
                maximumUpdateHz = value;
                updateMainThreadRates();
            }
        }

        private double maximumInactiveHz;

        public double MaximumInactiveHz
        {
            set
            {
                maximumInactiveHz = value;
                updateMainThreadRates();
            }
        }

        /// <summary>
        /// Construct a new ThreadRunner instance.
        /// </summary>
        /// <param name="mainThread">The main window thread. Used for input in multi-threaded execution; all game logic in single-threaded execution.</param>
        /// <exception cref="NotImplementedException"></exception>
        public ThreadRunner(InputThread mainThread)
        {
            this.mainThread = mainThread;
            AddThread(mainThread);
        }

        /// <summary>
        /// Add a new non-main thread. In single-threaded execution, threads will be executed in the order they are added.
        /// </summary>
        public void AddThread(GameThread thread)
        {
            lock (threads)
            {
                if (!threads.Contains(thread))
                    threads.Add(thread);
            }
        }

        /// <summary>
        /// Remove a non-main thread.
        /// </summary>
        public void RemoveThread(GameThread thread)
        {
            lock (threads)
                threads.Remove(thread);
        }

        private bool? singleThreaded;

        public bool SingleThreaded;

        public void RunMainLoop()
        {
            ensureCorrectExecutionMode();

            Debug.Assert(singleThreaded != null);

            if (singleThreaded.Value)
            {
                lock (threads)
                {
                    foreach (var t in threads)
                        t.ProcessFrame();
                }
            }
            else
            {
                mainThread.ProcessFrame();
            }
        }

        public void Start() => ensureCorrectExecutionMode();

        public void Stop()
        {
            const int thread_join_timeout = 30000;

            Threads.ForEach(t => t.Exit());
            Threads.Where(t => t.Running).ForEach(t =>
            {
                if (!t.Thread.Join(thread_join_timeout))
                    Logger.Log($"Thread {t.Name} failed to exit in allocated time ({thread_join_timeout}ms).", LoggingTarget.Runtime, LogLevel.Important);
            });

            // as the input thread isn't actually handled by a thread, the above join does not necessarily mean it has been completed to an exiting state.
            while (!mainThread.Exited)
                mainThread.ProcessFrame();
        }

        private void ensureCorrectExecutionMode()
        {
            if (SingleThreaded == singleThreaded)
                return;

            if (!SingleThreaded)
            {
                // switch to multi-threaded
                foreach (var t in Threads)
                {
                    t.Start();
                    t.Clock.Throttling = true;
                }
            }
            else
            {
                // switch to single-threaded.
                foreach (var t in Threads)
                    t.Pause();

                foreach (var t in Threads)
                {
                    // only throttle for the main thread
                    t.Initialize(withThrottling: t == mainThread);
                }
            }

            singleThreaded = SingleThreaded;

            ThreadSafety.SingleThreadThread = singleThreaded.Value ? Thread.CurrentThread : null;
            updateMainThreadRates();
        }

        private void updateMainThreadRates()
        {
            if (singleThreaded ?? false)
            {
                mainThread.ActiveHz = maximumUpdateHz;
                mainThread.InactiveHz = maximumInactiveHz;
            }
            else
            {
                mainThread.ActiveHz = GameThread.DEFAULT_ACTIVE_HZ;
                mainThread.InactiveHz = GameThread.DEFAULT_INACTIVE_HZ;
            }
        }
    }
}
