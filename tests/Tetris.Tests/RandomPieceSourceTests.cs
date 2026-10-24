using Tetris.Core.Domain;
using Tetris.Core.Game;

namespace Tetris.Tests;

public sealed class RandomPieceSourceTests
{
    [Fact]
    public void Bag_ReturnsEveryPieceOnceBeforeRepeating()
    {
        var source = new RandomPieceSource(seed: 42);
        var firstBag = Enumerable.Range(0, 7).Select(_ => source.NextType()).ToArray();
        Assert.Equal(7, firstBag.Distinct().Count());
        Assert.Equal(7, Enum.GetValues<TetrominoType>().Length);
    }
}
