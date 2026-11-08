namespace MPRSGxZ.Library.Events;

public delegate Task AsyncEventHandler<T>(object sender, T e) where T : EventArgs;
