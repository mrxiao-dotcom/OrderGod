using System;
using System.Drawing;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace DatabaseConfigDemo
{
    public partial class DbConfigForm : Form
    {
        private TextBox serverTextBox = null!;
        private TextBox databaseTextBox = null!;
        private TextBox usernameTextBox = null!;
        private TextBox passwordTextBox = null!;
        private Button testButton = null!;
        private Button saveButton = null!;
        private Button cancelButton = null!;
        private DbConfig config;

        public DbConfigForm()
        {
            InitializeComponent();
            InitializeControls();
            config = DbConfig.Load();
            LoadConfig();
        }

        private void InitializeComponent()
        {
            this.Text = "数据库配置";
            this.Size = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
        }

        private void InitializeControls()
        {
            // 服务器地址
            Label serverLabel = new Label
            {
                Text = "服务器地址:",
                Location = new Point(20, 20)
            };
            serverTextBox = new TextBox
            {
                Location = new Point(120, 20),
                Width = 200
            };

            // 数据库名
            Label databaseLabel = new Label
            {
                Text = "数据库名:",
                Location = new Point(20, 60)
            };
            databaseTextBox = new TextBox
            {
                Location = new Point(120, 60),
                Width = 200
            };

            // 用户名
            Label usernameLabel = new Label
            {
                Text = "用户名:",
                Location = new Point(20, 100)
            };
            usernameTextBox = new TextBox
            {
                Location = new Point(120, 100),
                Width = 200
            };

            // 密码
            Label passwordLabel = new Label
            {
                Text = "密码:",
                Location = new Point(20, 140)
            };
            passwordTextBox = new TextBox
            {
                Location = new Point(120, 140),
                Width = 200,
                PasswordChar = '*'
            };

            // 按钮
            testButton = new Button
            {
                Text = "测试连接",
                Location = new Point(40, 200),
                Width = 100
            };
            testButton.Click += TestButton_Click;

            saveButton = new Button
            {
                Text = "保存",
                Location = new Point(160, 200),
                Width = 80
            };
            saveButton.Click += SaveButton_Click;

            cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(260, 200),
                Width = 80
            };
            cancelButton.Click += (s, e) => this.Close();

            // 添加控件到窗体
            this.Controls.AddRange(new Control[] {
                serverLabel, serverTextBox,
                databaseLabel, databaseTextBox,
                usernameLabel, usernameTextBox,
                passwordLabel, passwordTextBox,
                testButton, saveButton, cancelButton
            });
        }

        private void LoadConfig()
        {
            serverTextBox.Text = config.Server;
            databaseTextBox.Text = config.Database;
            usernameTextBox.Text = config.Username;
            passwordTextBox.Text = config.Password;
        }

        private void TestButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var testConfig = new DbConfig
                {
                    Server = serverTextBox.Text,
                    Database = databaseTextBox.Text,
                    Username = usernameTextBox.Text,
                    Password = passwordTextBox.Text
                };

                using (var connection = new MySqlConnection(testConfig.GetConnectionString()))
                {
                    connection.Open();
                    MessageBox.Show("数据库连接成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                config.Server = serverTextBox.Text;
                config.Database = databaseTextBox.Text;
                config.Username = usernameTextBox.Text;
                config.Password = passwordTextBox.Text;

                config.Save();
                MessageBox.Show("配置已保存到文件", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 