namespace FinalProjectFleetLink
{
    public partial class Form1 : Form
    {
        private readonly DbConnections db = new DbConnections();
        public Form1()
        {
            try
            {
                using (var connection = db.GetConnection())
                {
                    connection.Open();

                    MessageBox.Show(
                        "Successfully connected to FleetLinkDB!",
                        "Database Connection",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Database connection failed:\n" + ex.Message,
                    "Connection Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
