namespace KeepPressing.Core;

public enum MouseButton
{
    Left,
    Right,
    Middle,
}

public abstract record InputTarget
{
    private InputTarget() { }

    /// <summary>Mouse button. A null <paramref name="Position"/> presses at the current cursor position.</summary>
    public sealed record Mouse(MouseButton Button, ScreenPoint? Position = null) : InputTarget;

    public sealed record Key(KeyCode Code) : InputTarget;
}
