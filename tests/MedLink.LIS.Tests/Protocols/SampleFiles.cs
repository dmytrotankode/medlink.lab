using System.Text;
using MedLink.LIS.Core.Protocols;

namespace MedLink.LIS.Tests.Protocols;

/// <summary>Доступ до tests/protocol_samples із тестів (пошук каталогу вгору від bin/).</summary>
public static class SampleFiles
{
    public static string Dir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "tests", "protocol_samples");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("tests/protocol_samples не знайдено");
        }
    }

    public static string Path_(string name) => Path.Combine(Dir, name);

    /// <summary>Текст зразка з розгорнутими мітками (&lt;STX&gt; → 0x02 …).</summary>
    public static string Load(string name) => ProtocolText.LoadSample(File.ReadAllText(Path_(name), Encoding.UTF8));

    /// <summary>Байти зразка (UTF-8) для подачі у сесію.</summary>
    public static byte[] LoadBytes(string name) => Encoding.UTF8.GetBytes(Load(name));
}
