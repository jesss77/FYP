namespace FYP.DTOs
{
    public class RegistrationResult
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public IEnumerable<string> Errors { get; set; } = Array.Empty<string>();
    }
}
