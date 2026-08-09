using System.Text;

namespace SolarSim.CatalogImporter;

/// <summary>
/// Leitor de CSV com aspas, o bastante para o que as tabelas do JPL produzem.
/// </summary>
/// <remarks>
/// Um <c>Split(',')</c> resolveria os números e estragaria a coluna de nota, que é onde
/// mora a procedência de cada linha e onde vírgula é natural. Linhas em branco são
/// ignoradas, e o que vier depois de <c>#</c> no início da linha é comentário.
/// </remarks>
public static class Csv
{
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var rows = new List<IReadOnlyList<string>>();

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.Trim().Length == 0 || trimmed.TrimStart().StartsWith('#'))
            {
                continue;
            }

            rows.Add(Fields(trimmed));
        }

        return rows;
    }

    private static List<string> Fields(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (quoted)
            {
                if (character != '"')
                {
                    field.Append(character);
                }
                else if (index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    quoted = true;
                    break;

                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;

                default:
                    field.Append(character);
                    break;
            }
        }

        fields.Add(field.ToString());

        return fields;
    }
}
