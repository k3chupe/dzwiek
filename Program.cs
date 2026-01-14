using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;

namespace CwiczenieKartaDzwiekowa
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MyForm());
        }
    }

    public class MyForm : Form
    {
        private Button btnPlaySound;
        private Button btnSystemPlayer;
        private Button btnHeader;
        private Button btnRecord;
        private Button btnWaveOut;
        private TextBox txtLog;

        private string wavFilePath = "test.wav";
        private string recordedFilePath = "nagranie.wav";

        private IntPtr hWaveIn = IntPtr.Zero;
        private IntPtr hWaveOut = IntPtr.Zero;
        private NativeMethods.WAVEHDR waveInHdr;
        private IntPtr pSaveBuffer = IntPtr.Zero;
        private int bufferSize = 44100 * 2 * 5; 

        public MyForm()
        {
            this.Text = "Lab 8 - Karta Dźwiękowa";
            this.Size = new Size(500, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. PlaySound
            btnPlaySound = new Button() { Text = "1. PlaySound", Location = new Point(20, 20), Size = new Size(200, 40) };
            btnPlaySound.Click += BtnPlaySound_Click;
            this.Controls.Add(btnPlaySound);

            // 2. Odtwarzacz
            btnSystemPlayer = new Button() { Text = "2. Otwórz w Odtwarzaczu", Location = new Point(240, 20), Size = new Size(200, 40) };
            btnSystemPlayer.Click += (s, e) => {
                if (File.Exists(wavFilePath)) 
                    Process.Start(new ProcessStartInfo(wavFilePath) { UseShellExecute = true });
                else 
                    MessageBox.Show("Brak pliku test.wav");
            };
            this.Controls.Add(btnSystemPlayer);

            // 3. Nagłówek
            btnHeader = new Button() { Text = "3. Pokaż Nagłówek WAV", Location = new Point(20, 80), Size = new Size(200, 40) };
            btnHeader.Click += BtnHeader_Click;
            this.Controls.Add(btnHeader);

            // 4. Nagraj
            btnRecord = new Button() { Text = "4. Nagraj (5 sek)", Location = new Point(20, 140), Size = new Size(200, 40) };
            btnRecord.Click += BtnRecord_Click;
            this.Controls.Add(btnRecord);

            // 5. WaveOut
            btnWaveOut = new Button() { Text = "5. Odtwórz (WaveOut)", Location = new Point(240, 140), Size = new Size(200, 40) };
            btnWaveOut.Click += BtnWaveOut_Click;
            this.Controls.Add(btnWaveOut);

            // 6. Log
            txtLog = new TextBox() { 
                Multiline = true, 
                ScrollBars = ScrollBars.Vertical, 
                Location = new Point(20, 200), 
                Size = new Size(440, 330),
                ReadOnly = true
            };
            this.Controls.Add(txtLog);
        }

        private void Log(string msg) => txtLog.AppendText(msg + "\r\n");

        // UWAGA: Zmieniono 'object sender' na 'object? sender' (dla CS8622)
        private void BtnPlaySound_Click(object? sender, EventArgs e)
        {
            if (!File.Exists(wavFilePath)) { MessageBox.Show("Brak pliku test.wav! Wgraj go do folderu."); return; }
            Log("PlaySound: Odtwarzanie...");
            NativeMethods.PlaySound(wavFilePath, IntPtr.Zero, NativeMethods.SND_FILENAME | NativeMethods.SND_ASYNC);
        }

        private void BtnHeader_Click(object? sender, EventArgs e)
        {
            if (!File.Exists(wavFilePath)) { MessageBox.Show("Brak pliku test.wav!"); return; }
            
            using (FileStream fs = new FileStream(wavFilePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                txtLog.Text = "--- NAGŁÓWEK WAV ---\r\n";
                // Prosty odczyt - bez zabezpieczeń (dla celów edukacyjnych ok)
                if (fs.Length < 44) { Log("Plik za krótki!"); return; }

                Log($"ChunkID: {new string(br.ReadChars(4))}");
                Log($"FileSize: {br.ReadInt32()}");
                Log($"Format: {new string(br.ReadChars(4))}");
                Log($"Subchunk1ID: {new string(br.ReadChars(4))}");
                Log($"Subchunk1Size: {br.ReadInt32()}");
                Log($"AudioFormat: {br.ReadInt16()}");
                Log($"Channels: {br.ReadInt16()}");
                Log($"SampleRate: {br.ReadInt32()}");
                br.ReadInt32(); 
                br.ReadInt16(); 
                Log($"BitsPerSample: {br.ReadInt16()}");
            }
        }

        private void BtnRecord_Click(object? sender, EventArgs e)
        {
            Log("Start nagrywania (5s)...");
            NativeMethods.WAVEFORMATEX fmt = new NativeMethods.WAVEFORMATEX();
            fmt.wFormatTag = 1; fmt.nChannels = 1; fmt.nSamplesPerSec = 44100; fmt.wBitsPerSample = 16;
            fmt.nBlockAlign = 2; fmt.nAvgBytesPerSec = 88200; fmt.cbSize = 0;

            // Tutaj był błąd CS8625 - w parametrze callback przekazujemy null, ale definicja wymagała Delegate. Poprawiono w NativeMethods.
            int res = NativeMethods.waveInOpen(out hWaveIn, -1, ref fmt, null, IntPtr.Zero, 0);
            if(res != 0) { Log($"Błąd waveInOpen: {res}"); return; }

            pSaveBuffer = Marshal.AllocHGlobal(bufferSize);
            waveInHdr = new NativeMethods.WAVEHDR { lpData = pSaveBuffer, dwBufferLength = (uint)bufferSize, dwFlags = 0 };

            NativeMethods.waveInPrepareHeader(hWaveIn, ref waveInHdr, Marshal.SizeOf(waveInHdr));
            NativeMethods.waveInAddBuffer(hWaveIn, ref waveInHdr, Marshal.SizeOf(waveInHdr));
            NativeMethods.waveInStart(hWaveIn);

            // Poprawiono Timer na System.Windows.Forms.Timer
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer() { Interval = 5000 };
            t.Tick += (s, args) => {
                t.Stop();
                NativeMethods.waveInStop(hWaveIn);
                NativeMethods.waveInClose(hWaveIn);
                
                using (FileStream fs = new FileStream(recordedFilePath, FileMode.Create))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    byte[] data = new byte[bufferSize];
                    Marshal.Copy(pSaveBuffer, data, 0, bufferSize);
                    
                    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(36 + bufferSize);
                    bw.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    bw.Write(16);
                    bw.Write((short)1); bw.Write((short)1); bw.Write(44100); bw.Write(88200); bw.Write((short)2); bw.Write((short)16);
                    bw.Write(Encoding.ASCII.GetBytes("data"));
                    bw.Write(bufferSize);
                    bw.Write(data);
                }
                Marshal.FreeHGlobal(pSaveBuffer);
                Log("Nagrano plik: " + recordedFilePath);
            };
            t.Start();
        }

        private void BtnWaveOut_Click(object? sender, EventArgs e)
        {
            if (!File.Exists(recordedFilePath)) { MessageBox.Show("Najpierw nagraj dźwięk!"); return; }
            Log("WaveOut: Przygotowanie...");

            byte[] rawData = File.ReadAllBytes(recordedFilePath);
            int dataLen = rawData.Length - 44;
            if(dataLen <= 0) return;

            IntPtr pBuffer = Marshal.AllocHGlobal(dataLen);
            Marshal.Copy(rawData, 44, pBuffer, dataLen);

            NativeMethods.WAVEFORMATEX fmt = new NativeMethods.WAVEFORMATEX();
            fmt.wFormatTag = 1; fmt.nChannels = 1; fmt.nSamplesPerSec = 44100; fmt.wBitsPerSample = 16;
            fmt.nBlockAlign = 2; fmt.nAvgBytesPerSec = 88200; fmt.cbSize = 0;

            // Tutaj też był błąd CS8625 - poprawiono definicję waveOutOpen
            NativeMethods.waveOutOpen(out hWaveOut, -1, ref fmt, null, IntPtr.Zero, 0);
            
            NativeMethods.WAVEHDR hdr = new NativeMethods.WAVEHDR { lpData = pBuffer, dwBufferLength = (uint)dataLen, dwFlags = 0 };
            NativeMethods.waveOutPrepareHeader(hWaveOut, ref hdr, Marshal.SizeOf(hdr));
            NativeMethods.waveOutWrite(hWaveOut, ref hdr, Marshal.SizeOf(hdr));
            Log("WaveOut: Odtwarzanie rozpoczęte.");
        }
    }

    public static class NativeMethods
    {
        public const uint SND_FILENAME = 0x00020000;
        public const uint SND_ASYNC = 0x0001;

        [DllImport("winmm.dll", EntryPoint = "PlaySound")]
        public static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEFORMATEX { public ushort wFormatTag; public ushort nChannels; public uint nSamplesPerSec; public uint nAvgBytesPerSec; public ushort nBlockAlign; public ushort wBitsPerSample; public ushort cbSize; }

        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength; public uint dwBytesRecorded; public IntPtr dwUser; public uint dwFlags; public uint dwLoops; public IntPtr lpNext; public IntPtr reserved; }

        // Zmiana: Delegate? dwCallback (znak zapytania pozwala na null)
        [DllImport("winmm.dll")] public static extern int waveInOpen(out IntPtr hWaveIn, int uDeviceID, ref WAVEFORMATEX lpFormat, Delegate? dwCallback, IntPtr dwInstance, int dwFlags);
        [DllImport("winmm.dll")] public static extern int waveInPrepareHeader(IntPtr hWaveIn, ref WAVEHDR lpWaveHdr, int uSize);
        [DllImport("winmm.dll")] public static extern int waveInAddBuffer(IntPtr hWaveIn, ref WAVEHDR lpWaveHdr, int uSize);
        [DllImport("winmm.dll")] public static extern int waveInStart(IntPtr hWaveIn);
        [DllImport("winmm.dll")] public static extern int waveInStop(IntPtr hWaveIn);
        [DllImport("winmm.dll")] public static extern int waveInClose(IntPtr hWaveIn);

        // Zmiana: Delegate? dwCallback
        [DllImport("winmm.dll")] public static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WAVEFORMATEX lpFormat, Delegate? dwCallback, IntPtr dwInstance, int dwFlags);
        [DllImport("winmm.dll")] public static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WAVEHDR lpWaveHdr, int uSize);
        [DllImport("winmm.dll")] public static extern int waveOutWrite(IntPtr hWaveOut, ref WAVEHDR lpWaveHdr, int uSize);
        [DllImport("winmm.dll")] public static extern int waveOutClose(IntPtr hWaveOut);
    }
}