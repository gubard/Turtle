using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Gaia.Models;

namespace Turtle.Contract.Models;

[JsonSerializable(typeof(TurtleGetRequest))]
[JsonSerializable(typeof(TurtlePostRequest))]
[JsonSerializable(typeof(TurtleGetResponse))]
[JsonSerializable(typeof(TurtlePostResponse))]
[JsonSerializable(typeof(ChangeOrder))]
[JsonSerializable(typeof(CreateCredential))]
[JsonSerializable(typeof(Credential))]
[JsonSerializable(typeof(CredentialType))]
[JsonSerializable(typeof(EditCredential))]
[JsonSerializable(typeof(AlreadyExistsValidationError))]
[JsonSerializable(typeof(NotFoundValidationError))]
public partial class TurtleJsonContext : JsonSerializerContext
{
    public static readonly IJsonTypeInfoResolver Resolver;

    static TurtleJsonContext()
    {
        Resolver = Default.WithAddedModifier(typeInfo =>
        {
            if (typeInfo.Type == typeof(ValidationError))
            {
                typeInfo.PolymorphismOptions = new()
                {
                    TypeDiscriminatorPropertyName = "$type",
                    DerivedTypes =
                    {
                        new(
                            typeof(AlreadyExistsValidationError),
                            typeof(AlreadyExistsValidationError).FullName!
                        ),
                        new(
                            typeof(NotFoundValidationError),
                            typeof(NotFoundValidationError).FullName!
                        ),
                    },
                };
            }
        });
    }
}
