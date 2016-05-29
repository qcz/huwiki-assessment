namespace AssessmentDotNet
{
	class Importance
	{
		public int Value { get; private set; }

		public override string ToString()
		{
			return Program.ImportanceCategories[Value];
		}

		public Importance(int value)
		{
			this.Value = value;
		}
	}

}