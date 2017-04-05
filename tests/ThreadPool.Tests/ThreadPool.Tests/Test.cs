using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

using NUnit.Framework;

using RipcordSoftware.ThreadPool;

namespace ThreadPool.Tests
{
    [TestFixture()]
    public class Test
    {
        #region Constants
        private const int DefaultAssertWait = 5000;
        #endregion

        #region Types
        private delegate bool WaitFunction();
        #endregion

        #region Private methods
        private static bool WaitFor(WaitFunction func, int timeout)
        {
            var status = false;
            var end = DateTime.UtcNow.AddMilliseconds(timeout);

            while (!(status = func()) && DateTime.UtcNow < end)
            {
                Thread.Sleep(0);
            }

            return status;
        }

        private static void AssertWaitFor(WaitFunction func, int timeout = DefaultAssertWait)
        {
            Assert.IsTrue(WaitFor(func, timeout));
        }
        #endregion

        #region Tests
        [Test()]
        public void TestInitialState()
        {
            using (var pool = new ApplicationThreadPool("test", 2, 8, true))
            {
                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.MaxThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(8, pool.MaxQueueLength);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
                Assert.AreEqual(0, pool.CompletedItems);
            }
        }

        [Test()]
        public void TestSingleThread()
        {
            using (var pool = new ApplicationThreadPool("test", 2, 8, true))
            {
                var finished = false;
                pool.QueueUserWorkItem(o => { finished = true; });

                AssertWaitFor(() => pool.CompletedItems == 1);

                Assert.IsTrue(finished);
                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
            }
        }

        [Test()]
        public void TestSingleThreadWithValue()
        {
            using (var pool = new ApplicationThreadPool("test", 2, 8, true))
            {
                var finished = false;
                object value = null;
                pool.QueueUserWorkItem(o => { value = o; finished = true; }, "Hello world");

                AssertWaitFor(() => pool.CompletedItems == 1);

                Assert.IsTrue(finished);
                Assert.AreEqual(typeof(string), value.GetType());
                Assert.AreEqual("Hello world", (string)value);

                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
            }
        }

        [Test()]
        public void TestFourThreads()
        {
            using (var pool = new ApplicationThreadPool("test", 2, 8, true))
            {
                var finished = 0;
                pool.QueueUserWorkItem(o => { Interlocked.Increment(ref finished); });
                pool.QueueUserWorkItem(o => { Interlocked.Increment(ref finished); });
                pool.QueueUserWorkItem(o => { Interlocked.Increment(ref finished); });
                pool.QueueUserWorkItem(o => { Interlocked.Increment(ref finished); });

                AssertWaitFor(() => pool.CompletedItems == 4);

                Assert.AreEqual(4, finished);
                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
            }
        }

        [Test()]
        public void TestMultipleThreads()
        {
            const int maxWorkItems = 1024;
            using (var pool = new ApplicationThreadPool("test", 2, maxWorkItems, true))
            {
                var finished = 0;
                for (var i = 0; i < maxWorkItems; ++i)
                {
                    pool.QueueUserWorkItem(o => { Interlocked.Increment(ref finished); });
                }

                AssertWaitFor(() => pool.CompletedItems == maxWorkItems);

                Assert.AreEqual(maxWorkItems, finished);
                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
            }
        }

