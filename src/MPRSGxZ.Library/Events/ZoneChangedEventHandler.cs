namespace MPRSGxZ.Library.Events;

public delegate void ZoneChangedEventHandler<T>(object sender, T e) where T : EventArgs;
