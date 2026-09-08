// A watched application that is running, and how many copies of it there are.
using JKBar.Core.Settings;

namespace JKBar.Core.Presentation;

/// <param name="Count">More than one copy earns a number on the icon, so a busy application is not hidden.</param>
public sealed record RunningProcess(WatchedProcess Watched, int Count);
