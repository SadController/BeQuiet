// Creates a silent Windows audio session for integration testing. No audible samples.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

internal static class SilentAudioSession
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort FormatTag, Channels;
        public uint SamplesPerSecond, BytesPerSecond;
        public ushort BlockAlign, BitsPerSample, ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength, BytesRecorded;
        public IntPtr User;
        public uint Flags, Loops;
        public IntPtr Next, Reserved;
    }

    [DllImport("winmm.dll")]
    private static extern uint waveOutOpen(out IntPtr handle, uint device, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")]
    private static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")]
    private static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")]
    private static extern uint waveOutReset(IntPtr handle);
    [DllImport("winmm.dll")]
    private static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")]
    private static extern uint waveOutClose(IntPtr handle);

    private static void Check(uint result)
    {
        if (result != 0) throw new InvalidOperationException("WinMM error " + result);
    }

    private static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            if (args[0].StartsWith("wait:"))
            {
                string signal = args[0].Substring(5);
                while (!File.Exists(signal)) Thread.Sleep(100);
            }
            else Thread.Sleep(int.Parse(args[0]));
        }
        WaveFormat format = new WaveFormat { FormatTag = 1, Channels = 1, SamplesPerSecond = 8000, BytesPerSecond = 16000, BlockAlign = 2, BitsPerSample = 16 };
        IntPtr handle;
        Check(waveOutOpen(out handle, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0));
        IntPtr samples = Marshal.AllocHGlobal(16000);
        Marshal.Copy(new byte[16000], 0, samples, 16000);
        uint headerSize = (uint)Marshal.SizeOf(typeof(WaveHeader));
        IntPtr headerMemory = Marshal.AllocHGlobal((int)headerSize);
        WaveHeader header = new WaveHeader { Data = samples, BufferLength = 16000, Flags = 4 | 8, Loops = uint.MaxValue };
        Marshal.StructureToPtr(header, headerMemory, false);
        try
        {
            Check(waveOutPrepareHeader(handle, headerMemory, headerSize));
            Check(waveOutWrite(handle, headerMemory, headerSize));
            Thread.Sleep(300000);
        }
        finally
        {
            waveOutReset(handle);
            waveOutUnprepareHeader(handle, headerMemory, headerSize);
            waveOutClose(handle);
            Marshal.FreeHGlobal(headerMemory);
            Marshal.FreeHGlobal(samples);
        }
    }
}
