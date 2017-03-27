using System;
using System.Threading;

namespace Lucene.Net.Store
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    // LUCENENET TODO: API Create NewAnonymous() method and update code sample
    /// <summary>
    /// An interprocess mutex lock.
    /// <para/>Typical use might look like:
    /// 
    /// <code>
    /// new Lock.With(directory.MakeLock("my.lock")) 
    /// {
    ///     public object doBody() {
    ///       <i>... code to execute while locked ...</i>
    ///     }
    /// }.Run();
    /// </code>
    /// </summary>
    /// <seealso cref="Directory.MakeLock(string)"/>
    public abstract class Lock : IDisposable
    {
        /// <summary>
        /// How long <see cref="Obtain(long)"/> waits, in milliseconds,
        /// in between attempts to acquire the lock.
        /// </summary>
        public static long LOCK_POLL_INTERVAL = 1000;

        /// <summary>
        /// Pass this value to <see cref="Obtain(long)"/> to try
        /// forever to obtain the lock.
        /// </summary>
        public const long LOCK_OBTAIN_WAIT_FOREVER = -1;

        /// <summary>
        /// Attempts to obtain exclusive access and immediately return
        /// upon success or failure.  Use <see cref="Dispose()"/> to
        /// release the lock. </summary>
        /// <returns> true iff exclusive access is obtained </returns>
        public abstract bool Obtain();

        /// <summary>
        /// If a lock obtain called, this failureReason may be set
        /// with the "root cause" <see cref="Exception"/> as to why the lock was
        /// not obtained.
        /// </summary>
        protected internal Exception FailureReason { get; set; }

        /// <summary>
        /// Attempts to obtain an exclusive lock within amount of
        /// time given. Polls once per <see cref="LOCK_POLL_INTERVAL"/>
        /// (currently 1000) milliseconds until <paramref name="lockWaitTimeout"/> is
        /// passed.
        /// </summary>
        /// <param name="lockWaitTimeout"> length of time to wait in
        ///        milliseconds or 
        ///        <see cref="LOCK_OBTAIN_WAIT_FOREVER"/> to retry forever </param>
        /// <returns> <c>true</c> if lock was obtained </returns>
        /// <exception cref="LockObtainFailedException"> if lock wait times out </exception>
        /// <exception cref="ArgumentException"> if <paramref name="lockWaitTimeout"/> is
        ///         out of bounds </exception>
        /// <exception cref="System.IO.IOException"> if <see cref="Obtain()"/> throws <see cref="System.IO.IOException"/> </exception>
        public bool Obtain(long lockWaitTimeout)
        {
            FailureReason = null;
            bool locked = Obtain();
            if (lockWaitTimeout < 0 && lockWaitTimeout != LOCK_OBTAIN_WAIT_FOREVER)
            {
                throw new ArgumentException("lockWaitTimeout should be LOCK_OBTAIN_WAIT_FOREVER or a non-negative number (got " + lockWaitTimeout + ")");
            }

            long maxSleepCount = lockWaitTimeout / LOCK_POLL_INTERVAL;
            long sleepCount = 0;
            while (!locked)
            {
                if (lockWaitTimeout != LOCK_OBTAIN_WAIT_FOREVER && sleepCount++ >= maxSleepCount)
                {
                    string reason = "Lock obtain timed out: " + this.ToString();
                    if (FailureReason != null)
                    {
                        reason += ": " + FailureReason;
                    }
                    LockObtainFailedException e = new LockObtainFailedException(reason);
                    e = FailureReason != null
                                        ? new LockObtainFailedException(reason, FailureReason)
                                        : new LockObtainFailedException(reason);
                    throw e;
                }

#if !NETSTANDARD
                try
                {
#endif
                    Thread.Sleep(TimeSpan.FromMilliseconds(LOCK_POLL_INTERVAL));
#if !NETSTANDARD                
                }
                catch (ThreadInterruptedException ie)
                {
                    throw new ThreadInterruptedException(ie.ToString(), ie);
                }
#endif
                locked = Obtain();
            }
            return locked;
        }

        /// <summary>
        /// Releases exclusive access. </summary>
        public abstract void Release(); // LUCENENET TODO: API Change to Dispose(bool) for proper implementation of disposable pattern

        /// <summary>
        /// Releases exclusive access. </summary>
        public virtual void Dispose()
        {
            Release();
        }

        /// <summary>
        /// Returns <c>true</c> if the resource is currently locked.  Note that one must
        /// still call <see cref="Obtain()"/> before using the resource.
        /// </summary>
        public abstract bool IsLocked { get; }

        /// <summary>
        /// Utility class for executing code with exclusive access. </summary>
        public abstract class With
        {
            private Lock @lock;
            private long lockWaitTimeout;

            /// <summary>
            /// Constructs an executor that will grab the named <paramref name="lock"/>. </summary>
            public With(Lock @lock, long lockWaitTimeout)
            {
                this.@lock = @lock;
                this.lockWaitTimeout = lockWaitTimeout;
            }

            /// <summary>
            /// Code to execute with exclusive access. </summary>
            protected abstract object DoBody();

            /// <summary>
            /// Calls <see cref="DoBody"/> while <i>lock</i> is obtained.  Blocks if lock
            /// cannot be obtained immediately.  Retries to obtain lock once per second
            /// until it is obtained, or until it has tried ten times. Lock is released when
            /// <see cref="DoBody"/> exits. </summary>
            /// <exception cref="LockObtainFailedException"> if lock could not
            /// be obtained </exception>
            /// <exception cref="System.IO.IOException"> if <see cref="Lock.Obtain()"/> throws <see cref="System.IO.IOException"/> </exception>
            public virtual object Run()
            {
                bool locked = false;
                try
                {
                    locked = @lock.Obtain(lockWaitTimeout);
                    return DoBody();
                }
                finally
                {
                    if (locked)
                    {
                        @lock.Release();
                    }
                }
            }
        }
    }
}