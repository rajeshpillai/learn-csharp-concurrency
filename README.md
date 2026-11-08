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

More parts are added as their videos are published.

This is teaching code. It is kept simple on purpose, so every line can be followed. It is not
meant for production.
