using FinalProjectFleetLink.Forms;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Controls;

public sealed class UsersControl : UserControl
{
    private sealed record RoleFilterOption(Role? Role, string DisplayName);

    private readonly UserService userService = new();
    private readonly TextBox searchTextBox = new();
    private readonly ComboBox roleComboBox = new();
    private readonly ComboBox branchComboBox = new();
    private readonly CheckBox showInactiveCheckBox = new();
    private readonly DataGridView usersGrid = new();
    private readonly Button addButton = new();
    private readonly Button editButton = new();
    private readonly Button stateButton = new();
    private readonly Button resetButton = new();
    private bool canManage;

    public UsersControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;
        InitializeControls();
        Load += UsersControl_Load;
    }

    private void InitializeControls()
    {
        var title = new Label
        {
            Text = "User Accounts",
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
            ForeColor = UiStyles.Text
        };
        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 78,
            WrapContents = true,
            AutoScroll = true
        };
        searchTextBox.Width = 180;
        searchTextBox.PlaceholderText = "Search email or name";
        searchTextBox.TextChanged += async (_, _) => await RefreshAsync();
        roleComboBox.Width = 150;
        roleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        roleComboBox.SelectedIndexChanged += async (_, _) => await RefreshAsync();
        branchComboBox.Width = 180;
        branchComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        branchComboBox.SelectedIndexChanged += async (_, _) => await RefreshAsync();
        showInactiveCheckBox.Text = "Show inactive";
        showInactiveCheckBox.AutoSize = true;
        showInactiveCheckBox.CheckedChanged += async (_, _) => await RefreshAsync();
        filters.Controls.AddRange([new Label { Text = "Search:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, searchTextBox,
            new Label { Text = "Role:", AutoSize = true, Padding = new Padding(8, 7, 0, 0) }, roleComboBox,
            new Label { Text = "Branch:", AutoSize = true, Padding = new Padding(8, 7, 0, 0) }, branchComboBox,
            showInactiveCheckBox]);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
        addButton.Text = "Add";
        editButton.Text = "Edit";
        stateButton.Text = "Deactivate";
        resetButton.Text = "Reset Password";
        foreach (var button in new[] { addButton, editButton, stateButton, resetButton })
        {
            button.Width = 115;
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
        }
        addButton.BackColor = UiStyles.SidebarActive;
        addButton.ForeColor = Color.White;
        addButton.FlatAppearance.BorderSize = 0;
        addButton.Click += AddButton_Click;
        editButton.Click += EditButton_Click;
        stateButton.Click += StateButton_Click;
        resetButton.Click += ResetButton_Click;
        buttons.Controls.AddRange([addButton, editButton, stateButton, resetButton]);

        ConfigureGrid();
        Controls.Add(usersGrid);
        Controls.Add(buttons);
        Controls.Add(filters);
        Controls.Add(title);
    }

    private void ConfigureGrid()
    {
        usersGrid.Dock = DockStyle.Fill;
        usersGrid.ReadOnly = true;
        usersGrid.AllowUserToAddRows = false;
        usersGrid.AllowUserToDeleteRows = false;
        usersGrid.AutoGenerateColumns = false;
        usersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        usersGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        usersGrid.MultiSelect = false;
        usersGrid.RowHeadersVisible = false;
        usersGrid.BackgroundColor = Color.White;
        usersGrid.BorderStyle = BorderStyle.None;
        AddColumn("Email", "Email");
        AddColumn("FullName", "Full Name");
        AddColumn("RoleName", "Role");
        AddColumn("BranchName", "Branch");
        AddColumn("ActiveText", "Active");
        AddColumn("CreatedAtText", "Created At");
        usersGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BranchId", DataPropertyName = "BranchId", Visible = false });
        usersGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UserId", DataPropertyName = "UserId", Visible = false });
        usersGrid.SelectionChanged += (_, _) => UpdateButtons();
    }

    private void AddColumn(string propertyName, string headerText) => usersGrid.Columns.Add(new DataGridViewTextBoxColumn
    {
        Name = propertyName,
        HeaderText = headerText,
        DataPropertyName = propertyName
    });

    private async void UsersControl_Load(object? sender, EventArgs e)
    {
        canManage = Session.CurrentUser is { Role: Role.Administrator };
        addButton.Visible = canManage;
        editButton.Visible = canManage;
        stateButton.Visible = canManage;
        resetButton.Visible = canManage;
        roleComboBox.DataSource = new List<RoleFilterOption> { new(null, "All roles") }
            .Concat(Enum.GetValues<Role>().Select(role => new RoleFilterOption(role, GetRoleName(role))))
            .ToList();
        roleComboBox.DisplayMember = nameof(RoleFilterOption.DisplayName);
        roleComboBox.ValueMember = nameof(RoleFilterOption.Role);

        var branches = await BranchFilterService.GetOptionsAsync(Module.Users);
        branchComboBox.DataSource = branches.ToList();
        branchComboBox.DisplayMember = nameof(BranchFilterOption.DisplayName);
        branchComboBox.ValueMember = nameof(BranchFilterOption.BranchId);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (!IsHandleCreated || IsDisposed || roleComboBox.SelectedItem is not RoleFilterOption role
            || branchComboBox.SelectedItem is not BranchFilterOption branch)
        {
            return;
        }
        try
        {
            var users = await userService.GetUsersAsync(
                searchTextBox.Text,
                role.Role,
                branch.BranchId,
                showInactiveCheckBox.Checked);
            usersGrid.DataSource = users.Select(user => new
            {
                user.UserId,
                user.Email,
                user.FullName,
                RoleName = GetRoleName(user.Role),
                user.BranchName,
                ActiveText = user.IsActive ? "Yes" : "No",
                CreatedAtText = user.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                user.BranchId
            }).ToList();
            UpdateButtons();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Users could not be loaded.\n" + ex.Message, "Users Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateButtons()
    {
        var selected = GetSelectedUser();
        editButton.Enabled = canManage && selected is not null;
        stateButton.Enabled = canManage && selected is not null;
        resetButton.Enabled = canManage && selected is not null;
        if (selected is not null)
        {
            stateButton.Text = selected.IsActive ? "Deactivate" : "Activate";
        }
    }

    private async void AddButton_Click(object? sender, EventArgs e)
    {
        using var form = new UserForm(userService);
        if (form.ShowDialog(this) == DialogResult.OK) await RefreshAsync();
    }

    private async void EditButton_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedUser();
        if (selected is null) return;
        using var form = new UserForm(userService, selected);
        if (form.ShowDialog(this) == DialogResult.OK) await RefreshAsync();
    }

    private async void StateButton_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedUser();
        if (selected is null) return;
        var action = selected.IsActive ? "deactivate" : "activate";
        if (MessageBox.Show($"Do you want to {action} {selected.Email}?", "Confirm Account Change", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            await userService.SetActiveAsync(selected.UserId, !selected.IsActive);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Account Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ResetButton_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedUser();
        if (selected is null) return;
        using var form = new PasswordResetForm(userService, selected.UserId);
        form.ShowDialog(this);
    }

    private UserListItem? GetSelectedUser()
    {
        if (usersGrid.CurrentRow is null) return null;
        var row = usersGrid.CurrentRow;
        var role = Enum.GetValues<Role>().FirstOrDefault(value => GetRoleName(value) == Convert.ToString(row.Cells["RoleName"].Value));
        return new UserListItem(
            Convert.ToInt32(row.Cells["UserId"].Value),
            Convert.ToString(row.Cells["Email"].Value) ?? string.Empty,
            Convert.ToString(row.Cells["FullName"].Value) ?? string.Empty,
            role,
            row.Cells["BranchId"].Value is null || row.Cells["BranchId"].Value == DBNull.Value
                ? null
                : Convert.ToInt32(row.Cells["BranchId"].Value),
            Convert.ToString(row.Cells["BranchName"].Value) ?? string.Empty,
            string.Equals(Convert.ToString(row.Cells["ActiveText"].Value), "Yes", StringComparison.Ordinal),
            DateTime.Parse(Convert.ToString(row.Cells["CreatedAtText"].Value)!));
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
