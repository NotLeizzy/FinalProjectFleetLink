using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class PasswordResetForm : Form
{
    private readonly UserService userService;
    private readonly int userId;
    private readonly TextBox passwordTextBox = new();
    private readonly TextBox confirmPasswordTextBox = new();
    private readonly Button resetButton = new();

    public PasswordResetForm(UserService userService, int userId)
    {
        this.userService = userService;
        this.userId = userId;
        InitializeForm();
        resetButton.Click += ResetButton_Click;
    }

    private void InitializeForm()
    {
        Text = "Reset Password";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(440, 220);
        Font = UiStyles.DefaultFont;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 2,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 3; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        }
        passwordTextBox.UseSystemPasswordChar = true;
        confirmPasswordTextBox.UseSystemPasswordChar = true;
        AddField(layout, 0, "New password", passwordTextBox);
        AddField(layout, 1, "Confirm password", confirmPasswordTextBox);

        resetButton.Text = "Reset";
        resetButton.Width = 100;
        resetButton.Height = 34;
        resetButton.BackColor = UiStyles.SidebarActive;
        resetButton.ForeColor = Color.White;
        resetButton.FlatStyle = FlatStyle.Flat;
        resetButton.FlatAppearance.BorderSize = 0;
        layout.Controls.Add(resetButton, 0, 2);
        layout.SetColumnSpan(resetButton, 2);
        Controls.Add(layout);
        AcceptButton = resetButton;
    }

    private static void AddField(TableLayoutPanel layout, int row, string labelText, Control control)
    {
        layout.Controls.Add(new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }

    private async void ResetButton_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show(
            "Reset this user's password?",
            "Confirm Password Reset",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        resetButton.Enabled = false;
        try
        {
            await userService.ResetPasswordAsync(userId, passwordTextBox.Text, confirmPasswordTextBox.Text);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Password Reset Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            resetButton.Enabled = true;
        }
    }
}
