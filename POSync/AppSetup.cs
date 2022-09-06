using System;
using System.IO;
using System.Windows.Forms;

namespace POSync
{
    public class AppSetup : Form
    {
        private Button button1;
        private Label label1;
        private Label label2;
        private ComboBox comboBox2;
        private Label label3;
        private Label label4;
        private ComboBox comboBox1;
        private RestInfoCollection restInfo;
        private LinkLabel linkLabel1;
        private Label label5;
        private LinkLabel linkLabel2;
        private Label label6;
        private Label label7;
        private readonly string assemblyPath;

        public AppSetup(string serviceAssembly)
        {
            InitializeComponent();
            this.assemblyPath = serviceAssembly;
        }
        private void InitializeComponent()
        {
            this.button1 = new System.Windows.Forms.Button();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.comboBox2 = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.label5 = new System.Windows.Forms.Label();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.Location = new System.Drawing.Point(343, 194);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(93, 30);
            this.button1.TabIndex = 0;
            this.button1.Text = "Guardar";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // comboBox1
            // 
            this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(167, 74);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(226, 24);
            this.comboBox1.TabIndex = 1;
            this.comboBox1.Tag = "Restaurante";
            this.comboBox1.DropDown += new System.EventHandler(this.comboBox1_DropDown);
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            this.comboBox1.DropDownClosed += new System.EventHandler(this.comboBox1_DropDownClosed);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(17, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(419, 30);
            this.label1.TabIndex = 2;
            this.label1.Text = "Ingrese la información del dispositivo donde ha instalado el servicio de \r\nsincro" +
    "nización. Si lo requiere, verifique la información con su administrador.";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(40, 74);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(81, 16);
            this.label2.TabIndex = 3;
            this.label2.Text = "Restaurante";
            // 
            // comboBox2
            // 
            this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Location = new System.Drawing.Point(167, 119);
            this.comboBox2.Name = "comboBox2";
            this.comboBox2.Size = new System.Drawing.Size(226, 24);
            this.comboBox2.TabIndex = 4;
            this.comboBox2.Tag = "POS";
            this.comboBox2.DropDown += new System.EventHandler(this.comboBox2_DropDown);
            this.comboBox2.SelectedIndexChanged += new System.EventHandler(this.comboBox2_SelectedIndexChanged);
            this.comboBox2.DropDownClosed += new System.EventHandler(this.comboBox2_DropDownClosed);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(40, 119);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(75, 16);
            this.label3.TabIndex = 5;
            this.label3.Text = "Dispositivo";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(164, 146);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(58, 15);
            this.label4.TabIndex = 6;
            this.label4.Text = "Ingrese a";
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkLabel1.LinkBehavior = System.Windows.Forms.LinkBehavior.NeverUnderline;
            this.linkLabel1.Location = new System.Drawing.Point(17, 201);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(58, 15);
            this.linkLabel1.TabIndex = 7;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "Recargar";
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(164, 161);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(193, 15);
            this.label5.TabIndex = 8;
            this.label5.Text = "para registrar un nuevo dispositivo";
            // 
            // linkLabel2
            // 
            this.linkLabel2.AutoSize = true;
            this.linkLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkLabel2.Location = new System.Drawing.Point(219, 146);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(101, 15);
            this.linkLabel2.TabIndex = 9;
            this.linkLabel2.TabStop = true;
            this.linkLabel2.Text = "oceanodigital.mx";
            this.linkLabel2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel2_LinkClicked);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.BackColor = System.Drawing.SystemColors.Window;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(219, 77);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(103, 16);
            this.label6.TabIndex = 10;
            this.label6.Text = "S e l e c c i o n e";
            this.label6.Click += new System.EventHandler(this.label6_Click);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.BackColor = System.Drawing.SystemColors.Window;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(219, 122);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(103, 16);
            this.label7.TabIndex = 11;
            this.label7.Text = "S e l e c c i o n e";
            this.label7.Click += new System.EventHandler(this.label7_Click);
            // 
            // AppSetup
            // 
            this.ClientSize = new System.Drawing.Size(454, 239);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.linkLabel2);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.comboBox2);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.button1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "AppSetup";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Configuración";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.AppConfig_FormClosing);
            this.Load += new System.EventHandler(this.AppConfig_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        private void AppConfig_Load(object sender, EventArgs e)
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

        private void AppConfig_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!AppInstaller.Successs)
            {
                var window = MessageBox.Show("¿Está seguro que desea cancelar la instalación del servicio de sincronización?","Advertencia",MessageBoxButtons.YesNo,MessageBoxIcon.Exclamation);
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
                MessageBox.Show("DEBE SELECCIONAR UN DISPOSITIVO", "Alerta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                int selectedRest = this.comboBox1.SelectedIndex;
                int selectedDevice = this.comboBox2.SelectedIndex;
                string deviceType = this.restInfo.CustomRestInfo[selectedRest].Device[selectedDevice].Type;
                AppInstaller.SetConfig(this.assemblyPath,this.restInfo.CustomRestInfo[selectedRest].FolderName,this.restInfo.CustomRestInfo[selectedRest].Device[selectedDevice].Value, deviceType);
                AppInstaller.EncryptAppSettings(this.assemblyPath);
                if (deviceType.ToLower().Contains("pc_gerentes") || deviceType.ToLower().Contains("soft_server"))
                    AppInstaller.GrantQueryPermissions();
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
            if (this.comboBox2.SelectedItem.ToString()=="  Otro")
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
            this.AppConfig_Load(sender, e);
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
