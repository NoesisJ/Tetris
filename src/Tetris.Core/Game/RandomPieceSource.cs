using Tetris.Core.Domain;

namespace Tetris.Core.Game;

public sealed class RandomPieceSource : IGamePieceSource
{
    private readonly Random _random;
    private readonly List<TetrominoType> _bag = [];

    public RandomPieceSource(int? seed = null) => _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;

    public TetrominoType NextType()
    {
        if (_bag.Count == 0)
        {
            _bag.AddRange(Enum.GetValues<TetrominoType>());
            for (var i = _bag.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
        }

        var result = _bag[^1];
        _bag.RemoveAt(_bag.Count - 1);
        return result;
    }
}
