using System.Diagnostics;
using System.IO;

namespace BimDown.Installer;

sealed class InstallerForm : Form
{
    readonly Label _statusLabel;
    readonly Button _installButton;
    readonly Button _uninstallButton;
    readonly LinkLabel _openFolderLink;

    public InstallerForm()
    {
        Text = L.Title;
        Size = new Size(480, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9);

        var titleLabel = new Label
        {
            Text = L.Heading,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Location = new Point(30, 20),
            AutoSize = true,
        };

        var descLabel = new Label
        {
            Text = L.Description,
            Font = new Font("Segoe UI", 10),
            Location = new Point(30, 60),
            Size = new Size(400, 80),
        };

        _installButton = new Button
        {
            Text = L.Install,
            Font = new Font("Segoe UI", 11),
            Size = new Size(180, 45),
            Location = new Point(30, 155),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        _installButton.FlatAppearance.BorderSize = 0;
        _installButton.Click += OnInstallClick;

        _uninstallButton = new Button
        {
            Text = L.Uninstall,
            Font = new Font("Segoe UI", 11),
            Size = new Size(180, 45),
            Location = new Point(250, 155),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        _uninstallButton.Click += OnUninstallClick;

        _statusLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9),
            Location = new Point(30, 215),
            Size = new Size(400, 40),
            ForeColor = Color.Gray,
        };

        _openFolderLink = new LinkLabel
        {
            Text = L.OpenInstallLocation,
            Font = new Font("Segoe UI", 9),
            Location = new Point(30, 260),
            AutoSize = true,
            Visible = false,
        };
        _openFolderLink.LinkClicked += (_, _) =>
        {
            var target = BundleInstaller.TargetDir;
            if (Directory.Exists(target))
                Process.Start("explorer.exe", target);
            else
                Process.Start("explorer.exe", BundleInstaller.AppPluginsDir);
        };

        Controls.AddRange([titleLabel, descLabel, _installButton, _uninstallButton, _statusLabel, _openFolderLink]);
        RefreshStatus();
    }

    void RefreshStatus()
    {
        if (BundleInstaller.IsInstalled)
        {
            _statusLabel.Text = L.StatusInstalled;
            _statusLabel.ForeColor = Color.Green;
            _installButton.Text = L.Reinstall;
            _openFolderLink.Visible = true;
        }
        else
        {
            _statusLabel.Text = L.StatusNotInstalled;
            _statusLabel.ForeColor = Color.Gray;
            _installButton.Text = L.Install;
            _openFolderLink.Visible = false;
        }
    }

    void OnInstallClick(object? sender, EventArgs e)
    {
        var result = BundleInstaller.Install();
        if (result == 0)
        {
            _statusLabel.Text = L.StatusInstalledOk;
            _statusLabel.ForeColor = Color.Green;
        }
        else
        {
            MessageBox.Show(L.ErrorInstallFailed, L.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        RefreshStatus();
    }

    void OnUninstallClick(object? sender, EventArgs e)
    {
        if (!BundleInstaller.IsInstalled)
        {
            _statusLabel.Text = L.NothingToUninstall;
            _statusLabel.ForeColor = Color.Gray;
            return;
        }

        var confirm = MessageBox.Show(L.ConfirmUninstall, L.ConfirmTitle,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        var result = BundleInstaller.Uninstall();
        if (result == 0)
        {
            _statusLabel.Text = L.StatusUninstalled;
            _statusLabel.ForeColor = Color.Gray;
        }
        else
        {
            MessageBox.Show(L.ErrorUninstallFailed, L.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        RefreshStatus();
    }
}
