using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

namespace MPLR.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(static arg => string.Equals(arg, "--verify-embedded-setup", StringComparison.OrdinalIgnoreCase)))
        {
            return EmbeddedSetup.HasSetupResource ? 0 : 2;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm());
        return 0;
    }
}

internal sealed class InstallerForm : Form
{
    private readonly TextBox installPathBox;
    private readonly Button browseButton;
    private readonly Button installButton;
    private readonly CheckBox agreementCheckBox;
    private readonly Label statusLabel;

    public InstallerForm()
    {
        Text = "MPLR Installer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 600);
        Size = new Size(820, 640);
        Font = new Font("Microsoft YaHei UI", 9F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            RowCount = 6,
            ColumnCount = 1,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label titleLabel = new()
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
            Text = "安装 MPLR",
            Margin = new Padding(0, 0, 0, 8),
        };

        TextBox disclaimerBox = new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = InstallerText.Load("InstallerDisclaimer.md"),
        };

        agreementCheckBox = new CheckBox()
        {
            AutoSize = true,
            Text = "我已阅读并同意免责声明，确认自行遵守平台规则和当地法律法规。",
            Margin = new Padding(0, 12, 0, 8),
        };
        agreementCheckBox.CheckedChanged += (_, _) => UpdateInstallButton();

