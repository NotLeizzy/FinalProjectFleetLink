using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;
using FinalProjectFleetLink.Forms;

namespace FinalProjectFleetLink.Controls;

public sealed class BranchesControl : UserControl
{
    private readonly BranchService branchService = new();
    private readonly DataGridView branchesGrid = new();
    private readonly CheckBox showInactiveCheckBox = new();
    private readonly Button addButton = new();
    private readonly Button editButton = new();
    private readonly Button stateButton = new();
    private readonly Label scopeLabel = new();
    private bool canManage;

    public BranchesControl()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;
        InitializeControls();
        Load += BranchesControl_Load;
    }

    private void InitializeControls()
    {
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 74
        };
        var titleLabel = new Label
        {
            Text = "Branches",
            AutoSize = true,
            Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
            ForeColor = UiStyles.Text,
            Location = new Point(0, 0)
        };
        scopeLabel.AutoSize = true;
        scopeLabel.ForeColor = Color.DimGray;
        scopeLabel.Location = new Point(0, 32);

        showInactiveCheckBox.Text = "Show inactive";
        showInactiveCheckBox.AutoSize = true;
        showInactiveCheckBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        showInactiveCheckBox.Location = new Point(510, 5);
        showInactiveCheckBox.CheckedChanged += async (_, _) => await RefreshAsync();

        addButton.Text = "Add";
        editButton.Text = "Edit";
        stateButton.Text = "Deactivate";
        foreach (var button in new[] { addButton, editButton, stateButton })
        {
            button.Width = 100;
            button.Height = 32;
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button.FlatStyle = FlatStyle.Flat;
        }
        addButton.BackColor = UiStyles.SidebarActive;
        addButton.ForeColor = Color.White;
        addButton.FlatAppearance.BorderSize = 0;
        addButton.Location = new Point(510, 38);
        editButton.Location = new Point(620, 38);
        stateButton.Location = new Point(730, 38);
        addButton.Click += AddButton_Click;
        editButton.Click += EditButton_Click;
        stateButton.Click += StateButton_Click;

        headerPanel.Controls.AddRange([
            titleLabel, scopeLabel, showInactiveCheckBox,
            addButton, editButton, stateButton
        ]);

        ConfigureGrid();
        Controls.Add(branchesGrid);
        Controls.Add(headerPanel);
    }

    private void ConfigureGrid()
    {
        branchesGrid.Dock = DockStyle.Fill;
        branchesGrid.ReadOnly = true;
        branchesGrid.AllowUserToAddRows = false;
        branchesGrid.AllowUserToDeleteRows = false;
        branchesGrid.AllowUserToResizeRows = false;
        branchesGrid.AutoGenerateColumns = false;
        branchesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        branchesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        branchesGrid.MultiSelect = false;
        branchesGrid.RowHeadersVisible = false;
        branchesGrid.BackgroundColor = Color.White;
        branchesGrid.BorderStyle = BorderStyle.None;
        branchesGrid.CellDoubleClick += (_, _) => EditSelectedBranch();
        branchesGrid.SelectionChanged += (_, _) => UpdateActionButtons();

        AddColumn("BranchCode", "Branch Code");
        AddColumn("BranchName", "Branch Name");
        AddColumn("Address", "Address");
        AddColumn("ActiveText", "Active");
        AddColumn("ActiveVehicles", "Active Vehicles");
        AddColumn("ActiveDrivers", "Active Drivers");
        AddColumn("Users", "Users");
        var idColumn = new DataGridViewTextBoxColumn
        {
            Name = "BranchId",
            DataPropertyName = "BranchId",
            Visible = false
        };
        branchesGrid.Columns.Add(idColumn);
    }

    private void AddColumn(string propertyName, string headerText)
    {
        branchesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = propertyName,
            HeaderText = headerText,
            DataPropertyName = propertyName
        });
    }

    private async void BranchesControl_Load(object? sender, EventArgs e)
    {
        canManage = Session.CurrentUser is { Role: Role.Administrator, IsHeadOffice: true };
        scopeLabel.Text = canManage
            ? "All branches | Head-office administrator"
            : "Your assigned branch | Read-only";
        addButton.Visible = canManage;
        editButton.Visible = canManage;
        stateButton.Visible = canManage;
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            var branches = await branchService.GetBranchesAsync(showInactiveCheckBox.Checked);
            branchesGrid.DataSource = branches.Select(branch => new
            {
                branch.BranchId,
                branch.BranchCode,
                branch.BranchName,
                Address = branch.Address ?? string.Empty,
                ActiveText = branch.IsActive ? "Yes" : "No",
                branch.ActiveVehicles,
                branch.ActiveDrivers,
                branch.Users
            }).ToList();
            UpdateActionButtons();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Branches could not be loaded.\n" + ex.Message,
                "Load Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void UpdateActionButtons()
    {
        var selected = GetSelectedBranch();
        editButton.Enabled = canManage && selected is not null;
        stateButton.Enabled = canManage && selected is not null;
        if (selected is not null)
        {
            stateButton.Text = selected.IsActive ? "Deactivate" : "Activate";
        }
    }

    private async void AddButton_Click(object? sender, EventArgs e)
    {
        using var form = new BranchForm(branchService);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await RefreshAsync();
        }
    }

    private async void EditButton_Click(object? sender, EventArgs e)
    {
        await EditSelectedBranch();
    }

    private async Task EditSelectedBranch()
    {
        var selected = GetSelectedBranch();
        if (!canManage || selected is null)
        {
            return;
        }

        using var form = new BranchForm(branchService, selected);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            await RefreshAsync();
        }
    }

    private async void StateButton_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedBranch();
        if (!canManage || selected is null)
        {
            return;
        }

        try
        {
            if (!selected.IsActive)
            {
                if (MessageBox.Show(
                    $"Activate branch {selected.BranchCode}?",
                    "Confirm Activation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }

                await branchService.ActivateAsync(selected.BranchId);
                await RefreshAsync();
                return;
            }

            var result = await branchService.DeactivateAsync(selected.BranchId, false);
            if (result.ErrorMessage is not null)
            {
                MessageBox.Show(result.ErrorMessage, "Branch Action", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var check = result.DeactivationCheck!;
            if (check.HasBlockingRecords)
            {
                MessageBox.Show(
                    BuildBlockerMessage(check),
                    "Branch Cannot Be Deactivated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (check.ActiveUsers > 0)
            {
                var warning = $"{check.ActiveUsers} active user account(s) belong to this branch. "
                    + "They will remain linked, but this branch cannot be selected for new records.\n\n"
                    + "Continue deactivating the branch?";
                if (MessageBox.Show(warning, "Active Users", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                    != DialogResult.Yes)
                {
                    return;
                }

                result = await branchService.DeactivateAsync(selected.BranchId, true);
                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? BuildBlockerMessage(result.DeactivationCheck!),
                        "Branch Cannot Be Deactivated",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (MessageBox.Show(
                $"Deactivate branch {selected.BranchCode}?",
                "Confirm Deactivation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                result = await branchService.DeactivateAsync(selected.BranchId, true);
                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? BuildBlockerMessage(result.DeactivationCheck!),
                        "Branch Cannot Be Deactivated",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                return;
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The branch status could not be changed.\n" + ex.Message,
                "Branch Action Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private BranchListItem? GetSelectedBranch()
    {
        if (branchesGrid.CurrentRow?.DataBoundItem is not null)
        {
            var id = Convert.ToInt32(branchesGrid.CurrentRow.Cells["BranchId"].Value);
            var code = Convert.ToString(branchesGrid.CurrentRow.Cells["BranchCode"].Value) ?? string.Empty;
            var name = Convert.ToString(branchesGrid.CurrentRow.Cells["BranchName"].Value) ?? string.Empty;
            var address = Convert.ToString(branchesGrid.CurrentRow.Cells["Address"].Value);
            var active = string.Equals(
                Convert.ToString(branchesGrid.CurrentRow.Cells["ActiveText"].Value),
                "Yes",
                StringComparison.Ordinal);
            var activeVehicles = Convert.ToInt32(branchesGrid.CurrentRow.Cells["ActiveVehicles"].Value);
            var activeDrivers = Convert.ToInt32(branchesGrid.CurrentRow.Cells["ActiveDrivers"].Value);
            var users = Convert.ToInt32(branchesGrid.CurrentRow.Cells["Users"].Value);
            return new BranchListItem(id, code, name, address, active, activeVehicles, activeDrivers, users);
        }
        return null;
    }

    private static string BuildBlockerMessage(BranchDeactivationCheck check)
    {
        var lines = new List<string>();
        if (check.ActiveVehicles > 0)
        {
            lines.Add($"Active vehicles: {check.ActiveVehicles}");
        }
        if (check.ActiveDrivers > 0)
        {
            lines.Add($"Active drivers: {check.ActiveDrivers}");
        }
        if (check.UnfinishedTrips > 0)
        {
            lines.Add($"Unfinished trips: {check.UnfinishedTrips}");
        }
        return "The branch cannot be deactivated because of the following:\n\n"
            + string.Join("\n", lines);
    }
}
