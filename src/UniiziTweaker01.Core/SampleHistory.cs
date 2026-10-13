namespace UniiziTweaker01.Core;

/// <summary>Fixed-capacity history of samples used by the live charts.</summary>
public sealed class SampleHistory
{
    private readonly Queue<double> _items = new();
    private readonly int _capacity;

    public SampleHistory(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Count => _items.Count;

    public void Add(double value)
    {
        _items.Enqueue(value);
        while (_items.Count > _capacity) _items.Dequeue();
    }

    public double[] ToArray() => _items.ToArray();
}
