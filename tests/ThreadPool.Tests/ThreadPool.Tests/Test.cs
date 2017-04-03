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

        private static void AssertWaitFor(WaitFunction func, int timeout)
        {
            Assert.IsTrue(WaitFor(func, timeout));
        }
        #endregion

        #region Tests
        [Test()]
        public void TestInitialState()
        {
            var pool = new ApplicationThreadPool("test", 2, 8, true);

            Assert.AreEqual(0, pool.ActiveThreads);
            Assert.AreEqual(2, pool.MaxThreads);
            Assert.AreEqual(2, pool.AvailableThreads);
            Assert.AreEqual(8, pool.MaxQueueLength);
            Assert.AreEqual(0, pool.QueueLength);
            Assert.AreEqual(0, pool.TotalExceptions);
            Assert.AreEqual(0, pool.TotalQueueLength);
        }

        [Test()]
        public void TestSingleThread()
        {
            var pool = new ApplicationThreadPool("test", 2, 8, true);

            var finished = false;
            object value = null;
            pool.QueueUserWorkItem(o => { finished = true; });

            AssertWaitFor(() => finished, 5000);
            AssertWaitFor(() => pool.ActiveThreads == 0, 1000);

            Assert.AreEqual(null, value);

            Assert.AreEqual(0, pool.ActiveThreads);
            Assert.AreEqual(2, pool.AvailableThreads);
            Assert.AreEqual(0, pool.QueueLength);
            Assert.AreEqual(0, pool.TotalExceptions);
            Assert.AreEqual(0, pool.TotalQueueLength);
        }

        [Test()]
        public void TestSingleThreadWithValue()
        {
            var pool = new ApplicationThreadPool("test", 2, 8, true);

            var finished = false;
            object value = null;
            pool.QueueUserWorkItem(o => { value = o; finished = true; }, "Hello world");

            AssertWaitFor(() => finished, 5000);
            AssertWaitFor(() => pool.ActiveThreads == 0, 1000);

            Assert.AreEqual(typeof(string), value.GetType());
            Assert.AreEqual("Hello world", (string)value);

            Assert.AreEqual(0, pool.ActiveThreads);
            Assert.AreEqual(2, pool.AvailableThreads);
            Assert.AreEqual(0, pool.QueueLength);
            Assert.AreEqual(0, pool.TotalExceptions);
            Assert.AreEqual(0, pool.TotalQueueLength);
        }

        [Test()]
        public void TestFourThreads()
        {
            var pool = new ApplicationThreadPool("test", 2, 8, true);

            var finished = 0;
            pool.QueueUserWorkItem(o => { Interlocked.Increment(ref finished); });
            pool.QueueUserWorkItem(o => { Interlocked.Increment(ref finished); });
            pool.QueueUserWorkItem(o => { Interlocked.Increment(ref finished); });
            pool.QueueUserWorkItem(o => { Interlocked.Increment(ref finished); });

            AssertWaitFor(() => finished == 4, 5000);
            AssertWaitFor(() => pool.ActiveThreads == 0, 1000);

            Assert.AreEqual(0, pool.ActiveThreads);
            Assert.AreEqual(2, pool.AvailableThreads);
            Assert.AreEqual(0, pool.QueueLength);
            Assert.AreEqual(0, pool.TotalExceptions);
            Assert.AreEqual(0, pool.TotalQueueLength);
        }

        [Test()]
        public void TestMultipleThreads()
        {
            const int maxWorkItems = 1024;
            var pool = new ApplicationThreadPool("test", 2, maxWorkItems, true);

            var finished = 0;
            for (var i = 0; i < maxWorkItems; ++i)
            {
                pool.QueueUserWorkItem(o => { Interlocked.Increment(ref finished); });
            }

            AssertWaitFor(() => finished == maxWorkItems, 5000);
            AssertWaitFor(() => pool.ActiveThreads == 0, 1000);

            Assert.AreEqual(0, pool.ActiveThreads);
            Assert.AreEqual(2, pool.AvailableThreads);
            Assert.AreEqual(0, pool.QueueLength);
            Assert.AreEqual(0, pool.TotalExceptions);
            Assert.AreEqual(0, pool.TotalQueueLength);
        }

        [Test()]
        public void TestSingleThreadException()
        {
            var pool = new ApplicationThreadPool("test", 2, 8, true);

            var finished = false;
            pool.QueueUserWorkItem(o => { finished = true; throw new Exception(); });

            AssertWaitFor(() => finished, 5000);
            AssertWaitFor(() => pool.ActiveThreads == 0, 1000);

            Assert.AreEqual(0, pool.ActiveThreads);
            Assert.AreEqual(2, pool.AvailableThreads);
            Assert.AreEqual(0, pool.QueueLength);
            Assert.AreEqual(1, pool.TotalExceptions);
            Assert.AreEqual(0, pool.TotalQueueLength);
        }

        [Test()]
        public void TestSingleTask()
        {
            var pool = new ApplicationThreadPool("test", 2, 8, true);

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
        }

        [Test()]
        public void TestSingleTaskException()
        {
            var pool = new ApplicationThreadPool("test", 2, 8, true);

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
        }

        [Test()]
        public void TestMultipleTasks()
        {
            const int maxWorkItems = 1024;
            var pool = new ApplicationThreadPool("test", 2, maxWorkItems, true);

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
        }

        [Test]
        public void TestSingleTypedThread()
        {
            var pool = new ApplicationThreadPool<string>("test", 2, 8, true);

            var finished = false;
            Type paramType = null;
            string paramValue = null;
            pool.QueueUserWorkItem(o => { paramValue = o; paramType = o.GetType(); finished = true; }, "Hello world");

            AssertWaitFor(() => finished, 5000);
            AssertWaitFor(() => pool.ActiveThreads == 0, 1000);

            Assert.IsTrue(paramType == typeof(string));
            Assert.AreEqual("Hello world", paramValue);

            Assert.AreEqual(0, pool.ActiveThreads);
            Assert.AreEqual(2, pool.AvailableThreads);
            Assert.AreEqual(0, pool.QueueLength);
            Assert.AreEqual(0, pool.TotalExceptions);
            Assert.AreEqual(0, pool.TotalQueueLength);
        }

        [Test()]
        public void TestFourTypedThreads()
        {
            var pool = new ApplicationThreadPool<string>("test", 2, 8, true);

            var values = new ConcurrentDictionary<string, bool>();
            var finished = 0;
            var count = 0;
            pool.QueueUserWorkItem(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString());
            pool.QueueUserWorkItem(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString());
            pool.QueueUserWorkItem(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString());
            pool.QueueUserWorkItem(o => { values[o] = true; Interlocked.Increment(ref finished); }, (count++).ToString());

            AssertWaitFor(() => finished == 4, 5000);
            AssertWaitFor(() => pool.ActiveThreads == 0, 1000);

            Assert.AreEqual(4, values.Count);

            Assert.AreEqual(0, pool.ActiveThreads);
            Assert.AreEqual(2, pool.AvailableThreads);
            Assert.AreEqual(0, pool.QueueLength);
            Assert.AreEqual(0, pool.TotalExceptions);
            Assert.AreEqual(0, pool.TotalQueueLength);
        }

        [Test()]
        public void TestMultipleTypedThreads()
        {
            const int maxWorkItems = 1024;
            var pool = new ApplicationThreadPool<string>("test", 2, maxWorkItems, true);

            var finished = 0;
            var values = new ConcurrentDictionary<string, bool>();
            for (var i = 0; i < maxWorkItems; ++i)
            {
                pool.QueueUserWorkItem(o => { values[o] = true; Interlocked.Increment(ref finished); }, i.ToString());
            }

            AssertWaitFor(() => finished == maxWorkItems, 5000);
            AssertWaitFor(() => pool.ActiveThreads == 0, 1000);

            Assert.AreEqual(maxWorkItems, values.Count);

            Assert.AreEqual(0, pool.ActiveThreads);
            Assert.AreEqual(2, pool.AvailableThreads);
            Assert.AreEqual(0, pool.QueueLength);
            Assert.AreEqual(0, pool.TotalExceptions);
            Assert.AreEqual(0, pool.TotalQueueLength);
        }

        [Test()]
        public void TestSingleTypedThreadException()
        {
            var pool = new ApplicationThreadPool<string>("test", 2, 8, true);

            var finished = false;
            Type paramType = null;
            string paramValue = null;
            pool.QueueUserWorkItem(o => { paramValue = o; paramType = o.GetType(); finished = true; throw new Exception(); }, "Hello world");

            AssertWaitFor(() => finished, 5000);
            AssertWaitFor(() => pool.ActiveThreads == 0, 1000);

            Assert.IsTrue(paramType == typeof(string));
            Assert.AreEqual("Hello world", paramValue);

            Assert.AreEqual(0, pool.ActiveThreads);
            Assert.AreEqual(2, pool.AvailableThreads);
            Assert.AreEqual(0, pool.QueueLength);
            Assert.AreEqual(1, pool.TotalExceptions);
            Assert.AreEqual(0, pool.TotalQueueLength);
        }

        [Test()]
        public void TestSingleTypedTask()
        {
            var pool = new ApplicationThreadPool<string>("test", 2, 8, true);

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
        }

        [Test()]
        public void TestFourTypedTasks()
        {
            var pool = new ApplicationThreadPool<string>("test", 2, 8, true);

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
        }
        #endregion
    }
}

