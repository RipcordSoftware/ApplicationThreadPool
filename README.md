# ThreadPool
An application thread pool for .NET or Mono

# Example
The short example below can be found in the examples directory in this repo.
```c#
static void Main(string[] args)
{
    var pool = new ApplicationThreadPool("test", 16, 1024, true);

    var activeThreads = pool.MaxThreads;
    for (var i = 0; i < pool.MaxThreads; ++i)
    {
        pool.QueueUserWorkItem(o => { Console.WriteLine("Hello"); Interlocked.Decrement(ref activeThreads); });
    }

    while (activeThreads > 0)
    {
        Thread.Sleep(0);
    }

    Console.WriteLine("Finished");
    Console.ReadLine();
}
```
