using System.Security.Cryptography;

namespace PainelSenhas.Web.Common;

/// <summary>
/// Gera senhas aleatórias que atendem à política do Identity (16 caracteres com maiúscula, minúscula, número e símbolo).
/// Os caracteres ambíguos (0/O, 1/l/I) ficam de fora para a senha poder ser anotada e digitada sem erro.
/// </summary>
internal static class PasswordGenerator
{
    private const int Length = 16;
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%*?-_";

    public static string Generate()
    {
        string[] groups = [Upper, Lower, Digits, Symbols];
        var all = string.Concat(groups);

        var chars = groups.Select(Pick).ToList();
        while (chars.Count < Length)
        {
            chars.Add(Pick(all));
        }

        // Embaralha para que os quatro caracteres obrigatórios não fiquem sempre no início
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
