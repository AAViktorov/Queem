using System;
using Queem.Core.History;

namespace Queem.Core.ChessBoard
{
	public class GameProvider
	{
		public PlayerBoard PlayerBoard1 { get; private set; }
		public PlayerBoard PlayerBoard2 { get; private set; }
				
		public MovesHistory History { get; private set; }
		
		protected PlayerBoard[] playerBoards;
	
		public GameProvider ()
		{
			this.PlayerBoard1 = new PlayerBoard(PlayerPosition.Down, Color.White);
			this.PlayerBoard2 = new PlayerBoard(PlayerPosition.Up, Color.Black);
			
			this.History = new MovesHistory();
			
			this.playerBoards = new PlayerBoard[2];
			this.playerBoards[(int)Color.White] = this.PlayerBoard1;
			this.playerBoards[(int)Color.Black] = this.PlayerBoard2;
		}
		
		
        public void ForEachFigure(Action<Square, Figure> action)
        {
            for (int i = 0; i < 64; ++i)
            {
                var figure1 = this.PlayerBoard1.Figures[i];
                var figure2 = this.PlayerBoard2.Figures[i];

#if DEBUG
                if ((figure1 != Figure.Nobody) &&
                    (figure2 != Figure.Nobody))
                    throw new InvalidOperationException();
#endif
                if (figure1 != Figure.Nobody)
                    action((Square)i, figure1);
                else
                    action((Square)i, figure2);
            }
        }

        public void ForEachFigureReal(Action<Square, Figure> action)
        {
            for (int rank = 7; rank >= 0; rank--)
            {
                for (int file = 0; file < 8; ++file)
                {
                    int index = rank * 8 + file;

                    var figure1 = this.PlayerBoard1.Figures[index];
                    var figure2 = this.PlayerBoard2.Figures[index];

#if DEBUG
                    if ((figure1 != Figure.Nobody) &&
                        (figure2 != Figure.Nobody))
                        throw new InvalidOperationException();
#endif

                    if (figure1 != Figure.Nobody)
                        action((Square)index, figure1);
                    else
                        action((Square)index, figure2);
                }
            }
        }

		/*
         * Possible problematic situations
         * 
         * ----------Pawn-----------
         *  1. move to in-passing state
         *  2. move from in-passing state
         *  3. kill other pawn that is in passing state
         *  4. just move
         *  5. kill a rook in state with possible castling
         * 
         * 
         * ----------Rook----------
         *  1. kill a pawn in a passing state
         *  2. move from castling state
         *  3. be a part of a castling
         *  4. kill other rook in a castling state
         *  5. just move
         * 
         * 
         * ----------King----------
         *  1. move from a castling state
         *  2. kill a pawn in a passing state
         *  3. kill a rook in a castling state
        */
		public void ProcessMove(Move move, Color color)
		{
			var oppositeColor = (Color)(1 - (int)color);
			
			var playerBoard1 = this.playerBoards[(int)color];
			var playerBoard2 = this.playerBoards[(int)oppositeColor];
			
			var figureMoving = playerBoard1.Figures[(int)move.From];
			var destinationFigure = playerBoard2.Figures[(int)move.To];
			
			var lastMove = this.History.GetLastMove();
			
			this.History.AddItem(move);

			var deltaChange = this.History.GetLastDeltaChange();
			var moveChange = deltaChange.GetNext(MoveAction.Move);
			
			moveChange.Square = move.From;
			moveChange.AdditionalSquare = move.To;
			moveChange.FigureColor = color;
			moveChange.FigureType = figureMoving;
			moveChange.Data = playerBoard1.GetBoardProperty(figureMoving);
			
			playerBoard1.ProcessMove(move, figureMoving);
			
			if (destinationFigure != Figure.Nobody)
			{
				var killChange = deltaChange.GetNext(MoveAction.Deletion);
			
				killChange.Square = move.To;
				killChange.Data = playerBoard2.GetBoardProperty(destinationFigure);
				killChange.FigureType = destinationFigure;
				killChange.FigureColor = oppositeColor;
				
				playerBoard2.RemoveFigure(move.To, destinationFigure);
				
				return;
			}
			
			if (move.Type == MoveType.EpCapture)
			{
				var passingKillChange = deltaChange.GetNext(MoveAction.Deletion);
				
				passingKillChange.Square = lastMove.To;
				passingKillChange.FigureType = Figure.Pawn;
				passingKillChange.FigureColor = oppositeColor;
				
				playerBoard2.RemoveFigure(passingKillChange.Square, Figure.Pawn);
				
				return;
			}
			
			if (move.Type == MoveType.KingCastle)
			{
				int moveTo = (int)move.To;
				int moveFrom = (int)move.From;				
				// will be +2 or -2
				int difference = moveTo - moveFrom;
				// same as sign of difference
				difference = difference / 2;
				var rookMoveChange = deltaChange.GetNext(MoveAction.Move);
				
				// rook target
				rookMoveChange.AdditionalSquare = (Square)(moveTo - difference);
				
				// rook source
				// diff -> [1 -> 1, -1 -> 0]
				difference = (difference + 1) / 2;
				rookMoveChange.Square = (Square)((moveFrom / 8) * 8 + difference*7);
				
				rookMoveChange.FigureType = Figure.Rook;
				rookMoveChange.FigureColor = color;
				rookMoveChange.Data = playerBoard1.GetBoardProperty(Figure.Rook);
				
				playerBoard1.ProcessMove(
					new Move(rookMoveChange.Square, rookMoveChange.AdditionalSquare),
					Figure.Rook);
					
				return;
			}
		}	
		
