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
    private static readonly Color WindowBackground = Color.FromArgb(246, 247, 249);
    private static readonly Color CardBorder = Color.FromArgb(221, 226, 233);
    private static readonly Color MutedText = Color.FromArgb(91, 99, 112);
    private static readonly Color Primary = Color.FromArgb(37, 99, 235);
    private static readonly Color PrimaryDisabled = Color.FromArgb(148, 163, 184);

    private readonly Panel pageHost;
    private readonly Label titleLabel;
    private readonly Label subtitleLabel;
    private readonly Label stepLabel;
    private readonly Label statusLabel;
    private readonly Button backButton;
    private readonly Button nextButton;
    private readonly Button installButton;
    private readonly Button cancelButton;

    private TextBox? installPathBox;
    private Button? browseButton;
    private CheckBox? agreementCheckBox;
    private string installPath = GetDefaultInstallPath();
    private bool agreementAccepted;
    private bool isBusy;
    private int currentStep;

    public InstallerForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "MPLR 安装器";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);
        Size = new Size(860, 660);
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = WindowBackground;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Panel header = CreateHeader(out titleLabel, out subtitleLabel, out stepLabel);
        pageHost = new Panel()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 22, 28, 18),
        };
        Panel footer = CreateFooter(out statusLabel, out backButton, out nextButton, out installButton, out cancelButton);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(pageHost, 0, 1);
        root.Controls.Add(footer, 0, 2);

        Controls.Add(root);
        ShowStep(0);
    }

    private Panel CreateHeader(out Label title, out Label subtitle, out Label step)
    {
        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 118,
            BackColor = Color.White,
            Padding = new Padding(28, 22, 28, 18),
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        PictureBox iconBox = new()
        {
            Size = new Size(44, 44),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Image = Icon?.ToBitmap(),
            Margin = new Padding(0, 0, 14, 0),
        };

        title = new Label()
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 20F, FontStyle.Bold),
            Text = "安装 MPLR",
            Margin = new Padding(0, 0, 0, 2),
        };

        subtitle = new Label()
        {
            AutoSize = true,
            ForeColor = MutedText,
            Text = "",
        };

        step = new Label()
        {
            AutoSize = true,
            ForeColor = MutedText,
            TextAlign = ContentAlignment.TopRight,
            Margin = new Padding(16, 6, 0, 0),
        };

        layout.Controls.Add(iconBox, 0, 0);
        layout.SetRowSpan(iconBox, 2);
        layout.Controls.Add(title, 1, 0);
        layout.Controls.Add(subtitle, 1, 1);
        layout.Controls.Add(step, 2, 0);

        header.Controls.Add(layout);
        header.Paint += (_, e) =>
        {
            using Pen pen = new(CardBorder);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        return header;
    }

    private Panel CreateFooter(out Label status, out Button back, out Button next, out Button install, out Button cancel)
    {
        Panel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 76,
            BackColor = Color.White,
            Padding = new Padding(28, 16, 28, 16),
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        status = new Label()
        {
            Dock = DockStyle.Fill,
            ForeColor = MutedText,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "",
        };

        cancel = CreateSecondaryButton("取消", 88);
        cancel.Click += (_, _) => Close();

        back = CreateSecondaryButton("上一步", 88);
        back.Click += (_, _) => ShowStep(0);

        next = CreatePrimaryButton("下一步", 104);
        next.Click += (_, _) => ShowStep(1);

        install = CreatePrimaryButton("开始安装", 112);
        install.Click += InstallButtonClick;

        layout.Controls.Add(status, 0, 0);
        layout.Controls.Add(cancel, 1, 0);
        layout.Controls.Add(back, 2, 0);
        layout.Controls.Add(next, 3, 0);
        layout.Controls.Add(install, 4, 0);

        footer.Controls.Add(layout);
        footer.Paint += (_, e) =>
        {
            using Pen pen = new(CardBorder);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        return footer;
    }

    private static Button CreateSecondaryButton(string text, int width)
    {
        return new Button()
        {
            Text = text,
            Width = width,
            Height = 34,
            Margin = new Padding(0, 0, 10, 0),
            FlatStyle = FlatStyle.System,
        };
    }

    private static Button CreatePrimaryButton(string text, int width)
    {
        Button button = new()
        {
            Text = text,
            Width = width,
            Height = 34,
            ForeColor = Color.White,
            BackColor = Primary,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void ShowStep(int step)
    {
        currentStep = step;
        pageHost.Controls.Clear();
        installPathBox = null;
        browseButton = null;
        agreementCheckBox = null;

        if (currentStep == 0)
        {
            titleLabel.Text = "阅读重要说明";
            subtitleLabel.Text = "请先确认平台规则、风控和自启动说明。";
            stepLabel.Text = "步骤 1 / 2";
            pageHost.Controls.Add(CreateNoticePage());
        }
        else
        {
            titleLabel.Text = "选择安装位置";
            subtitleLabel.Text = "选择 MPLR 安装目录，然后开始安装。";
            stepLabel.Text = "步骤 2 / 2";
            pageHost.Controls.Add(CreateInstallPage());
        }

        UpdateButtons();
    }

    private Control CreateNoticePage()
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        CardPanel card = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
        };

        TableLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        content.Controls.Add(CreateSectionTitle("请在继续前阅读"), 0, 0);
        content.Controls.Add(CreateNoticeBlock("平台规则与风控", "MPLR 是本地直播间监控、预览与录制工具，不绕过平台风控、鉴权、地区限制、限流、验证码、登录状态、直播间可访问性或平台接口变化。"), 0, 1);
        content.Controls.Add(CreateNoticeBlock("合规责任", "请自行遵守各平台规则、所在地法律法规，以及主播或内容权利人的相关权益。账号限制、访问失败、直播中断、登录失效或录制失败等平台侧结果不属于 MPLR 的责任范围。"), 0, 2);
        content.Controls.Add(CreateNoticeBlock("开机自启动", "安装器不会默认开启开机自启动。安装完成后，如需自启，请在 MPLR 托盘菜单中手动开启或关闭。"), 0, 3);
        content.Controls.Add(CreateNoticeBlock("安装目录与更新", "软件内自动更新会继续更新当前安装目录。手动把新版本安装到另一个目录会形成独立副本。"), 0, 4);
        content.Controls.Add(CreateNoticeBlock("建议", "如果你已经安装过 MPLR，下一步请选择原来的安装目录，避免出现多个副本。"), 0, 5);

        card.Controls.Add(content);

        agreementCheckBox = new CheckBox()
        {
            AutoSize = true,
            Checked = agreementAccepted,
            Text = "我已阅读并同意上述说明，确认自行承担平台规则与合规责任。",
            Margin = new Padding(4, 14, 0, 0),
        };
        agreementCheckBox.CheckedChanged += (_, _) =>
        {
            agreementAccepted = agreementCheckBox.Checked;
            UpdateButtons();
        };

        Label hint = new()
        {
            AutoSize = true,
            ForeColor = MutedText,
            Text = "勾选后才能进入安装位置选择。",
            Margin = new Padding(4, 8, 0, 0),
        };

        layout.Controls.Add(card, 0, 0);
        layout.Controls.Add(agreementCheckBox, 0, 1);
        layout.Controls.Add(hint, 0, 2);

        return layout;
    }

    private Control CreateInstallPage()
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CreatePathCard(), 0, 0);
        layout.Controls.Add(CreateInstallSummaryCard(), 0, 1);

        return layout;
    }

    private Control CreatePathCard()
    {
        CardPanel card = new()
        {
            Dock = DockStyle.Top,
            Height = 146,
            Padding = new Padding(20),
            Margin = new Padding(0, 0, 0, 14),
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label title = CreateSectionTitle("安装位置");
        Label label = new()
        {
            AutoSize = true,
            Text = "目录",
            Margin = new Padding(0, 11, 12, 0),
        };

        installPathBox = new TextBox()
        {
            Dock = DockStyle.Fill,
            Height = 30,
            Text = installPath,
            Margin = new Padding(0, 6, 10, 0),
        };
        installPathBox.TextChanged += (_, _) =>
        {
            installPath = installPathBox.Text;
            UpdateButtons();
        };

        browseButton = new Button()
        {
            Text = "浏览...",
            Width = 82,
            Height = 30,
            Margin = new Padding(0, 5, 0, 0),
            FlatStyle = FlatStyle.System,
        };
        browseButton.Click += BrowseButtonClick;

        Label hint = CreateMutedLabel("如果已经安装过 MPLR，请选择原安装目录；后续软件内更新会继续更新这个目录。");

        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 3);
        layout.Controls.Add(label, 0, 1);
        layout.Controls.Add(installPathBox, 1, 1);
        layout.Controls.Add(browseButton, 2, 1);
        layout.Controls.Add(hint, 1, 2);
        layout.SetColumnSpan(hint, 2);

        card.Controls.Add(layout);
        return card;
    }

    private Control CreateInstallSummaryCard()
    {
        CardPanel card = new()
        {
            Dock = DockStyle.Top,
            Height = 138,
            Padding = new Padding(20),
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };

        layout.Controls.Add(CreateSectionTitle("安装行为"), 0, 0);
        layout.Controls.Add(CreateMutedLabel("安装器会调用内置 Velopack Setup，并把你选择的目录传给它。"), 0, 1);
        layout.Controls.Add(CreateMutedLabel("安装器本身不会修改开机自启动设置。"), 0, 2);
        layout.Controls.Add(CreateMutedLabel("选择非空目录时会再次确认，防止误装到其它软件目录。"), 0, 3);

        card.Controls.Add(layout);
        return card;
    }

    private static Label CreateSectionTitle(string text)
    {
        return new Label()
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            Text = text,
            Margin = new Padding(0, 0, 0, 10),
        };
    }

    private static Control CreateNoticeBlock(string title, string body)
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 12),
        };

        Label titleLabel = new()
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Text = title,
            Margin = new Padding(0, 0, 0, 3),
        };

        Label bodyLabel = new()
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 44,
            ForeColor = Color.FromArgb(35, 42, 52),
            Text = body,
        };

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(bodyLabel, 0, 1);
        return layout;
    }

    private static Label CreateMutedLabel(string text)
    {
        return new Label()
        {
            AutoSize = true,
            ForeColor = MutedText,
            Text = text,
            Margin = new Padding(0, 8, 0, 0),
        };
    }

    private void BrowseButtonClick(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "选择 MPLR 安装目录",
            SelectedPath = installPath,
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            installPath = dialog.SelectedPath;
            if (installPathBox != null)
            {
                installPathBox.Text = installPath;
            }
        }
    }

    private async void InstallButtonClick(object? sender, EventArgs e)
    {
        string selectedInstallPath = installPath.Trim();

        if (!ValidateInstallPath(selectedInstallPath))
        {
            return;
        }

        if (IsProtectedPath(selectedInstallPath) && !IsAdministrator())
        {
            DialogResult result = MessageBox.Show(
                this,
                "你选择的位置可能需要管理员权限。建议选择 D:\\Apps\\MPLR 或用户目录。如果继续安装失败，请以管理员身份重新运行安装器，或选择其它目录。",
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
            int exitCode = await Task.Run(() => RunSetup(selectedInstallPath));
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
            MessageBox.Show(this, "请选择安装目录。", "安装目录为空", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (Path.GetPathRoot(installPath) == installPath)
        {
            MessageBox.Show(this, "不能直接安装到磁盘根目录，请选择一个独立文件夹。", "安装目录无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (File.Exists(installPath))
        {
            MessageBox.Show(this, "安装目录不能是文件，请选择文件夹。", "安装目录无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (Directory.Exists(installPath) && Directory.EnumerateFileSystemEntries(installPath).Any())
        {
            DialogResult result = MessageBox.Show(
                this,
                "目标目录不是空目录。如果这里是已有 MPLR 安装目录，可以继续；否则建议换一个独立目录。是否继续？",
                "确认安装目录",
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
        isBusy = busy;
        cancelButton.Enabled = !busy;
        backButton.Enabled = !busy && currentStep == 1;
        nextButton.Enabled = !busy && currentStep == 0 && agreementAccepted;
        installButton.Enabled = !busy && CanInstall();
        if (installPathBox != null)
        {
            installPathBox.Enabled = !busy;
        }
        if (browseButton != null)
        {
            browseButton.Enabled = !busy;
        }
        if (agreementCheckBox != null)
        {
            agreementCheckBox.Enabled = !busy;
        }
        statusLabel.Text = status;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        UpdatePrimaryButtonColors();
    }

    private void UpdateButtons()
    {
        backButton.Visible = currentStep == 1;
        nextButton.Visible = currentStep == 0;
        installButton.Visible = currentStep == 1;
        backButton.Enabled = !isBusy && currentStep == 1;
        nextButton.Enabled = !isBusy && currentStep == 0 && agreementAccepted;
        installButton.Enabled = !isBusy && CanInstall();
        statusLabel.Text = currentStep == 0 && !agreementAccepted ? "请先勾选同意说明，然后进入下一步。" : "";
        UpdatePrimaryButtonColors();
    }

    private void UpdatePrimaryButtonColors()
    {
        nextButton.BackColor = nextButton.Enabled ? Primary : PrimaryDisabled;
        installButton.BackColor = installButton.Enabled ? Primary : PrimaryDisabled;
    }

    private bool CanInstall()
    {
        return currentStep == 1 && !string.IsNullOrWhiteSpace(installPath);
    }

    private static string GetDefaultInstallPath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "MPLR");
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

internal sealed class CardPanel : Panel
{
    public CardPanel()
    {
        BackColor = Color.White;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using Pen pen = new(Color.FromArgb(221, 226, 233));
        Rectangle rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;
        e.Graphics.DrawRectangle(pen, rect);
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
