using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using FinalProjectFleetLink.Services;

namespace FinalProjectFleetLink.Forms;

public sealed class TripStatusForm : Form
{
    private readonly TripStatusService service = new();
    private readonly int tripId;
    private readonly Label statusLabel = new();
    private readonly CheckBox distanceCheckBox = new();
    private readonly NumericUpDown distanceInput = new();
    private readonly Button startButton = new();
    private readonly Button completeButton = new();
    private TripStatus status;

    public TripStatusForm(int tripId, TripStatus status)
    {
        this.tripId = tripId;
        this.status = status;
        Text = $"Update Trip Status - Trip {tripId}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 210);
        Font = UiStyles.DefaultFont;
        InitializeControls();
        UpdateActions();
    }

    private void InitializeControls()
    {
        var title = new Label
        {
            Text = $"Update status for Trip {tripId}",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
            ForeColor = UiStyles.Text
        };
        statusLabel.Dock = DockStyle.Top;
        statusLabel.Height = 32;
        statusLabel.Padding = new Padding(12, 4, 0, 0);
        var distancePanel = new Panel { Dock = DockStyle.Top, Height = 45, Padding = new Padding(12, 5, 0, 0) };
        distanceCheckBox.Text = "Enter actual distance (km)";
        distanceCheckBox.AutoSize = true;
        distanceCheckBox.CheckedChanged += (_, _) => distanceInput.Enabled = distanceCheckBox.Checked;
        distanceInput.DecimalPlaces = 2;
        distanceInput.Maximum = 999999999;
        distanceInput.Minimum = 0.01M;
        distanceInput.Increment = 0.1M;
        distanceInput.Width = 110;
        distanceInput.Location = new Point(205, 2);
        distanceInput.Enabled = false;
        distancePanel.Controls.Add(distanceCheckBox);
        distancePanel.Controls.Add(distanceInput);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 5, 8, 0) };
        ConfigureButton(startButton, "Start Trip");
        ConfigureButton(completeButton, "Complete Trip");
        var closeButton = new Button { Text = "Close", Width = 90, Height = 34, DialogResult = DialogResult.Cancel };
        startButton.Click += StartButton_Click;
        completeButton.Click += CompleteButton_Click;
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(completeButton);
        buttons.Controls.Add(startButton);
        Controls.Add(distancePanel);
        Controls.Add(statusLabel);
        Controls.Add(buttons);
        Controls.Add(title);
        CancelButton = closeButton;
    }

    private void UpdateActions()
    {
        statusLabel.Text = $"Current status: {EnumDisplay.ToDisplayName(status)}";
        startButton.Visible = status == TripStatus.Dispatched;
        completeButton.Visible = status == TripStatus.InTransit;
        distanceCheckBox.Visible = status == TripStatus.InTransit;
        distanceInput.Visible = status == TripStatus.InTransit;
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        startButton.Enabled = false;
        try
        {
            var result = await service.StartAsync(tripId);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "Trip Status", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MessageBox.Show("The trip is now In Transit.", "Trip Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
        finally
        {
            startButton.Enabled = true;
        }
    }

    private async void CompleteButton_Click(object? sender, EventArgs e)
    {
        completeButton.Enabled = false;
        try
        {
            decimal? distance = distanceCheckBox.Checked ? distanceInput.Value : null;
            var result = await service.CompleteAsync(tripId, distance);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "Trip Status", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MessageBox.Show("The trip was completed.", "Trip Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
        finally
        {
            completeButton.Enabled = true;
        }
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Text = text;
        button.Width = 115;
        button.Height = 34;
        button.BackColor = UiStyles.SidebarActive;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
    }
}
