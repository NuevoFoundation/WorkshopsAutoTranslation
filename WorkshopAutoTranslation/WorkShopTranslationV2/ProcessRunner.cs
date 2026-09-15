using System.Diagnostics;
using System.Text;

namespace WorkShopTranslationV2;

internal sealed class ProcessRunner
{
    public CommandResult Run(string fileName, IEnumerable<string> arguments, string workingDirectory, bool throwOnError = true)
    {
        var argumentList = arguments.ToList();
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        var result = new CommandResult(
            fileName,
            argumentList,
            workingDirectory,
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult(),
            standardError.GetAwaiter().GetResult());

        if (throwOnError && result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.ToExceptionMessage());
        }

        return result;
    }
}

internal sealed class CommandResult(
    string fileName,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    int exitCode,
    string standardOutput,
    string standardError)
{
    public string FileName { get; } = fileName;
    public IReadOnlyList<string> Arguments { get; } = arguments;
    public string WorkingDirectory { get; } = workingDirectory;
    public int ExitCode { get; } = exitCode;
    public string StandardOutput { get; } = standardOutput;
    public string StandardError { get; } = standardError;

    public string ToExceptionMessage()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Command failed with exit code {ExitCode}: {FileName} {string.Join(" ", Arguments)}");
        builder.AppendLine($"Working directory: {WorkingDirectory}");

        if (!string.IsNullOrWhiteSpace(StandardOutput))
        {
            builder.AppendLine("StdOut:");
            builder.AppendLine(StandardOutput.Trim());
        }

        if (!string.IsNullOrWhiteSpace(StandardError))
        {
            builder.AppendLine("StdErr:");
            builder.AppendLine(StandardError.Trim());
        }

        return builder.ToString().TrimEnd();
    }
}
