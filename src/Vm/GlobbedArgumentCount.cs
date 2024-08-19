namespace Elk.Vm;

class GlobbedArgumentCount(int nonGlobbedCount)
{
    public int NonGlobbedCount { get; } = nonGlobbedCount;

    public int GlobbedCount { get; set; } = 0;
}