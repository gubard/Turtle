namespace Turtle.Contract.Models;

public class EditCredential
{
    public Guid[] Ids { get; set; } = [];
    public bool IsEditName { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEditLogin { get; set; }
    public string Login { get; set; } = string.Empty;
    public bool IsEditKey { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool IsEditIsAvailableUpperLatin { get; set; }
    public bool IsAvailableUpperLatin { get; set; }
    public bool IsEditIsAvailableLowerLatin { get; set; }
    public bool IsAvailableLowerLatin { get; set; }
    public bool IsEditIsAvailableNumber { get; set; }
    public bool IsAvailableNumber { get; set; }
    public bool IsEditIsAvailableSpecialSymbols { get; set; }
    public bool IsAvailableSpecialSymbols { get; set; }
    public bool IsEditCustomAvailableCharacters { get; set; }
    public string CustomAvailableCharacters { get; set; } = string.Empty;
    public bool IsEditLength { get; set; }
    public ushort Length { get; set; }
    public bool IsEditRegex { get; set; }
    public string Regex { get; set; } = string.Empty;
    public bool IsEditType { get; set; }
    public CredentialType Type { get; set; }
    public bool IsEditParentId { get; set; }
    public Guid? ParentId { get; set; }
}
