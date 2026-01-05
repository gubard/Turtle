using System.Collections.Frozen;

namespace Turtle.Contract.Helpers;

public static class TurtleMigration
{
    public static readonly FrozenDictionary<int, string> Migrations;

    static TurtleMigration()
    {
        Migrations = new Dictionary<int, string>
        {
            {
                6,
                @"
CREATE TABLE IF NOT EXISTS Credentials (
    Id TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL CHECK(length(Name) <= 255),
    Login TEXT NOT NULL CHECK(length(Login) <= 255),
    Key TEXT NOT NULL CHECK(length(Key) <= 255),
    IsAvailableUpperLatin INTEGER NOT NULL CHECK (IsAvailableUpperLatin IN (0, 1)),
    IsAvailableLowerLatin INTEGER NOT NULL CHECK (IsAvailableLowerLatin IN (0, 1)),
    IsAvailableNumber INTEGER NOT NULL CHECK (IsAvailableNumber IN (0, 1)),
    IsAvailableSpecialSymbols INTEGER NOT NULL CHECK (IsAvailableSpecialSymbols IN (0, 1)),
    CustomAvailableCharacters TEXT NOT NULL,
    Length INTEGER NOT NULL,
    Regex TEXT NOT NULL CHECK(length(Regex) <= 255),
    Type INTEGER NOT NULL,
    OrderIndex INTEGER NOT NULL,
    ParentId TEXT,
    -- Optional: Foreign key constraint for self-referencing ParentId
    FOREIGN KEY (ParentId) REFERENCES CredentialEntities (Id)
);
"
            },
        }.ToFrozenDictionary();
    }
}
