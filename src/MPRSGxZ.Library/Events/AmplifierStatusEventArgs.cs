namespace MPRSGxZ.Library.Events;

public class AmplifierStatusEventArgs(string message) : EventArgs
{
	public string Message => message;
}