using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace thelastoneturnslightoff
{
    public class SharedResource : IDisposable
    {
        public int SequenceNumber { get; private set; }

        public SharedResource()
        {
            Debug.WriteLine("SharedResource created.");
        }

        public void Increment()
        {
            SequenceNumber++;
            Debug.WriteLine(SequenceNumber);
        }
        public void Dispose()
        {
            SequenceNumber = 0;
            Debug.WriteLine("SharedResource completed.");
        }
    }

    public class MyScheduler : LocalScheduler
    {
        private readonly IScheduler _scheduler;
        private TimeSpan _elapsed;
        public int Elements { get; private set; }
        public TimeSpan Elapsed
        {
            get { return _elapsed.TotalMilliseconds <= 10 ? TimeSpan.FromMilliseconds(10) : _elapsed; }
            set { _elapsed = value; }
        }

        public MyScheduler(IScheduler _scheduler)
        {
            this._scheduler = _scheduler;
            Elapsed = TimeSpan.FromMilliseconds(10);
        }

        public override IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            return _scheduler.Schedule(this, dueTime, (x, z) => {
                                               var sw = this.AsStopwatchProvider().StartStopwatch();
                                               z.Elements++;
                                               var t = action(this, state);
                                               z.Elements--;
                                               Elapsed = sw.Elapsed;
                                               return t;
                                           });
        }
    }

    class Program
    {
        public static int GetCPUCOunter()
        {

            var cpuCounter = new PerformanceCounter
                                                {
                                                    CategoryName = "Processor",
                                                    CounterName = "% Processor Time",
                                                    InstanceName = "_Total"
                                                };
            cpuCounter.NextValue();
            Thread.Sleep(TimeSpan.FromMilliseconds(10));
            var value = (int) cpuCounter.NextValue();
            return value == 0 ? 5 : value;
        }
        static void Main(string[] args)
        {
            var dis = new MyScheduler(Scheduler.Default);

            
            var f = new Subject<int>();
            f.Sum()
                .Do(x => Debug.WriteLine("sum done {0}", x), () => Debug.WriteLine("all sum done"))
                .Subscribe();

            Observable.Interval(TimeSpan.FromSeconds(60)).Do(x=>f.OnCompleted()).Subscribe();

            using (var test = Observable
                //.Range(1,int.MaxValue)
                .Generate(1, i => i <= 3000, i => i + 1, i => i, i =>
                                                                   {
                                                                       var cpu = GetCPUCOunter();
                                                                       var log = Math.Log(cpu) * Math.Log(cpu);
                                                                       var log2 = Math.Log(dis.Elapsed.TotalMilliseconds);
                                                                       var timeSpan = TimeSpan.FromMilliseconds(log * log2 * dis.Elements * 10);
                                                                       timeSpan = TimeSpan.FromMilliseconds(1);
                                                                       return timeSpan;
                                                                   }, Scheduler.CurrentThread)
                //.Take(25)
                //.Buffer(5)
                //.Select(_=>
                //            {
                //                Debug.WriteLine("Thread Id: {0}", Thread.CurrentThread.ManagedThreadId);
                //                for (var i = 0; i < int.MaxValue; i++){}
                //                return _.Sum();
                //            })
                .TakeUntil(f)
                .Window(2)
                    .Do(w => w.ObserveOn(dis)
                                            .Select(_ =>
                                            {
                                                for (var i = 0; i <1000000000; i++)
                                                {

                                                }
                                                //Debug.WriteLine("CPU: {0}", GetCPUCOunter());
                                                Debug.WriteLine("Thread Id: {0}", Thread.CurrentThread.ManagedThreadId);
                                                //Debug.WriteLine("Window Tick: {0}", _);
                                                //Debug.WriteLine("Scheduler in Window Tick: {0}", dis.Elements);
                                                return 1;
                                            }).Sum().Do(x=>f.OnNext(x)).Subscribe()
                )
                .Switch()
                .Do(_ => Debug.WriteLine("Done Tick: {0}, Thread Id: {1}, CPU: {2}", _, Thread.CurrentThread.ManagedThreadId, GetCPUCOunter()), () => Debug.WriteLine("all ticks done"))
                .Subscribe())
            {
                Console.ReadLine();
            }


            var underlaying = Observable
                //.Range(1,int.MaxValue)
                .Interval(TimeSpan.FromMilliseconds(1))
                .Take(int.MaxValue)
                .Do(_ => Debug.WriteLine("Tick: {0}", _), () => Debug.WriteLine("Ticks done"));


            var stream = Observable
                .Using(() => new SharedResource(), resource => underlaying.Do(l => resource.Increment()))
                .Publish()
                .RefCount(); ;

            var first = stream.Subscribe(_ => Debug.WriteLine("First: {0}", _), () => Debug.WriteLine("first done"));
            var second = stream.Subscribe(_ => Debug.WriteLine("Second: {0}", _), () => Debug.WriteLine("second done"));
            var third = stream.Subscribe(_ => Debug.WriteLine("Third: {0}", _), () => Debug.WriteLine("third done"));

            Console.ReadLine();
            first.Dispose();
            Console.ReadLine();
            second.Dispose();
            Console.ReadLine();
            third.Dispose();
            Console.ReadLine();
            var four = stream.Subscribe(_ => Debug.WriteLine("Four: {0}", _), () => Debug.WriteLine("four done"));
            Console.ReadLine();
            four.Dispose();
        }
    }
}
