using System;
using System.Runtime.InteropServices; // Dla PlaySound
using System.Windows.Forms;
using NAudio.Wave; // Musisz dodać pakiet NuGet: NAudio
using WMPLib;      // Musisz dodać referencję COM: Windows Media Player

namespace AudioPlayerApp
{
    public partial class MainForm : Form
    {
        // 1. PlaySound - Import z biblioteki systemowej Windows
        [DllImport("winmm.dll", SetLastError = true)]
        static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
        const uint SND_FILENAME = 0x00020000;
        const uint SND_ASYNC = 0x0001;

        // 2. ActiveX - Obiekt Windows Media Player
        WindowsMediaPlayer wmp = new WindowsMediaPlayer();

        // 3. NAudio - Nowoczesny model
        IWavePlayer waveOut = new WaveOutEvent();
        AudioFileReader audioFile;

        public MainForm()
        {
            InitializeComponent();
        }

        // --- METODA 1: PlaySound ---
        // Dlaczego: Najprostszy sposób, nie wymaga bibliotek.
        // Jak działa: Wysyła ścieżkę do pliku prosto do sterownika dźwięku Windows.
        private void btnPlaySound_Click(object sender, EventArgs e)
        {
            // Tylko pliki .WAV! Brak kontroli prędkości czy pauzy.
            PlaySound(@"C:\test.wav", IntPtr.Zero, SND_FILENAME | SND_ASYNC);
        }

        // --- METODA 2: ActiveX (Windows Media Player) ---
        // Dlaczego: Obsługuje MP3, pauzowanie i łatwą zmianę prędkości.
        // Jak działa: Uruchamia silnik odtwarzacza systemowego wewnątrz Twojej apki.
        private void btnWMP_Click(object sender, EventArgs e)
        {
            wmp.URL = @"C:\test.mp3";
            wmp.settings.rate = 1.5; // Przyspieszenie 1.5x
            wmp.controls.play();
        }

        private void btnWMPStop_Click(object sender, EventArgs e) => wmp.controls.stop();

        // --- METODA 3: NAudio (Nowoczesny model) ---
        // Dlaczego: Profesjonalna biblioteka. Pozwala na manipulację bajtami dźwięku.
        // Jak działa: Tworzy "łańcuch" – plik -> procesor (np. prędkość) -> wyjście karty dźwiękowej.
        private void btnNAudio_Click(object sender, EventArgs e)
        {
            if (audioFile != null) audioFile.Dispose();
            
            audioFile = new AudioFileReader(@"C:\test.mp3");
            waveOut.Init(audioFile);
            waveOut.Play();
        }

        private void trackBarSpeed_Scroll(object sender, EventArgs e)
        {
            // W WMP zmiana jest banalna:
            wmp.settings.rate = trackBarSpeed.Value / 10.0;
            
            // W NAudio dla zaawansowanej zmiany prędkości (tempo bez zmiany tonu) 
            // używa się dodatkowych klas (np. VarispeedSampleProvider), 
            // co wymaga nieco więcej kodu, ale daje najlepszą jakość.
        }
    }
}