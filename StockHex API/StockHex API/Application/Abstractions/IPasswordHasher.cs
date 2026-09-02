namespace StockHex_API.Application.Abstractions;

/// <summary>Abstrae el algoritmo de hashing para poder sustituirlo y para poder testear sin coste de CPU.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
