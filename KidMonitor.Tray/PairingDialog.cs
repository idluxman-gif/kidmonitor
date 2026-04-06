using System.Drawing.Drawing2D;
using QRCoder;

namespace KidMonitor.Tray;

internal sealed class PairingDialog : Form
{
    private readonly Label _statusLabel;
    private readonly Button _closeButton;
    private readonly PictureBox _qrCodeBox;

    public PairingDialog(TrayPairingSession session)
    {
        Text = "Pair with parent app";
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 590);

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Location = new Point(24, 20),
            Text = "Pair this PC with the parent app",
        };

        var descriptionLabel = new Label
        {
            AutoSize = false,
            Location = new Point(24, 56),
            Size = new Size(372, 48),
            Text = "In the parent app open Settings, tap Add device, then scan the QR code or enter the 6-digit code shown here.",
        };

        var codeTitleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Location = new Point(24, 118),
            Text = "Pairing code",
        };

        var codeLabel = new Label
        {
            AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            Location = new Point(24, 146),
            Size = new Size(372, 60),
            Text = session.PairingCode,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        _qrCodeBox = new PictureBox
        {
            Location = new Point(84, 226),
            Size = new Size(252, 252),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Image = BuildQrImage(session.QrPayload),
        };

        var expiresAtLabel = new Label
        {
            AutoSize = false,
            Location = new Point(24, 492),
            Size = new Size(372, 18),
            Text = $"Code expires at {session.ExpiresAt.LocalDateTime:t}.",
            TextAlign = ContentAlignment.MiddleCenter,
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(33, 37, 41),
            Location = new Point(24, 516),
            Size = new Size(372, 20),
            Text = "Waiting for the parent app to confirm this device...",
            TextAlign = ContentAlignment.MiddleCenter,
        };

        _closeButton = new Button
        {
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel,
            Location = new Point(296, 548),
            Size = new Size(100, 30),
            Text = "Cancel",
        };
        _closeButton.Click += (_, _) => Close();

        Controls.Add(titleLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(codeTitleLabel);
        Controls.Add(codeLabel);
        Controls.Add(_qrCodeBox);
        Controls.Add(expiresAtLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_closeButton);

        CancelButton = _closeButton;
    }

    public void ShowConfirmed(string deviceName)
    {
        UpdateState(
            $"Paired successfully. {deviceName} is now linked to the parent account.",
            Color.FromArgb(25, 135, 84),
            "Close");
    }

    public void ShowExpired(string message)
    {
        UpdateState(message, Color.FromArgb(220, 53, 69), "Close");
    }

    public void ShowError(string message)
    {
        UpdateState(message, Color.FromArgb(220, 53, 69), "Close");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _qrCodeBox.Image?.Dispose();
            _qrCodeBox.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateState(string message, Color color, string closeButtonText)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateState(message, color, closeButtonText));
            return;
        }

        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
        _closeButton.Text = closeButtonText;
    }

    private static Bitmap BuildQrImage(string payload)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCode(qrCodeData);
        using var source = qrCode.GetGraphic(10);
        var target = new Bitmap(252, 252);

        using var graphics = Graphics.FromImage(target);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.White);
        graphics.DrawImage(source, new Rectangle(0, 0, target.Width, target.Height));

        return target;
    }
}
