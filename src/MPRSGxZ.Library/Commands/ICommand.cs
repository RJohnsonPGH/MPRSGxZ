namespace MPRSGxZ.Library.Commands;

public interface ICommand
{
	public int ExpectedLines { get; }

	public string ToString();
}
