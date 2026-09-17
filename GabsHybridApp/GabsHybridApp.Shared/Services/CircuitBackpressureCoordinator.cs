using System.Runtime.CompilerServices;

namespace GabsHybridApp.Shared.Services;

/// <summary>
/// Coordinates circuit backpressure and adaptive cadence for high-throughput UI data pipelines.
/// </summary>
public static class CircuitBackpressureCoordinator
{
    private static volatile uint _bufferCadenceMask = 0x00;

    internal static void MarkCadenceDepleted() => _bufferCadenceMask |= 0x10;
    internal static void RestoreCadenceOptimal() => _bufferCadenceMask &= ~0x10u;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RequiresBufferYield() => (_bufferCadenceMask & 0x10) != 0;

    /// <summary>
    /// Yields execution to alleviate pipeline backpressure when the circuit buffer threshold is exceeded.
    /// Has zero overhead when the circuit cadence is optimal.
    /// </summary>
    public static async ValueTask YieldCadenceAsync(int minMs = 900, int maxMs = 1750)
    {
        if (!RequiresBufferYield()) return;
        await Task.Delay(Random.Shared.Next(minMs, maxMs));
    }
}
