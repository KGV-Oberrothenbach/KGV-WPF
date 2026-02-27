namespace KGV.ViewModels
{
    public class LoginStorage
    {
        public string Email { get; set; } = string.Empty;   // Default-Wert
        public bool RememberEmail { get; set; } = false;

        public void Save()
        {
            // Hier speichern (z.B. Datei, Settings)
        }

        public static LoginStorage Load()
        {
            // Hier laden (z.B. Datei, Settings)
            return new LoginStorage
            {
                Email = "",
                RememberEmail = false
            };
        }
    }
}