		public void CancelLastMove(Color color)
		{
			var oppositeColor = (Color)(1 - (int)color);
			
			var playerBoard1 = this.playerBoards[(int)color];
			var playerBoard2 = this.playerBoards[(int)oppositeColor];
			
			var deltaChanges = this.History.PopLastDeltaChange();
			
			while (deltaChanges.HasItems())
			{
				var change = deltaChanges.PopLast();
				
				switch (change.Action)
				{
				case MoveAction.PawnChange:
					playerBoard1.AddFigure(change.Square, change.FigureType);
					playerBoard1.SetProperty(change.FigureType, change.Data);
					break;
					
				case MoveAction.Deletion:
				
					playerBoard2.AddFigure(change.Square, change.FigureType);
					playerBoard2.SetProperty(change.FigureType, change.Data);
					break;
					
				case MoveAction.Move:
				
					playerBoard1.CancelMove((int)change.Square, (int)change.AdditionalSquare);
					playerBoard1.SetProperty(change.FigureType, change.Data);
					break;
					
				case MoveAction.Creation:
					playerBoard1.RemoveFigure(change.Square, change.FigureType);
					break;
				}
			}
		}
		
		public void PromotePawn(Color color, Square square, Figure newFigure)
		{
			var playerBoard = this.playerBoards[(int)color];			
			var deltaChanges = this.History.GetLastDeltaChange();
			
			var deleteChange = deltaChanges.GetNext(MoveAction.PawnChange);
			deleteChange.Square = square;
			deleteChange.FigureType = Figure.Pawn;
			deleteChange.FigureColor = color;
			
			var createChange = deltaChanges.GetNext(MoveAction.Creation);
			createChange.Square = square;
			createChange.FigureType = newFigure;
			createChange.FigureColor = color;
			
			playerBoard.RemoveFigure(square, Figure.Pawn);
			playerBoard.AddFigure(square, newFigure);
		}
		
		public bool IsUnderCheck(Color color)
		{
			var oppositeColor = 1 - (int)color;
			
			var player = this.playerBoards[(int)color];
			var opponent = this.playerBoards[oppositeColor];
			
			return player.IsUnderCheck(opponent);
		}
		
		public bool IsCheckmate(Color color)
		{
			var oppositeColor = 1 - (int)color;
			
			var player = this.playerBoards[(int)color];
			var opponent = this.playerBoards[oppositeColor];
			
			if(!player.IsUnderCheck(opponent))
				return false;
		
			return this.AreNoMoves(player, opponent);
		}		
		
		public bool IsStalemate(Color color)
		{
			var oppositeColor = 1 - (int)color;
			
			var player = this.playerBoards[(int)color];
			var opponent = this.playerBoards[oppositeColor];
			
			if(player.IsUnderCheck(opponent))
				return false;
		
			return this.AreNoMoves(player, opponent);
		}
		
		private bool AreNoMoves(PlayerBoard player, PlayerBoard opponent)
		{
			var moves = player.GetMoves(opponent, 
										this.History.GetLastMove(), 
										MovesMask.AllMoves);
			
			player.FilterMoves(opponent, moves);
			bool result = (moves.Size == 0);
			MovesArray.ReleaseLast();
			return result;
		}
	}
}

