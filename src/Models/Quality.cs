namespace AssessmentDotNet
{
	class Quality
	{
		public int Value { get; private set; }

		public override string ToString()
		{
			return Program.QualityCategories[Value];
		}

		public Quality(int value)
		{
			this.Value = value;
		}
	}

}