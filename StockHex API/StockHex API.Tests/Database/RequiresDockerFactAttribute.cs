using System.Diagnostics;

namespace StockHex_API.Tests.Database;

/// <summary>
/// <c>[Fact]</c> que se salta —no falla— cuando no hay un Docker con el que
/// levantar el contenedor.
///
/// Sin esto, <c>dotnet test</c> reventaría en la máquina de cualquiera que no
/// tenga Docker corriendo, y un test rojo por falta de herramienta es ruido que
/// enseña a ignorar los rojos. En CI Docker siempre está, así que ahí se ejecutan.
/// </summary>
public sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
            Skip = "Requiere Docker en ejecución. Los tests contra SQL Server real se omiten.";
    }
}

internal static class DockerAvailability
{
    // Se consulta una vez por proceso: lanzar 'docker info' por cada test costaría
    // más que varios de los tests.
    private static readonly Lazy<bool> Probe = new(Check, isThreadSafe: true);

    public static bool IsAvailable => Probe.Value;

    private static bool Check()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info --format {{.ServerVersion}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (process is null)
                return false;

            // 'docker info' cuelga si el demonio no responde; se acota la espera.
            if (!process.WaitForExit(milliseconds: 15_000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            // El ejecutable no está en el PATH: tampoco hay Docker.
            return false;
        }
    }
}
