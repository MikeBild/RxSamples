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
class Program
{
    static void Main(string[] args)
    {
        var underlaying = Observable
            .Interval(TimeSpan.FromSeconds(1))
            .Take(10)
            .Do(_ => Debug.WriteLine("Tick: {0}", _), () => Debug.WriteLine("Ticks done"));
                
 
        var stream = Observable
            .Using(() => new SharedResource(), resource => underlaying.Do(l => resource.Increment()))
            .Publish()
            .RefCount(); ;
 
        var first = stream.Subscribe(_ => Debug.WriteLine("First: {0}", _), () => Debug.WriteLine("first done"));
        var second = stream.Subscribe(_ => Debug.WriteLine("Second: {0}", _), () => Debug.WriteLine("second done"));
 
        Console.ReadLine();
        first.Dispose();
        Console.ReadLine();
        second.Dispose();
        Console.ReadLine();
    }
}