        TableLayoutPanel pathPanel = new()
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8),
        };
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        Label pathLabel = new()
        {
            AutoSize = true,
            Text = "安装位置",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 6, 12, 0),
        };
        installPathBox = new TextBox()
        {
            Dock = DockStyle.Fill,
            Text = GetDefaultInstallPath(),
            Margin = new Padding(0, 0, 8, 0),
        };
        installPathBox.TextChanged += (_, _) => UpdateInstallButton();
        browseButton = new Button()
        {
            AutoSize = true,
            Text = "浏览...",
        };
        browseButton.Click += BrowseButtonClick;

        Label hintLabel = new()
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Text = "如果已经安装过 MPLR，请选择原来的安装目录；软件内更新会继续更新这个目录。",
            Margin = new Padding(0, 6, 0, 0),
        };

        pathPanel.Controls.Add(pathLabel, 0, 0);
        pathPanel.Controls.Add(installPathBox, 1, 0);
        pathPanel.Controls.Add(browseButton, 2, 0);
        pathPanel.Controls.Add(hintLabel, 1, 1);
        pathPanel.SetColumnSpan(hintLabel, 2);

        TextBox notesBox = new()
        {
            Dock = DockStyle.Top,
            Height = 86,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = InstallerText.Load("InstallerNotes.md"),
            Margin = new Padding(0, 0, 0, 12),
        };

        TableLayoutPanel actionPanel = new()
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
        };
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        statusLabel = new Label()
        {
            AutoSize = true,
            Text = "",
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 8, 12, 0),
        };
        installButton = new Button()
        {
            AutoSize = true,
            Text = "开始安装",
            Padding = new Padding(18, 6, 18, 6),
        };
        installButton.Click += InstallButtonClick;

        actionPanel.Controls.Add(statusLabel, 0, 0);
        actionPanel.Controls.Add(installButton, 1, 0);

        root.Controls.Add(titleLabel, 0, 0);
        root.Controls.Add(disclaimerBox, 0, 1);
        root.Controls.Add(agreementCheckBox, 0, 2);
        root.Controls.Add(pathPanel, 0, 3);
        root.Controls.Add(notesBox, 0, 4);
        root.Controls.Add(actionPanel, 0, 5);

        Controls.Add(root);
        UpdateInstallButton();
    }

    private void BrowseButtonClick(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "选择 MPLR 安装位置",
            SelectedPath = installPathBox.Text,
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            installPathBox.Text = dialog.SelectedPath;
        }
    }

    private async void InstallButtonClick(object? sender, EventArgs e)
    {
        string installPath = installPathBox.Text.Trim();

        if (!ValidateInstallPath(installPath))
        {
            return;
        }

        if (IsProtectedPath(installPath) && !IsAdministrator())
        {
            DialogResult result = MessageBox.Show(
                this,
                "你选择的位置可能需要管理员权限。建议选择 D:\\Apps\\MPLR 或用户目录。如果继续安装失败，请重新以管理员身份运行安装器，或选择其它目录。",
                "安装位置可能受限",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.OK)
            {
                return;
            }
        }

        SetBusy(true, "正在准备安装...");

        try
        {
            int exitCode = await Task.Run(() => RunSetup(installPath));
            if (exitCode == 0)
            {
                statusLabel.Text = "安装完成。";
                MessageBox.Show(this, "MPLR 已安装完成。", "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }

            statusLabel.Text = $"安装失败，退出代码：{exitCode}";
            MessageBox.Show(this, $"安装失败，退出代码：{exitCode}", "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception exception)
        {
            statusLabel.Text = "安装失败。";
            MessageBox.Show(this, exception.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false, statusLabel.Text);
        }
    }

    private bool ValidateInstallPath(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            MessageBox.Show(this, "请选择安装位置。", "安装位置为空", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (Path.GetPathRoot(installPath) == installPath)
        {
            MessageBox.Show(this, "不能直接安装到磁盘根目录，请选择一个独立文件夹。", "安装位置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (File.Exists(installPath))
        {
            MessageBox.Show(this, "安装位置不能是文件，请选择文件夹。", "安装位置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (Directory.Exists(installPath) && Directory.EnumerateFileSystemEntries(installPath).Any())
        {
            DialogResult result = MessageBox.Show(
                this,
                "目标目录不是空目录。如果这里是已有 MPLR 安装目录，可以继续；否则建议换一个独立目录。是否继续？",
                "确认安装位置",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            return result == DialogResult.OK;
        }

        return true;
    }

    private static int RunSetup(string installPath)
    {
        string setupPath = EmbeddedSetup.Extract();
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = $"--installto {QuoteArgument(installPath)}",
                WorkingDirectory = Path.GetDirectoryName(setupPath) ?? Path.GetTempPath(),
                UseShellExecute = true,
            },
        };

        process.Start();
        process.WaitForExit();
        return process.ExitCode;
    }

    private void SetBusy(bool busy, string status)
    {
        installButton.Enabled = !busy && CanInstall();
        browseButton.Enabled = !busy;
        installPathBox.Enabled = !busy;
        agreementCheckBox.Enabled = !busy;
        statusLabel.Text = status;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void UpdateInstallButton()
    {
        installButton.Enabled = CanInstall();
    }

    private bool CanInstall()
    {
        return agreementCheckBox.Checked && !string.IsNullOrWhiteSpace(installPathBox.Text);
    }

    private static string GetDefaultInstallPath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string defaultPath = Path.Combine(localAppData, "MPLR");

        if (File.Exists(Path.Combine(defaultPath, "MPLR.exe")) || Directory.Exists(defaultPath))
        {
            return defaultPath;
        }

        return defaultPath;
    }

    private static bool IsProtectedPath(string installPath)
    {
        string fullPath = Path.GetFullPath(installPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string[] protectedRoots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        ];

        return protectedRoots
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .Select(static root => Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Any(root => fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                         fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}

internal static class EmbeddedSetup
{
    private const string ResourceName = "MPLR.Setup.exe";

    public static bool HasSetupResource => Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains(ResourceName);

    public static string Extract()
    {
        using Stream? source = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (source == null)
        {
            throw new InvalidOperationException("安装器内部缺少 MPLR Setup 资源，请重新下载安装包。");
        }

        string directory = Path.Combine(Path.GetTempPath(), "MPLR", "installer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string setupPath = Path.Combine(directory, "MPLR-win-Setup.exe");

        using FileStream target = File.Create(setupPath);
        source.CopyTo(target);
        return setupPath;
    }
}

internal static class InstallerText
{
    public static string Load(string resourceName)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return string.Empty;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