        [Test()]
        public void TestSingleThreadException()
        {
            using (var pool = new ApplicationThreadPool("test", 2, 8, true))
            {
                var finished = false;
                pool.QueueUserWorkItem(o => { finished = true; throw new Exception(); });

                AssertWaitFor(() => pool.CompletedItems == 1);

                Assert.IsTrue(finished);
                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(1, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
            }
        }

        [Test()]
        public void TestSingleTask()
        {
            using (var pool = new ApplicationThreadPool("test", 2, 8, true))
            {
                var finished = false;
                using (var task = pool.QueueUserTask(o => { finished = true; }))
                {
                    task.Join();
                }

                Assert.IsTrue(finished);
                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
                Assert.AreEqual(1, pool.CompletedItems);
            }
        }

        [Test()]
        public void TestSingleTaskException()
        {
            using (var pool = new ApplicationThreadPool("test", 2, 8, true))
            {
                var finished = false;
                using (var task = pool.QueueUserTask(o => { finished = true; throw new Exception(); }))
                {
                    task.Join();
                }

                Assert.IsTrue(finished);
                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(1, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
                Assert.AreEqual(1, pool.CompletedItems);
            }
        }

        [Test()]
        public void TestMultipleTasks()
        {
            const int maxWorkItems = 1024;
            using (var pool = new ApplicationThreadPool("test", 2, maxWorkItems, true))
            {
                int finished = 0;
                var tasks = new List<ApplicationThreadPool.TaskState>();
                for (var i = 0; i < maxWorkItems; ++i)
                {
                    tasks.Add(pool.QueueUserTask(o => { Interlocked.Increment(ref finished); }));
                }

                ApplicationThreadPool.TaskState.WaitAll(tasks, true);

                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
                Assert.AreEqual(maxWorkItems, pool.CompletedItems);
            }
        }

        [Test]
        public void TestSingleTypedThread()
        {
            using (var pool = new ApplicationThreadPool<string>("test", 2, 8, true))
            {

                var finished = false;
                Type paramType = null;
                string paramValue = null;
                pool.QueueUserWorkItem(o => { paramValue = o; paramType = o.GetType(); finished = true; }, "Hello world");

                AssertWaitFor(() => pool.CompletedItems == 1);

                Assert.IsTrue(finished);
                Assert.IsTrue(paramType == typeof(string));
                Assert.AreEqual("Hello world", paramValue);

                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
            }
        }

        [Test()]
        public void TestFourTypedThreads()
        {
            using (var pool = new ApplicationThreadPool<string>("test", 2, 8, true))
            {

                var values = new ConcurrentDictionary<string, bool>();
                var finished = 0;
                var count = 0;
                pool.QueueUserWorkItem(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString());
                pool.QueueUserWorkItem(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString());
                pool.QueueUserWorkItem(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString());
                pool.QueueUserWorkItem(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString());

                AssertWaitFor(() => pool.CompletedItems == 4);

                Assert.AreEqual(4, finished);
                Assert.AreEqual(4, values.Count);

                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
            }
        }

        [Test()]
        public void TestMultipleTypedThreads()
        {
            const int maxWorkItems = 1024;
            using (var pool = new ApplicationThreadPool<string>("test", 2, maxWorkItems, true))
            {

                var finished = 0;
                var values = new ConcurrentDictionary<string, bool>();
                for (var i = 0; i < maxWorkItems; ++i)
                {
                    pool.QueueUserWorkItem(o => { values[o] = true; Interlocked.Increment(ref finished); }, i.ToString());
                }

                AssertWaitFor(() => pool.CompletedItems == maxWorkItems);

                Assert.AreEqual(maxWorkItems, finished);
                Assert.AreEqual(maxWorkItems, values.Count);

                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
            }
        }

        [Test()]
        public void TestSingleTypedThreadException()
        {
            using (var pool = new ApplicationThreadPool<string>("test", 2, 8, true))
            {
                var finished = false;
                Type paramType = null;
                string paramValue = null;
                pool.QueueUserWorkItem(o => { paramValue = o; paramType = o.GetType(); finished = true; throw new Exception(); }, "Hello world");

                AssertWaitFor(() => pool.CompletedItems == 1);

                Assert.IsTrue(finished);
                Assert.IsTrue(paramType == typeof(string));
                Assert.AreEqual("Hello world", paramValue);

                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(1, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
            }
        }

        [Test()]
        public void TestSingleTypedTask()
        {
            using (var pool = new ApplicationThreadPool<string>("test", 2, 8, true))
            {
                var finished = false;
                Type paramType = null;
                string paramValue = null;
                using (var task = pool.QueueUserTask(o => { paramValue = o; paramType = o.GetType(); finished = true; }, "Hello world"))
                {
                    task.Join();
                }

                Assert.IsTrue(finished);
                Assert.IsTrue(paramType == typeof(string));
                Assert.AreEqual("Hello world", paramValue);

                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
                Assert.AreEqual(1, pool.CompletedItems);
            }
        }

        [Test()]
        public void TestFourTypedTasks()
        {
            using (var pool = new ApplicationThreadPool<string>("test", 2, 8, true))
            {
                var tasks = new List<ApplicationThreadPool.TaskState>();
                var values = new ConcurrentDictionary<string, bool>();
                var finished = 0;
                var count = 0;
                tasks.Add(pool.QueueUserTask(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString()));
                tasks.Add(pool.QueueUserTask(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString()));
                tasks.Add(pool.QueueUserTask(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString()));
                tasks.Add(pool.QueueUserTask(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString()));

                ApplicationThreadPool.TaskState.WaitAll(tasks, true);

                Assert.AreEqual(4, finished);
                Assert.AreEqual(4, values.Count);

                Assert.AreEqual(0, pool.ActiveThreads);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.TotalQueueLength);
                Assert.AreEqual(4, pool.CompletedItems);
            }
        }

        [Test()]
        public void TestFourBlockingThreads()
        {
            using (var pool = new ApplicationThreadPool("test", 4, 8, true))
            {
                using (var block = new ManualResetEvent(false))
                {
                    pool.QueueUserWorkItem(o => { block.WaitOne(); });
                    pool.QueueUserWorkItem(o => { block.WaitOne(); });
                    pool.QueueUserWorkItem(o => { block.WaitOne(); });
                    pool.QueueUserWorkItem(o => { block.WaitOne(); });

                    AssertWaitFor(() => pool.ActiveThreads == 4);
                    Assert.AreEqual(0, pool.AvailableThreads);
                    Assert.AreEqual(0, pool.QueueLength);
                    Assert.AreEqual(0, pool.TotalExceptions);
                    Assert.AreEqual(4, pool.TotalQueueLength);
                    Assert.AreEqual(0, pool.CompletedItems);

                    block.Set();

                    AssertWaitFor(() => pool.ActiveThreads == 0);
                    Assert.AreEqual(4, pool.AvailableThreads);
                    Assert.AreEqual(0, pool.QueueLength);
                    Assert.AreEqual(0, pool.TotalExceptions);
                    Assert.AreEqual(0, pool.TotalQueueLength);
                    Assert.AreEqual(4, pool.CompletedItems);
                }
            }
        }

        [Test()]
        public void TestFourBlockingThreadsCountdown()
        {
            using (var pool = new ApplicationThreadPool("test", 2, 8, true))
            {
                var activeWorkerEvents = new ConcurrentQueue<int>();

                var workerEvents = new ManualResetEvent[4];
                for (var i = 0; i < workerEvents.Length; ++i)
                {
                    workerEvents[i] = new ManualResetEvent(false);
                }

                for (var i = 0; i < workerEvents.Length; ++i)
                {
                    pool.QueueUserWorkItem(o => { var n = (int)o; activeWorkerEvents.Enqueue(n); workerEvents[n].WaitOne(); }, i);
                }

                AssertWaitFor(() => pool.ActiveThreads == 2);
                Assert.AreEqual(0, pool.AvailableThreads);
                Assert.AreEqual(2, pool.QueueLength);
                Assert.AreEqual(4, pool.TotalQueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
                Assert.AreEqual(0, pool.CompletedItems);

                int eventIndex = 0;
                Assert.IsTrue(activeWorkerEvents.TryDequeue(out eventIndex));
                workerEvents[eventIndex].Set();

                AssertWaitFor(() => pool.CompletedItems == 1);
                AssertWaitFor(() => pool.ActiveThreads == 2);
                Assert.AreEqual(0, pool.AvailableThreads);
                Assert.AreEqual(1, pool.QueueLength);
                Assert.AreEqual(3, pool.TotalQueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);

                Assert.IsTrue(activeWorkerEvents.TryDequeue(out eventIndex));
                workerEvents[eventIndex].Set();

                AssertWaitFor(() => pool.CompletedItems == 2);
                AssertWaitFor(() => pool.ActiveThreads == 2);
                Assert.AreEqual(0, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(2, pool.TotalQueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);

                Assert.IsTrue(activeWorkerEvents.TryDequeue(out eventIndex));
                workerEvents[eventIndex].Set();

                AssertWaitFor(() => pool.CompletedItems == 3);
                AssertWaitFor(() => pool.ActiveThreads == 1);
                Assert.AreEqual(1, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(1, pool.TotalQueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);

                Assert.IsTrue(activeWorkerEvents.TryDequeue(out eventIndex));
                workerEvents[eventIndex].Set();

                AssertWaitFor(() => pool.CompletedItems == 4);
                AssertWaitFor(() => pool.ActiveThreads == 0);
                Assert.AreEqual(2, pool.AvailableThreads);
                Assert.AreEqual(0, pool.QueueLength);
                Assert.AreEqual(0, pool.TotalQueueLength);
                Assert.AreEqual(0, pool.TotalExceptions);
            }
        }
        #endregion
    }
}
