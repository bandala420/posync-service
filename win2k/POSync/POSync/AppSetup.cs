using System;
using System.IO;
using System.Windows.Forms;

namespace POSync
{
    public partial class AppSetup : Form
    {
        private RestInfoCollection restInfo;
        private readonly string assemblyPath;
        public AppSetup(string serviceAssembly)
        {
            InitializeComponent();
            this.assemblyPath = serviceAssembly;
        }

        private void AppSetup_Load(object sender, EventArgs e)
        {
            this.label4.Hide();
            this.label5.Hide();
            this.label6.Show();
            this.label7.Hide();
            this.linkLabel2.Hide();
            this.label3.Enabled = false;
            this.comboBox2.Enabled = false;
            this.restInfo = AppInstaller.PopulateRestInfo();
            if (this.restInfo != null)
            {
                this.comboBox1.Items.Clear();
                this.comboBox2.Items.Clear();
                for (int i = 0; i < this.restInfo.CustomRestInfo.Length; i++)
                {
                    this.comboBox1.Items.AddRange(new object[] { @"  " + this.restInfo.CustomRestInfo[i].ID + @"  -  " + this.restInfo.CustomRestInfo[i].RestName });
                }
            }
        }
        private void AppSetup_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!AppInstaller.Successs)
            {
                var window = MessageBox.Show("¿Está seguro que desea cancelar la instalación del servicio de sincronización?", "Advertencia", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                e.Cancel = (window == DialogResult.No);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (this.comboBox1.SelectedItem == null)
            {
                MessageBox.Show("DEBE SELECCIONAR UN RESTAURANTE", "Alerta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (this.comboBox2.SelectedItem == null || this.comboBox2.SelectedItem.ToString() == "  Otro")
            {
                MessageBox.Show("DEBE SELECCIONAR UN PUNTO DE VENTA", "Alerta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                int selectedRest = this.comboBox1.SelectedIndex;
                int selectedDevice = this.comboBox2.SelectedIndex;
                string driveLetter = AppInstaller.FindDirectory();
                string deviceType = this.restInfo.CustomRestInfo[selectedRest].Device[selectedDevice].Type;
                AppInstaller.SetConfig(this.assemblyPath, this.restInfo.CustomRestInfo[selectedRest].FolderName, this.restInfo.CustomRestInfo[selectedRest].Device[selectedDevice].Value, driveLetter, deviceType);
                AppInstaller.EncryptAppSettings(this.assemblyPath);
                MessageBox.Show("El servicio de sincronización POSync se ha instalado y configurado correctamente.", "Instalación completa", MessageBoxButtons.OK, MessageBoxIcon.None);
                AppInstaller.Successs = true;
                Application.Exit();
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.comboBox2.Items.Clear();
            int selectedIndex = this.comboBox1.SelectedIndex;
            for (int i = 0; i < this.restInfo.CustomRestInfo[selectedIndex].Device.Length; i++)
            {
                string itemText = @" " + this.restInfo.CustomRestInfo[selectedIndex].Device[i].Type;
                if (itemText.ToLower().Contains("pos") || itemText.ToLower().Contains("itona"))
                {
                    itemText += @" " + this.restInfo.CustomRestInfo[selectedIndex].Device[i].Value;
                    if (this.restInfo.CustomRestInfo[selectedIndex].Device[i].Entrance.Length > 2)
                        itemText += @"   -   " + this.restInfo.CustomRestInfo[selectedIndex].Device[i].Entrance;
                }
                this.comboBox2.Items.AddRange(new object[] { itemText });
            }
            this.comboBox2.Items.AddRange(new object[] { @"  Otro" });
            this.label3.Enabled = true;
            this.comboBox2.Enabled = true;
            this.label7.Show();
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.comboBox2.SelectedItem.ToString() == "  Otro")
            {
                this.label4.Show();
                this.label5.Show();
                this.linkLabel2.Show();
            }
            else
            {
                this.label4.Hide();
                this.label5.Hide();
                this.linkLabel2.Hide();
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.AppSetup_Load(sender, e);
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.linkLabel2.LinkVisited = true;
            System.Diagnostics.Process.Start("http://oceanodigital.mx");
        }

        private void label6_Click(object sender, EventArgs e)
        {
            this.comboBox1.DroppedDown = true;
        }

        private void label7_Click(object sender, EventArgs e)
        {
            this.comboBox2.DroppedDown = true;
        }

        private void comboBox1_DropDown(object sender, EventArgs e)
        {
            this.label6.Hide();
        }

        private void comboBox2_DropDown(object sender, EventArgs e)
        {
            this.label7.Hide();
        }

        private void comboBox1_DropDownClosed(object sender, EventArgs e)
        {
            if (this.comboBox1.SelectedItem == null)
            {
                this.label6.Show();
            }
        }

        private void comboBox2_DropDownClosed(object sender, EventArgs e)
        {
            if (this.comboBox2.SelectedItem == null)
            {
                this.label7.Show();
            }
        }
    }
}
