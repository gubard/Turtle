namespace Turtle.Contract.Models;

public class CreateCredential
{
    public string Name { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool IsAvailableUpperLatin { get; set; }
    public bool IsAvailableLowerLatin { get; set; }
    public bool IsAvailableNumber { get; set; }
    public bool IsAvailableSpecialSymbols { get; set; }
    public string CustomAvailableCharacters { get; set; } = string.Empty;
    public ushort Length { get; set; }
    public string Regex { get; set; } = string.Empty;
    public CredentialType Type { get; set; }
    public Guid? ParentId { get; set; }
}