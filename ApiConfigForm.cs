using DatabaseConfigDemo.Models;
using DatabaseConfigDemo.Services;

namespace DatabaseConfigDemo;

public partial class ApiConfigForm : Form
{
    private TextBox zhimaApiKeyTextBox = null!;
    private TextBox zhimaSecretKeyTextBox = null!;
    private TextBox zhimaEndpointTextBox = null!;
    private readonly ApiConfig config;
    private readonly ExchangeApiService _exchangeApiService;

    public ApiConfigForm()
    {
        InitializeComponent();
        config = ApiConfig.Load();
        _exchangeApiService = new ExchangeApiService(new Logger());
        LoadConfig();
    }

    private void InitializeComponent()
    {
        this.Text = "API 设置";
        this.Size = new Size(500, 250);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        TableLayoutPanel mainLayout = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 1,
            RowCount = 2
        };

        // 芝麻 API 设置组
        GroupBox zhimaGroup = new()
        {
            Text = "芝麻期货 API 设置",
            Dock = DockStyle.Top,
            Height = 150
        };

        TableLayoutPanel zhimaLayout = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            ColumnCount = 2,
            RowCount = 3
        };

        zhimaApiKeyTextBox = AddLabelAndTextBox(zhimaLayout, "API Key:", 0);
        zhimaSecretKeyTextBox = AddLabelAndTextBox(zhimaLayout, "Secret Key:", 1);
        zhimaEndpointTextBox = AddLabelAndTextBox(zhimaLayout, "自定义接入点:", 2);

        zhimaGroup.Controls.Add(zhimaLayout);

        // 按钮面板
        FlowLayoutPanel buttonPanel = new()
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40
        };

        Button cancelButton = new()
        {
            Text = "取消",
            Width = 80
        };
        cancelButton.Click += (s, e) => this.Close();

        Button saveButton = new()
        {
            Text = "保存",
            Width = 80
        };
        saveButton.Click += SaveButton_Click;

        Button testButton = new()
        {
            Text = "测试连接",
            Width = 80
        };
        testButton.Click += TestZhimaConnectionButton_Click;

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(testButton);

        mainLayout.Controls.Add(zhimaGroup);
        mainLayout.Controls.Add(buttonPanel);

        this.Controls.Add(mainLayout);
    }

    private TextBox AddLabelAndTextBox(TableLayoutPanel panel, string labelText, int row)
    {
        Label label = new()
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        TextBox textBox = new()
        {
            Dock = DockStyle.Fill,
            Width = 300
        };

        panel.Controls.Add(label, 0, row);
        panel.Controls.Add(textBox, 1, row);

        return textBox;
    }

    private void LoadConfig()
    {
        zhimaApiKeyTextBox.Text = config.Zhima.ApiKey;
        zhimaSecretKeyTextBox.Text = config.Zhima.ApiSecret;
        zhimaEndpointTextBox.Text = config.Zhima.Endpoint;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            config.Zhima.ApiKey = zhimaApiKeyTextBox.Text;
            config.Zhima.ApiSecret = zhimaSecretKeyTextBox.Text;
            config.Zhima.Endpoint = zhimaEndpointTextBox.Text;

            config.Save();
            MessageBox.Show("API 配置已保存", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void TestZhimaConnectionButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var config = new ZhimaConfig
            {
                ApiKey = zhimaApiKeyTextBox.Text,
                ApiSecret = zhimaSecretKeyTextBox.Text,
                Endpoint = zhimaEndpointTextBox.Text
            };
            
            var result = await _exchangeApiService.TestZhimaConnectionAsync(config);
            MessageBox.Show(result, "测试结果", MessageBoxButtons.OK, 
                result.Contains("成功") ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"测试失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
} 