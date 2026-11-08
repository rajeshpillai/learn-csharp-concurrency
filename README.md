# Learn C# Concurrency by Building Things

The code for the video series on Unlearning Labs. Each part builds one real program from an empty
folder, breaks it on purpose where concurrency goes wrong, and fixes it. Every step the video types
is here, and every terminal the video shows was captured from these programs, in each part's
`output/` folder.

For beginners in concurrency: you need C# basics and a terminal. The videos use .NET 11 on Linux;
the code also runs on .NET 10.

| Part | We build | The question |
|---|---|---|
| [1. Threads and cores](part-01-threads-and-cores/) | a Mandelbrot renderer | Who runs your code, and why don't 12 threads make it 12 times faster? |
| [2. Shared state](part-02-shared-state/) | a concert ticket booking engine | Why does `seats--` sell seats that don't exist? |
| [3. Atomics and the hardware](part-03-atomics-and-the-hardware/) | a metrics library | Why does one hot counter make 10 cores slower than 1? |
| [4. The thread pool and Task](part-04-thread-pool-and-task/) | our own thread pool, then our own Task | If threads are expensive, who runs `Task.Run`? |
| [5. async/await in use](part-05-async-await/) | a TCP chat server | How do 10,000 connections fit on 12 threads? |
| [6. Under await](part-06-under-await/) | async/await from scratch | What does `await` actually save, and where does it resume? |

More parts are added as their videos are published.

This is teaching code. It is kept simple on purpose, so every line can be followed. It is not
meant for production.
