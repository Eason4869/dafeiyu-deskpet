namespace DeskPet.Platform.Mac;

// Keep native transport separate from recorder lifecycle so cancellation and failure races
// can be exercised without a microphone, permissions, or a macOS process.
internal interface IMacMicrophone
{
    MicrophonePermission Access { get; }
    IntPtr Start(out int error);
    int Read(IntPtr device, byte[] buffer);
    void Stop(IntPtr device);
}
internal sealed class NativeMacMicrophone : IMacMicrophone
{
    public MicrophonePermission Access => MacServices.MicrophoneAccess;
    public IntPtr Start(out int error) { MacServices.EnsureMac(); return Native.MicrophoneStart(out error); }
    public int Read(IntPtr device, byte[] buffer) => Native.MicrophoneRead(device, buffer, buffer.Length);
    public void Stop(IntPtr device) => Native.MicrophoneStop(device);
}
