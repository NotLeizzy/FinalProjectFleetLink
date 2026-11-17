using FinalProjectFleetLink.Controls;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Forms;

public sealed class MainForm : Form
{
    private readonly Panel contentPanel = new();
    private readonly FlowLayoutPanel sidebarPanel = new();
    private readonly Label branchLabel = new();
    private readonly Dictionary<Module, Button> moduleButtons = new();
    private Button? activeButton;

    public MainForm()
    {
        if (!Session.IsLoggedIn)
        {
            throw new InvalidOperationException("A logged-in user is required to open the main form.");
        }

        InitializeForm();
        Load += MainForm_Load;
        FormClosed += (_, _) => Session.Logout();
    }

    private void InitializeForm()
    {
        Text = "FleetLink";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(900, 600);
        Font = UiStyles.DefaultFont;
        BackColor = UiStyles.ContentBackground;

        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = UiStyles.TopBar,
            Padding = new Padding(20, 0, 20, 0)
        };
        var appNameLabel = new Label
        {
            Text = "FleetLink",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = UiStyles.Sidebar,
            Location = new Point(20, 22)
        };
        var userLabel = new Label
        {
            Text = Session.CurrentUser!.FullName,
            AutoSize = true,
            ForeColor = UiStyles.Text,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(530, 14)
        };
        var roleLabel = new Label
        {
            Text = GetRoleName(Session.CurrentUser.Role),
            AutoSize = true,
            ForeColor = Color.DimGray,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(530, 38)
        };
        branchLabel.Text = "Loading branch...";
        branchLabel.AutoSize = true;
        branchLabel.ForeColor = Color.DimGray;
        branchLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        branchLabel.Location = new Point(700, 38);

        var logoutButton = new Button
        {
            Text = "Logout",
            Width = 90,
            Height = 32,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(800, 18)
        };
        logoutButton.Click += (_, _) =>
        {
            Session.Logout();
            Close();
        };
        topBar.Resize += (_, _) =>
        {
            logoutButton.Left = topBar.ClientSize.Width - logoutButton.Width;
            userLabel.Left = logoutButton.Left - userLabel.Width - 24;
            roleLabel.Left = userLabel.Left;
            branchLabel.Left = roleLabel.Left;
        };
        topBar.Controls.AddRange([appNameLabel, userLabel, roleLabel, branchLabel, logoutButton]);

        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = UiStyles.Sidebar,
            Padding = new Padding(10, 20, 10, 10)
        };
        sidebarPanel.Dock = DockStyle.Fill;
        sidebarPanel.FlowDirection = FlowDirection.TopDown;
        sidebarPanel.WrapContents = false;
        sidebarPanel.AutoScroll = true;
        sidebarPanel.BackColor = UiStyles.Sidebar;
        sidebar.Controls.Add(sidebarPanel);

        contentPanel.Dock = DockStyle.Fill;
        contentPanel.BackColor = UiStyles.ContentBackground;
        contentPanel.Padding = new Padding(18);

        Controls.Add(contentPanel);
        Controls.Add(sidebar);
        Controls.Add(topBar);

        BuildSidebar(Session.CurrentUser.Role);
    }

    private void BuildSidebar(Role role)
    {
        foreach (var module in Enum.GetValues<Module>())
        {
            if (!Permissions.CanAccess(role, module))
            {
                continue;
            }

            var moduleButton = new Button
            {
                Text = GetModuleName(module),
                Tag = module,
                Width = 190,
                Height = 42,
                Margin = new Padding(0, 0, 0, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = UiStyles.SidebarButton,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                FlatAppearance = { BorderSize = 0 }
            };
            moduleButton.Click += ModuleButton_Click;
            moduleButtons[module] = moduleButton;
            sidebarPanel.Controls.Add(moduleButton);
        }
    }

    private void ModuleButton_Click(object? sender, EventArgs e)
    {
        if (sender is Button { Tag: Module module } button)
        {
            ShowModule(module, button);
        }
    }

    private void ShowModule(Module module, Button button)
    {
        activeButton?.BackColor = UiStyles.SidebarButton;
        activeButton = button;
        activeButton.BackColor = UiStyles.SidebarActive;

        var nextControl = CreateModuleControl(module);
        var oldControl = contentPanel.Controls.Count == 0 ? null : contentPanel.Controls[0];
        contentPanel.Controls.Clear();
        oldControl?.Dispose();
        contentPanel.Controls.Add(nextControl);
    }

    private static UserControl CreateModuleControl(Module module)
    {
        return module switch
        {
            Module.Branches => new BranchesControl(),
            Module.Users => new UsersControl(),
            Module.Dashboard => Session.CurrentUser?.Role switch
            {
                Role.Dispatcher => new DispatcherDashboardControl(),
                Role.MaintenanceOfficer => new MaintenanceDashboardControl(),
                _ => new DashboardControl()
            },
            Module.Reports => new ReportsControl(),
            Module.FuelMonitor => new FuelMonitorControl(),
            Module.Assignments => new AssignmentsControl(),
            Module.Shipments => new ShipmentsControl(),
            Module.Trips => new TripsControl(),
            Module.Maintenance => new MaintenanceControl(),
            _ => new PlaceholderControl(module)
        };
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        if (Session.CurrentUser!.IsHeadOffice)
        {
            branchLabel.Text = "Head Office";
        }
        else
        {
            try
            {
                await using var db = DbContextFactory.Create();
                branchLabel.Text = await db.Branches
                    .Where(branch => branch.BranchId == Session.CurrentUser.BranchId)
                    .Select(branch => branch.BranchName)
                    .SingleOrDefaultAsync() ?? "Unknown branch";
            }
            catch
            {
                branchLabel.Text = "Unknown branch";
            }
        }

        var dashboardButton = moduleButtons.GetValueOrDefault(Module.Dashboard);
        if (dashboardButton is not null)
        {
            ShowModule(Module.Dashboard, dashboardButton);
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
