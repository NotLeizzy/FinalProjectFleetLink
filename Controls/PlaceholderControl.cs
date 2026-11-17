using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;

namespace FinalProjectFleetLink.Controls;

public sealed class PlaceholderControl : UserControl
{
    public PlaceholderControl(Module module)
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyles.ContentBackground;

        var titleLabel = new Label
        {
            Text = GetModuleName(module),
            AutoSize = true,
            Font = new Font(UiStyles.DefaultFont, FontStyle.Bold),
            ForeColor = UiStyles.Text,
            Location = new Point(32, 28)
        };
        var messageLabel = new Label
        {
            Text = "Coming soon",
            AutoSize = true,
            Font = new Font(UiStyles.DefaultFont.FontFamily, 18F),
            ForeColor = Color.DimGray,
            Location = new Point(32, 76)
        };
        Controls.Add(titleLabel);
        Controls.Add(messageLabel);
    }

    private static string GetModuleName(Module module) => module switch
    {
        Module.Users => "Users",
        Module.Branches => "Branches",
        Module.Vehicles => "Vehicles",
        Module.Drivers => "Drivers",
        Module.Assignments => "Assignments",
        Module.Shipments => "Shipments",
        Module.Trips => "Trips",
        Module.Dispatch => "Dispatch",
        Module.Maintenance => "Maintenance",
        Module.FuelMonitor => "Fuel Monitor",
        Module.Reports => "Reports",
        _ => "Dashboard"
    };
}
