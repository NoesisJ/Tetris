using Tetris.Core.Domain;

namespace Tetris.Core.Game;

public interface IGamePieceSource
{
    TetrominoType NextType();
}
