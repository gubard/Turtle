using Turtle.Contract.Models;

namespace Turtle.Contract.Helpers
{
    public static class Mapper
    {
        public static Credential ToCredential(this CredentialEntity entity)
        {
            return new()
            {
                Id = entity.Id,
                Name = entity.Name,
                CustomAvailableCharacters = entity.CustomAvailableCharacters,
                IsAvailableLowerLatin = entity.IsAvailableLowerLatin,
                IsAvailableNumber = entity.IsAvailableNumber,
                IsAvailableSpecialSymbols = entity.IsAvailableSpecialSymbols,
                IsAvailableUpperLatin = entity.IsAvailableUpperLatin,
                Key = entity.Key,
                Link = entity.Link,
                Length = entity.Length,
                Regex = entity.Regex,
                Type = entity.Type,
                Login = entity.Login,
                OrderIndex = entity.OrderIndex,
                ParentId = entity.ParentId,
                IsBookmark = entity.IsBookmark,
            };
        }

        public static CredentialEntity ToCredentialEntity(this Credential credential)
        {
            return new()
            {
                Name = credential.Name,
                Login = credential.Login,
                Key = credential.Key,
                Link = credential.Link,
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
                IsBookmark = credential.IsBookmark,
            };
        }

        public static EditCredentialEntity[] ToEditCredentialEntities(this EditCredential edit)
        {
            var entities = new EditCredentialEntity[edit.Ids.Length];

            for (var index = 0; index < edit.Ids.Length; index++)
            {
                var id = edit.Ids[index];

                entities[index] = new(id)
                {
                    CustomAvailableCharacters = edit.CustomAvailableCharacters,
                    IsEditCustomAvailableCharacters = edit.IsEditCustomAvailableCharacters,
                    IsAvailableLowerLatin = edit.IsAvailableLowerLatin,
                    IsEditIsAvailableLowerLatin = edit.IsEditIsAvailableLowerLatin,
                    IsAvailableNumber = edit.IsAvailableNumber,
                    IsEditIsAvailableNumber = edit.IsEditIsAvailableNumber,
                    IsAvailableSpecialSymbols = edit.IsAvailableSpecialSymbols,
                    IsEditIsAvailableSpecialSymbols = edit.IsEditIsAvailableSpecialSymbols,
                    IsAvailableUpperLatin = edit.IsAvailableUpperLatin,
                    IsEditIsAvailableUpperLatin = edit.IsEditIsAvailableUpperLatin,
                    Key = edit.Key,
                    IsEditKey = edit.IsEditKey,
                    Length = edit.Length,
                    IsEditLength = edit.IsEditLength,
                    Login = edit.Login,
                    IsEditLogin = edit.IsEditLogin,
                    Name = edit.Name,
                    IsEditName = edit.IsEditName,
                    Regex = edit.Regex,
                    IsEditRegex = edit.IsEditRegex,
                    Type = edit.Type,
                    IsEditType = edit.IsEditType,
                    ParentId = edit.ParentId,
                    IsEditParentId = edit.IsEditParentId,
                    IsBookmark = edit.IsBookmark,
                    IsEditIsBookmark = edit.IsEditIsBookmark,
                    IsEditLink = edit.IsEditLink,
                    Link = edit.Link,
                };
            }

            return entities;
        }
    }
}
