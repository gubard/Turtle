namespace Turtle.Contract.Models;

public class CredentialsResponse
{
    public bool IsResponse { get; set; }
    public Credential[] Credentials { get; set; } = [];
}
