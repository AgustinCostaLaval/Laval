namespace LAVAL.Web.Models;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string ResendApiKey { get; set; } = string.Empty;
    public string ResendFrom { get; set; } = string.Empty;

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "lavalserviciossrl@gmail.com";
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "lavalserviciossrl@gmail.com";
    public string To { get; set; } = "lavalserviciossrl@gmail.com";
}
