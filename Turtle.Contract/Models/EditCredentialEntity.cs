using Gaia.Models;
using Gaia.Services;

namespace Turtle.Contract.Models;

public sealed partial class EditCredentialEntity
    : IStaticFactory<Guid, EditCredentialEntity>,
        IId<Guid>
{
    public static EditCredentialEntity Create(Guid input)
    {
        return new(input);
    }
}
