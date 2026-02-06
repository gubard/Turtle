namespace Turtle.Contract.Models;

public sealed class CredentialSelector
{
    public Credential Item { get; set; } = new();
    public CredentialSelector[] Children { get; set; } = [];
}
