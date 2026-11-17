using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Forms;

public sealed class LoginForm : Form
{
    private readonly TextBox emailTextBox = new();
    private readonly TextBox passwordTextBox = new();
    private readonly CheckBox showPasswordCheckBox = new();
    private readonly Button loginButton = new();
    private readonly Button exitButton = new();
    private readonly Label statusLabel = new();
    private readonly System.Windows.Forms.Timer lockoutTimer = new() { Interval = 1000 };
    private int failedAttempts;
    private int lockoutSecondsRemaining;

    public LoginForm()
    {
        InitializeForm();
        Shown += (_, _) => emailTextBox.Focus();
        loginButton.Click += LoginButton_Click;
        exitButton.Click += (_, _) => Close();
        showPasswordCheckBox.CheckedChanged += (_, _) =>
            passwordTextBox.UseSystemPasswordChar = !showPasswordCheckBox.Checked;
        lockoutTimer.Tick += LockoutTimer_Tick;
    }

    private void InitializeForm()
    {
        Text = "FleetLink - Login";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(420, 330);
        ClientSize = new Size(420, 330);
        Font = UiStyles.DefaultFont;
        BackColor = UiStyles.ContentBackground;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var titleLabel = new Label
        {
            Text = "FleetLink",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold | FontStyle.Italic),
            ForeColor = UiStyles.Sidebar,
            Location = new Point(42, 30)
        };
        var subtitleLabel = new Label
        {
            Text = "Fleet and Transportation Management",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(44, 67)
        };

        ConfigureLabel(emailTextBox, "Email", 44, 108);
        ConfigureLabel(passwordTextBox, "Password", 44, 164);
        emailTextBox.Width = 325;
        passwordTextBox.Width = 325;
        passwordTextBox.UseSystemPasswordChar = true;
        emailTextBox.KeyDown += LoginOnEnter;
        passwordTextBox.KeyDown += LoginOnEnter;

        showPasswordCheckBox.Text = "Show password";
        showPasswordCheckBox.AutoSize = true;
        showPasswordCheckBox.Location = new Point(44, 218);

        loginButton.Text = "Login";
        loginButton.Width = 145;
        loginButton.Height = 36;
        loginButton.Location = new Point(44, 256);
        loginButton.BackColor = UiStyles.SidebarActive;
        loginButton.ForeColor = Color.White;
        loginButton.FlatStyle = FlatStyle.Flat;
        loginButton.FlatAppearance.BorderSize = 0;

        exitButton.Text = "Exit";
        exitButton.Width = 145;
        exitButton.Height = 36;
        exitButton.Location = new Point(224, 256);
        exitButton.FlatStyle = FlatStyle.Flat;

        statusLabel.AutoSize = true;
        statusLabel.ForeColor = Color.Firebrick;
        statusLabel.Location = new Point(44, 232);

        Controls.AddRange([
            titleLabel, subtitleLabel, emailTextBox, passwordTextBox,
            showPasswordCheckBox, statusLabel, loginButton, exitButton
        ]);
        AcceptButton = loginButton;
        CancelButton = exitButton;
    }

    private static void ConfigureLabel(Control control, string labelText, int x, int y)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Location = new Point(x, y)
        };
        control.ParentChanged += (_, _) =>
        {
            if (control.Parent is not null && !control.Parent.Controls.Contains(label))
            {
                control.Parent.Controls.Add(label);
                label.BringToFront();
            }
        };
        control.Location = new Point(x, y + 22);
    }

    private async void LoginOnEnter(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            await AuthenticateAsync();
        }
    }

    private async void LoginButton_Click(object? sender, EventArgs e)
    {
        await AuthenticateAsync();
    }

    private async Task AuthenticateAsync()
    {
        if (!loginButton.Enabled)
        {
            return;
        }

        var email = emailTextBox.Text.Trim().ToLowerInvariant();
        var password = passwordTextBox.Text;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowStatus("Enter your email and password.");
            return;
        }

        SetBusy(true);
        try
        {
            await using var db = DbContextFactory.Create();
            var user = await db.UserAccounts
                .Include(account => account.Branch)
                .SingleOrDefaultAsync(account => account.Email.ToLower() == email);

            if (user is null || !PasswordHasher.Verify(password, user.PasswordHash))
            {
                RegisterFailedAttempt();
                ShowStatus("Invalid email or password");
                return;
            }

            if (!user.IsActive)
            {
                ShowStatus("This account is deactivated");
                return;
            }

            failedAttempts = 0;
            Session.Login(user, password);
            Hide();
            if (Session.MustChangePassword)
            {
                MessageBox.Show(
                    "Please change your password",
                    "Password Change Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            using var mainForm = new MainForm();
            mainForm.ShowDialog(this);
            if (!IsDisposed)
            {
                Show();
                emailTextBox.Focus();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Login failed. Please try again.\n" + ex.Message,
                "Login Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed && lockoutSecondsRemaining == 0)
            {
                SetBusy(false);
            }
        }
    }

    private void RegisterFailedAttempt()
    {
        failedAttempts++;
        if (failedAttempts >= 5)
        {
            lockoutSecondsRemaining = 30;
            loginButton.Enabled = false;
            lockoutTimer.Start();
            ShowStatus("Too many failed attempts. Try again in 30 seconds.");
        }
    }

    private void LockoutTimer_Tick(object? sender, EventArgs e)
    {
        lockoutSecondsRemaining--;
        if (lockoutSecondsRemaining <= 0)
        {
            lockoutTimer.Stop();
            failedAttempts = 0;
            SetBusy(false);
            statusLabel.Text = string.Empty;
        }
        else
        {
            statusLabel.Text = $"Too many failed attempts. Try again in {lockoutSecondsRemaining} seconds.";
        }
    }

    private void SetBusy(bool busy)
    {
        loginButton.Enabled = !busy && lockoutSecondsRemaining == 0;
        emailTextBox.Enabled = !busy;
        passwordTextBox.Enabled = !busy;
        showPasswordCheckBox.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void ShowStatus(string message)
    {
        statusLabel.Text = message;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lockoutTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
