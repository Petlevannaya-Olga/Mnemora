using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;
using Mnemora.Desktop.Settings;

namespace Mnemora.Desktop.Editors;

public sealed class MarkdownEditorService(
    ISettingsService settingsService) : IMarkdownEditorService
{
    private const string EditorCheckFileName =
        "mnemora-editor-check.md";

    private const string VisualStudioCodeDownloadUrl =
        "https://code.visualstudio.com/download";

    private const string ObsidianDownloadUrl =
        "https://obsidian.md/download";

    public string? FindVisualStudioCodeExecutable()
    {
        var candidates = new List<string?>
        {
            GetRegisteredApplicationPath(
                Registry.CurrentUser,
                "Code.exe"),
            GetRegisteredApplicationPath(
                Registry.LocalMachine,
                "Code.exe"),
        };

        string localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        string programFiles =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);

        string programFilesX86 =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);

        candidates.AddRange(
        [
            Path.Combine(
                localAppData,
                "Programs",
                "Microsoft VS Code",
                "Code.exe"),
            Path.Combine(
                programFiles,
                "Microsoft VS Code",
                "Code.exe"),
            Path.Combine(
                programFilesX86,
                "Microsoft VS Code",
                "Code.exe"),
        ]);

        string? environmentPath =
            Environment.GetEnvironmentVariable("PATH");

        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            foreach (string directory in environmentPath.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries))
            {
                candidates.Add(
                    Path.Combine(
                        directory,
                        "Code.exe"));

                candidates.Add(
                    Path.Combine(
                        directory,
                        "..",
                        "Code.exe"));
            }
        }

        foreach (string? candidate in candidates)
        {
            if (IsValidVisualStudioCodePath(candidate))
            {
                return Path.GetFullPath(candidate!);
            }
        }

        return null;
    }

    public bool IsObsidianInstalled()
    {
        return FindObsidianExecutable() is not null;
    }

    public MarkdownEditorLaunchResult OpenDownloadPage(
        MarkdownEditorType editor)
    {
        string url = editor switch
        {
            MarkdownEditorType.VisualStudioCode =>
                VisualStudioCodeDownloadUrl,
            MarkdownEditorType.Obsidian =>
                ObsidianDownloadUrl,
            _ => throw new ArgumentOutOfRangeException(
                nameof(editor),
                editor,
                null),
        };

        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });

            return new MarkdownEditorLaunchResult(
                true,
                "Страница установки открыта в браузере.");
        }
        catch (Exception exception)
            when (exception is Win32Exception
                      or InvalidOperationException
                      or NotSupportedException)
        {
            return new MarkdownEditorLaunchResult(
                false,
                $"Не удалось открыть страницу установки: {exception.Message}");
        }
    }

    public async Task<MarkdownEditorLaunchResult> CheckAsync(
        MarkdownEditorType editor,
        string? visualStudioCodePath,
        string? obsidianVaultPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (editor == MarkdownEditorType.VisualStudioCode &&
            !IsValidVisualStudioCodePath(visualStudioCodePath))
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Visual Studio Code не найден. Укажите путь к Code.exe.");
        }

        if (editor == MarkdownEditorType.Obsidian &&
            !IsObsidianInstalled())
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Obsidian не найден. Установите приложение и повторите поиск.");
        }

        try
        {
            string testFilePath = editor switch
            {
                MarkdownEditorType.VisualStudioCode =>
                    await CreateVisualStudioCodeCheckFileAsync(
                        cancellationToken),

                MarkdownEditorType.Obsidian =>
                    await CreateObsidianCheckFileAsync(
                        obsidianVaultPath,
                        cancellationToken),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(editor),
                    editor,
                    null),
            };

            return OpenCore(
                editor,
                testFilePath,
                visualStudioCodePath,
                obsidianVaultPath);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or NotSupportedException
                      or ArgumentException
                      or Win32Exception)
        {
            return new MarkdownEditorLaunchResult(
                false,
                $"Не удалось запустить проверку редактора: {exception.Message}");
        }
    }

    public async Task<MarkdownEditorLaunchResult> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Не указан Markdown-файл для открытия.");
        }

        AppSettings settings =
            await settingsService.LoadAsync(
                cancellationToken);

        if (settings.MarkdownEditor is not { } editor)
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Редактор Markdown не настроен.");
        }

        try
        {
            return OpenCore(
                editor,
                filePath,
                settings.VisualStudioCodePath,
                settings.ObsidianVaultPath);
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or NotSupportedException
                      or ArgumentException
                      or Win32Exception)
        {
            return new MarkdownEditorLaunchResult(
                false,
                $"Не удалось открыть Markdown-файл: {exception.Message}");
        }
    }

    private MarkdownEditorLaunchResult OpenCore(
        MarkdownEditorType editor,
        string filePath,
        string? visualStudioCodePath,
        string? obsidianVaultPath)
    {
        string fullFilePath =
            Path.GetFullPath(filePath);

        if (!File.Exists(fullFilePath))
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Markdown-файл не найден.");
        }

        return editor switch
        {
            MarkdownEditorType.VisualStudioCode =>
                OpenInVisualStudioCode(
                    fullFilePath,
                    visualStudioCodePath),

            MarkdownEditorType.Obsidian =>
                OpenInObsidian(
                    fullFilePath,
                    obsidianVaultPath),

            _ => new MarkdownEditorLaunchResult(
                false,
                "Выбран неподдерживаемый Markdown-редактор."),
        };
    }

    private static MarkdownEditorLaunchResult OpenInVisualStudioCode(
        string filePath,
        string? visualStudioCodePath)
    {
        if (!IsValidVisualStudioCodePath(
                visualStudioCodePath))
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Visual Studio Code не найден. Проверьте путь к Code.exe.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(
                visualStudioCodePath!),
            UseShellExecute = true,
        };

        startInfo.ArgumentList.Add(
            "--reuse-window");
        startInfo.ArgumentList.Add(
            filePath);

        Process? process = Process.Start(startInfo);

        if (process is null)
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Не удалось запустить Visual Studio Code.");
        }

        return new MarkdownEditorLaunchResult(
            true,
            "Тестовый Markdown-файл передан в Visual Studio Code.");
    }

    private MarkdownEditorLaunchResult OpenInObsidian(
        string filePath,
        string? obsidianVaultPath)
    {
        if (!IsObsidianInstalled())
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Obsidian не найден. Установите приложение и повторите поиск.");
        }

        if (!IsValidObsidianVault(
                obsidianVaultPath))
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Vault Obsidian не найден. Проверьте выбранную папку.");
        }

        if (!IsObsidianUriRegistered())
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Obsidian установлен, но протокол obsidian:// не зарегистрирован. Запустите Obsidian один раз и повторите проверку.");
        }

        string vaultPath =
            Path.GetFullPath(obsidianVaultPath!);

        if (!IsPathInsideDirectory(
                filePath,
                vaultPath))
        {
            return new MarkdownEditorLaunchResult(
                false,
                "Файл находится вне выбранного Vault Obsidian.");
        }

        string uri =
            $"obsidian://open?path={Uri.EscapeDataString(filePath)}";

        Process.Start(new ProcessStartInfo(uri)
        {
            UseShellExecute = true,
        });

        return new MarkdownEditorLaunchResult(
            true,
            "Тестовый Markdown-файл передан в Obsidian.");
    }

    private static async Task<string> CreateVisualStudioCodeCheckFileAsync(
        CancellationToken cancellationToken)
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Mnemora",
            "Temp");

        Directory.CreateDirectory(directory);

        string filePath = Path.Combine(
            directory,
            EditorCheckFileName);

        await WriteCheckFileAsync(
            filePath,
            cancellationToken);

        return filePath;
    }

    private static async Task<string> CreateObsidianCheckFileAsync(
        string? obsidianVaultPath,
        CancellationToken cancellationToken)
    {
        if (!IsValidObsidianVault(
                obsidianVaultPath))
        {
            throw new DirectoryNotFoundException(
                "Выбранная папка не является Vault Obsidian.");
        }

        string directory = Path.Combine(
            Path.GetFullPath(obsidianVaultPath!),
            ".mnemora-temp");

        Directory.CreateDirectory(directory);

        string filePath = Path.Combine(
            directory,
            EditorCheckFileName);

        await WriteCheckFileAsync(
            filePath,
            cancellationToken);

        return filePath;
    }

    private static Task WriteCheckFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        const string content =
            "# Проверка редактора Mnemora\n\n" +
            "Если вы видите этот файл, Mnemora смогла передать его выбранному Markdown-редактору.\n";

        return File.WriteAllTextAsync(
            filePath,
            content,
            cancellationToken);
    }

    private static string? FindObsidianExecutable()
    {
        string localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        string programFiles =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);

        string programFilesX86 =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);

        string? registeredExecutable =
            GetObsidianUriExecutable();

        string?[] candidates =
        [
            registeredExecutable,
            Path.Combine(
                localAppData,
                "Programs",
                "Obsidian",
                "Obsidian.exe"),
            Path.Combine(
                localAppData,
                "Obsidian",
                "Obsidian.exe"),
            Path.Combine(
                programFiles,
                "Obsidian",
                "Obsidian.exe"),
            Path.Combine(
                programFilesX86,
                "Obsidian",
                "Obsidian.exe"),
        ];

        return candidates.FirstOrDefault(
            candidate =>
                !string.IsNullOrWhiteSpace(candidate) &&
                File.Exists(candidate));
    }

    private static bool IsObsidianUriRegistered()
    {
        string? executable =
            GetObsidianUriExecutable();

        return !string.IsNullOrWhiteSpace(executable) &&
               File.Exists(executable);
    }

    private static string? GetObsidianUriExecutable()
    {
        try
        {
            using RegistryKey? key =
                Registry.ClassesRoot.OpenSubKey(
                    @"obsidian\shell\open\command");

            string? command =
                key?.GetValue(null) as string;

            return ExtractExecutablePath(command);
        }
        catch (Exception exception)
            when (exception is SecurityException
                      or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? GetRegisteredApplicationPath(
        RegistryKey root,
        string executableName)
    {
        try
        {
            using RegistryKey? key =
                root.OpenSubKey(
                    $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executableName}");

            string? path =
                key?.GetValue(null) as string;

            return string.IsNullOrWhiteSpace(path)
                ? null
                : path.Trim().Trim('"');
        }
        catch (Exception exception)
            when (exception is SecurityException
                      or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ExtractExecutablePath(
        string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        string trimmed = command.Trim();

        if (trimmed.Length > 1 && trimmed[0] == '"')
        {
            int closingQuote =
                trimmed.IndexOf('"', 1);

            if (closingQuote > 1)
            {
                return trimmed[1..closingQuote];
            }
        }

        int executableEnd =
            trimmed.IndexOf(
                ".exe",
                StringComparison.OrdinalIgnoreCase);

        if (executableEnd < 0)
        {
            return null;
        }

        return trimmed[..(executableEnd + 4)]
            .Trim()
            .Trim('"');
    }

    private static bool IsValidVisualStudioCodePath(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return File.Exists(path) &&
                   string.Equals(
                       Path.GetFileName(path),
                       "Code.exe",
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
            when (exception is ArgumentException
                      or NotSupportedException
                      or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsValidObsidianVault(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            string fullPath =
                Path.GetFullPath(path);

            return Directory.Exists(fullPath) &&
                   Directory.Exists(
                       Path.Combine(
                           fullPath,
                           ".obsidian"));
        }
        catch (Exception exception)
            when (exception is ArgumentException
                      or NotSupportedException
                      or PathTooLongException
                      or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsPathInsideDirectory(
        string filePath,
        string directoryPath)
    {
        string normalizedDirectory =
            Path.GetFullPath(directoryPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        string normalizedFile =
            Path.GetFullPath(filePath);

        return normalizedFile.StartsWith(
            normalizedDirectory,
            StringComparison.OrdinalIgnoreCase);
    }
}
