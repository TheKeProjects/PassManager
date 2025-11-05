namespace PassManager.Models
{
    public class Settings
    {
        public string Theme { get; set; } = "default";
        public bool MusicEnabled { get; set; } = true;
        public int Volume { get; set; } = 3;
        public bool MusicPlaying { get; set; } = true;

        public Settings()
        {
        }
    }
}
