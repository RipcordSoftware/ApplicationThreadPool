using System;
using System.Threading;

using RipcordSoftware.ThreadPool;

namespace HelloTasks
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var pool = new ApplicationThreadPool("test", 16, 1024, true)) 
            {
                var tasks = new ApplicationThreadPool.TaskState[pool.MaxThreads];
                for (var i = 0; i < pool.MaxThreads; ++i) 
                {
                    tasks[i] = pool.QueueUserTask(o => Console.WriteLine("Hello from thread {0}", Thread.CurrentThread.ManagedThreadId));
                }

                ApplicationThreadPool.TaskState.WaitAll(tasks);

                Console.WriteLine("Finished");
                Console.ReadLine();
            }
        }
    }
}