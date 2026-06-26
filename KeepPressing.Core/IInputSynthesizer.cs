namespace KeepPressing.Core;

/// <summary>Core's only side-effect boundary. Implemented by Win32 SendInput in the app and by a fake in tests.</summary>
public interface IInputSynthesizer
{
    void Press(InputTarget target);

    /// <summary>Releasing a target that is not pressed must be a harmless no-op.</summary>
    void Release(InputTarget target);

    /// <summary>One press-release. Implementations may override to send both events as an atomic batch.</summary>
    void Tap(InputTarget target)
    {
        Press(target);
        Release(target);
    }
}
