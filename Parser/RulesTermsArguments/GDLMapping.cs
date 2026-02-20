using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Parser
{
	public class GDLMapping
	{
		public int[] LocalColumns;
		public int[] ForeignColumns;

		public GDLMapping(int length)
		{
			LocalColumns = new int[length];
			ForeignColumns = new int[length];
		}

		public GDLMapping(int[] localColumns, int[] foreignColumns)
		{
			LocalColumns = new int[localColumns.Length];
			ForeignColumns = new int[foreignColumns.Length];

			localColumns.CopyTo(LocalColumns, 0);
			foreignColumns.CopyTo(ForeignColumns, 0);
		}

		public override string ToString()
		{
			return string.Format("[{0}]<-[{1}]", string.Join(",", LocalColumns), string.Join(" ", ForeignColumns));
		}
		public int Length
		{
			get
			{
				return LocalColumns.Length;
			}
		}
	}
}
