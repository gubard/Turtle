using Turtle.Contract.Models;

namespace Turtle.Contract.Helpers;

public static class Mapper
{
    public static CredentialEntity ToCredentialEntity(this Credential credential)
    {
        return new()
        {
            Name = credential.Name,
            Login = credential.Login,
            Key = credential.Key,
            Type = credential.Type,
            Id = credential.Id,
            ParentId = credential.ParentId,
            CustomAvailableCharacters = credential.CustomAvailableCharacters,
            IsAvailableLowerLatin = credential.IsAvailableLowerLatin,
            IsAvailableNumber = credential.IsAvailableNumber,
            IsAvailableSpecialSymbols = credential.IsAvailableSpecialSymbols,
            IsAvailableUpperLatin = credential.IsAvailableUpperLatin,
            Length = credential.Length,
            Regex = credential.Regex,
            OrderIndex = credential.OrderIndex,
        };
    }
}
