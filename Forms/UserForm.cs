using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class UserForm : Form
{
    private sealed record RoleOption(Role Role, string DisplayName);
    private sealed record BranchOption(int? BranchId, string DisplayName);

    private readonly UserService userService;
    private readonly UserListItem? existingUser;
    private readonly TextBox emailTextBox = new();
    private readonly TextBox fullNameTextBox = new();
    private readonly ComboBox roleComboBox = new();
    private readonly ComboBox branchComboBox = new();
    private readonly TextBox passwordTextBox = new();
    private readonly TextBox confirmPasswordTextBox = new();
    private readonly Button saveButton = new();

    public UserForm(UserService userService, UserListItem? existingUser = null)
    {
        this.userService = userService;
        this.existingUser = existingUser;
        InitializeForm();
        Load += UserForm_Load;
        saveButton.Click += SaveButton_Click;
    }

    private void InitializeForm()
    {
        Text = existingUser is null ? "Add User" : "Edit User";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, existingUser is null ? 390 : 300);
        Font = UiStyles.DefaultFont;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 2,
            RowCount = existingUser is null ? 7 : 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < layout.RowCount; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        }

        emailTextBox.MaxLength = 100;
        emailTextBox.Text = existingUser?.Email ?? string.Empty;
        emailTextBox.ReadOnly = existingUser is not null;
        fullNameTextBox.MaxLength = 100;
        fullNameTextBox.Text = existingUser?.FullName ?? string.Empty;
        roleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        passwordTextBox.UseSystemPasswordChar = true;
        confirmPasswordTextBox.UseSystemPasswordChar = true;

        AddField(layout, 0, "Email", emailTextBox);
        AddField(layout, 1, "Full name", fullNameTextBox);
        AddField(layout, 2, "Role", roleComboBox);
        AddField(layout, 3, "Branch", branchComboBox);
        if (existingUser is null)
        {
            AddField(layout, 4, "Password", passwordTextBox);
            AddField(layout, 5, "Confirm password", confirmPasswordTextBox);
        }

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        saveButton.Text = "Save";
        saveButton.Width = 100;
        saveButton.Height = 34;
        saveButton.BackColor = UiStyles.SidebarActive;
        saveButton.ForeColor = Color.White;
        saveButton.FlatStyle = FlatStyle.Flat;
        saveButton.FlatAppearance.BorderSize = 0;
        var cancelButton = new Button
        {
            Text = "Cancel",
            Width = 100,
            Height = 34,
            DialogResult = DialogResult.Cancel
        };
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 0, layout.RowCount - 1);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private static void AddField(TableLayoutPanel layout, int row, string labelText, Control control)
    {
        layout.Controls.Add(new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 0, 0)
        }, 0, row);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }

    private async void UserForm_Load(object? sender, EventArgs e)
    {
        try
        {
            roleComboBox.DataSource = Enum.GetValues<Role>()
                .Select(role => new RoleOption(role, GetRoleName(role)))
                .ToList();
            roleComboBox.DisplayMember = nameof(RoleOption.DisplayName);
            roleComboBox.ValueMember = nameof(RoleOption.Role);

            var branches = await new BranchService().GetActiveBranchesAsync();
            var branchOptions = new List<BranchOption> { new(null, "Head Office (all branches)") };
            branchOptions.AddRange(branches.Select(branch =>
                new BranchOption(branch.BranchId, $"{branch.BranchCode} - {branch.BranchName}")));
            branchComboBox.DataSource = branchOptions;
            branchComboBox.DisplayMember = nameof(BranchOption.DisplayName);
            branchComboBox.ValueMember = nameof(BranchOption.BranchId);

            if (existingUser is not null)
            {
                roleComboBox.SelectedValue = existingUser.Role;
                branchComboBox.SelectedValue = existingUser.BranchId;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "User form data could not be loaded.\n" + ex.Message,
                "User Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (roleComboBox.SelectedItem is not RoleOption role
            || branchComboBox.SelectedItem is not BranchOption branch)
        {
            MessageBox.Show("Select a role and branch option.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (branch.BranchId is null && role.Role != Role.Administrator
            && MessageBox.Show(
                "This non-administrator account will be assigned to Head Office and can access all branches. Continue?",
                "Head Office Assignment",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        saveButton.Enabled = false;
        try
        {
            var request = new UserRequest(
                emailTextBox.Text,
                fullNameTextBox.Text,
                role.Role,
                branch.BranchId,
                existingUser is null ? passwordTextBox.Text : null,
                existingUser is null ? confirmPasswordTextBox.Text : null);
            if (existingUser is null)
            {
                await userService.CreateAsync(request);
            }
            else
            {
                await userService.UpdateAsync(existingUser.UserId, request);
            }
            DialogResult = DialogResult.OK;
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show(ex.Message, "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("The user could not be saved.\n" + ex.Message, "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }

    private static string GetRoleName(Role role) => role switch
    {
        Role.Administrator => "Administrator",
        Role.FleetManager => "Fleet Manager",
        Role.Dispatcher => "Dispatcher",
        Role.MaintenanceOfficer => "Maintenance Officer",
        Role.FuelFinanceClerk => "Fuel/Finance Clerk",
        _ => role.ToString()
    };
}
