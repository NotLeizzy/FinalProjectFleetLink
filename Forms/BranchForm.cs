using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class BranchForm : Form
{
    private readonly BranchService branchService;
    private readonly int? branchId;
    private readonly TextBox codeTextBox = new();
    private readonly TextBox nameTextBox = new();
    private readonly TextBox addressTextBox = new();
    private readonly Button saveButton = new();
    private readonly Button cancelButton = new();

    public BranchForm(BranchService branchService, BranchListItem? branch = null)
    {
        this.branchService = branchService;
        branchId = branch?.BranchId;
        InitializeForm(branch);
        saveButton.Click += SaveButton_Click;
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
    }

    private void InitializeForm(BranchListItem? branch)
    {
        Text = branch is null ? "Add Branch" : "Edit Branch";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(480, 270);
        Font = UiStyles.DefaultFont;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 2,
            RowCount = 4,
            AutoSize = false
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 4; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        }

        codeTextBox.CharacterCasing = CharacterCasing.Upper;
        codeTextBox.MaxLength = 10;
        codeTextBox.Text = branch?.BranchCode ?? string.Empty;
        codeTextBox.ReadOnly = branch is not null;

        nameTextBox.MaxLength = 100;
        nameTextBox.Text = branch?.BranchName ?? string.Empty;
        addressTextBox.MaxLength = 200;
        addressTextBox.Text = branch?.Address ?? string.Empty;

        AddField(layout, 0, "BranchCode", codeTextBox);
        AddField(layout, 1, "BranchName", nameTextBox);
        AddField(layout, 2, "Address", addressTextBox);

        var buttonPanel = new FlowLayoutPanel
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
        cancelButton.Text = "Cancel";
        cancelButton.Width = 100;
        cancelButton.Height = 34;
        cancelButton.DialogResult = DialogResult.Cancel;
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);
        layout.Controls.Add(buttonPanel, 0, 3);
        layout.SetColumnSpan(buttonPanel, 2);

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

    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        saveButton.Enabled = false;
        try
        {
            var request = new BranchRequest(
                codeTextBox.Text,
                nameTextBox.Text,
                addressTextBox.Text);

            if (branchId.HasValue)
            {
                await branchService.UpdateAsync(branchId.Value, request);
            }
            else
            {
                await branchService.CreateAsync(request);
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
            MessageBox.Show(
                "The branch could not be saved.\n" + ex.Message,
                "Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            saveButton.Enabled = true;
        }
    }
}
