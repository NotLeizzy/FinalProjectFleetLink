namespace FinalProjectFleetLink
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static async Task Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            try
            {
                await Helpers.DatabaseSeeder.SeedAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "FleetLink could not initialize the database:\n" + ex.Message,
                    "Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            Application.Run(new Forms.LoginForm());
        }
    }
}