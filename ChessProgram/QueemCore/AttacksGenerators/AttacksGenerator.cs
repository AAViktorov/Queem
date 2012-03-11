using System;

namespace QueemCore.AttacksGenerators
{
	public abstract class AttacksGenerator
	{
		public abstract ulong GetAttacks(Square figureSquare, ulong otherFigures);
	}
}
