using System;
using System.Data;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using System.Text;
using DatabaseConfigDemo.Models;
using DatabaseConfigDemo.Services;
using System.Collections.Concurrent;
using Io.Gate.GateApi.Api;
using Io.Gate.GateApi.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace DatabaseConfigDemo;

public partial class MainForm : Form
{
    private ComboBox accountComboBox = null!;
    private DataGridView activeOrdersGrid = null!;
    private DataGridView completedOrdersGrid = null!;
    private readonly IDbService _dbService;
    private readonly OrderService _orderService;
    private readonly DatabaseConfigDemo.Services.ILogger _logger;
    private readonly MarketDataService _marketDataService;

    // 添加输入框字段
    private TextBox quantityTextBox = null!;
    private TextBox totalValueTextBox = null!;
    private TextBox marginTextBox = null!;
    private TextBox entryPriceTextBox = null!;
    private TextBox faceValueTextBox = null!;
    private TextBox leverageTextBox = null!;
    private TextBox stopLossAmountTextBox = null!;
    private TextBox stopLossPercentageTextBox = null!;
    private TextBox stopLossPriceTextBox = null!;
    private TextBox takeProfitPriceTextBox = null!;
    private TextBox takeProfitDrawdownTextBox = null!;
    private TextBox orderPreviewBox = null!;
    private ComboBox directionComboBox = null!;

    // 添加机会区的ListView字段
    private ListView customListView = null!;
    private ListView trendLongListView = null!;
    private ListView trendShortListView = null!;

    // 添加信息区的字段
    private TextBox logTextBox = null!;

    public MainForm()
    {
        try
        {
            InitializeComponent();

            var config = DbConfig.Load();
            _logger = new Logger();
            _dbService = new MySqlDbService(config.GetConnectionString(), _logger);
            _orderService = new OrderService(_dbService, _logger);
            
            (_logger as Logger)?.SetLogTextBox(logTextBox);
            _logger.Log("正在初始化交易系统...");
            
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            var marketDataLogger = loggerFactory.CreateLogger<MarketDataService>();
            _marketDataService = new MarketDataService("https://api.gateio.ws/api/v4", marketDataLogger);
            _marketDataService.TickerUpdated += MarketDataService_TickerUpdated;
            
            _ = Task.Run(async () => 
            {
                try 
                {
                    await InitializeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError("初始化失败", ex);
                }
            });
            
            _logger.Log("交易系统初始化完成");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"系统初始化失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }

    private void InitializeComponent()
    {
        try
        {
            // 基本窗体设置
            this.Text = "交易系统";
            this.Size = new System.Drawing.Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);

            // 创建一个容器面板来放置主布局
            Panel mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(3)
            };

            // 创建主工作区面板
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(3)
            };

            // 设置行高比例
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));  // 第一行 35%
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));  // 第二行 35%
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));  // 第三行 30%

            // 第一行：订单区和下单区 (50%-50%)
            var topSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5,
                Panel1MinSize = 100,  // 添加最小尺寸限制
                Panel2MinSize = 100
            };
            mainLayout.Controls.Add(topSplit, 0, 0);

            // 左侧订单区：使用 TabControl
            var orderGroup = new GroupBox 
            { 
                Text = "订单区", 
                Dock = DockStyle.Fill, 
                Padding = new Padding(5),
                Margin = new Padding(3)
            };

            var orderTabControl = new TabControl 
            { 
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 15, 3, 3)  // 增加顶部边距，避免与 GroupBox 标题重叠
            };
            
            // 创建标签页
            var activeOrdersTab = new TabPage { Text = "持仓订单" };
            var completedOrdersTab = new TabPage { Text = "已平仓订单" };

            // 添加订单表格到标签页
            activeOrdersGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true
            };

            // 添加列定义
            activeOrdersGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "OrderId", HeaderText = "订单ID", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "Contract", HeaderText = "合约", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "Direction", HeaderText = "方向", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "数量", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "EntryPrice", HeaderText = "开仓价", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "CurrentStopLoss", HeaderText = "当前止损", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "HighestPrice", HeaderText = "最高价", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "MaxProfit", HeaderText = "最大盈利", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态", Width = 80 }
            });

            completedOrdersGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true
            };

            // 添加列定义
            completedOrdersGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "OrderId", HeaderText = "订单ID", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "Contract", HeaderText = "合约", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "Direction", HeaderText = "方向", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "数量", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "EntryPrice", HeaderText = "开仓价", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "ClosePrice", HeaderText = "平仓价", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "RealizedProfit", HeaderText = "实现盈亏", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "CloseTime", HeaderText = "平仓时间", Width = 120 },
                new DataGridViewTextBoxColumn { Name = "CloseType", HeaderText = "平仓类型", Width = 100 }
            });

            activeOrdersTab.Controls.Add(activeOrdersGrid);
            completedOrdersTab.Controls.Add(completedOrdersGrid);
            orderTabControl.TabPages.Add(activeOrdersTab);
            orderTabControl.TabPages.Add(completedOrdersTab);

            orderGroup.Controls.Add(orderTabControl);
            topSplit.Panel1.Controls.Add(orderGroup);

            // 右侧下单区：上下分隔
            var placementSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 5,
                Panel1MinSize = 100,
                Panel2MinSize = 100
            };

            // 上部分：并排放置三个区域
            var upperPanel = new Panel { Dock = DockStyle.Fill };
            var upperLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,  // 三列布局
                RowCount = 1,     // 一行
                Padding = new Padding(3)
            };

            // 设置列宽比例为1:1:1
            upperLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            upperLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            upperLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            // 添加三个区域
            var positionGroup = CreatePositionGroup();  // 数量设置区
            var stopLossGroup = CreateStopLossGroup();  // 止损区
            var takeProfitGroup = CreateTakeProfitGroup(); // 止盈区

            upperLayout.Controls.Add(positionGroup, 0, 0);
            upperLayout.Controls.Add(stopLossGroup, 1, 0);
            upperLayout.Controls.Add(takeProfitGroup, 2, 0);

            upperPanel.Controls.Add(upperLayout);
            placementSplit.Panel1.Controls.Add(upperPanel);

            // 下部分：下单信息和按钮
            var lowerPanel = new Panel { Dock = DockStyle.Fill };
            var orderPreviewGroup = CreateOrderPreviewGroup();
            lowerPanel.Controls.Add(orderPreviewGroup);
            placementSplit.Panel2.Controls.Add(lowerPanel);

            // 设置分隔比例为 7:3
            placementSplit.SplitterDistance = (int)(placementSplit.Height * 0.7);

            var placementGroup = new GroupBox 
            { 
                Text = "下单区", 
                Dock = DockStyle.Fill, 
                Padding = new Padding(5),
                Margin = new Padding(3)
            };
            placementGroup.Controls.Add(placementSplit);
            topSplit.Panel2.Controls.Add(placementGroup);

            // 第二行：机会区和图表区（左右排列）
            var middleSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5,
                Panel1MinSize = 100,  // 添加最小尺寸限制
                Panel2MinSize = 100
            };
            mainLayout.Controls.Add(middleSplit, 0, 1);

            // 左侧机会区（使用 TabControl）
            var opportunityTabControl = new TabControl { Dock = DockStyle.Fill };
            
            // 创建三个标签页
            var customTab = new TabPage { Text = "自选区" };
            var trendLongTab = new TabPage { Text = "趋势多" };
            var trendShortTab = new TabPage { Text = "趋势空" };

            // 创建三个ListView
            customListView = CreateContractListView();
            trendLongListView = CreateContractListView();
            trendShortListView = CreateContractListView();

            // 在创建完 ListView 后立即添加默认合约
            AddDefaultContracts(customListView);
            customTab.Controls.Add(customListView);

            trendLongListView = CreateContractListView();
            AddTrendLongContracts(trendLongListView);  // 添加趋势多合约
            trendLongTab.Controls.Add(trendLongListView);

            trendShortListView = CreateContractListView();
            AddTrendShortContracts(trendShortListView);  // 添加趋势空合约
            trendShortTab.Controls.Add(trendShortListView);

            // 将标签页添加到TabControl
            opportunityTabControl.TabPages.Add(customTab);
            opportunityTabControl.TabPages.Add(trendLongTab);
            opportunityTabControl.TabPages.Add(trendShortTab);

            var opportunityGroup = new GroupBox { Text = "机会区", Dock = DockStyle.Fill, Padding = new Padding(5) };
            opportunityGroup.Controls.Add(opportunityTabControl);
            middleSplit.Panel1.Controls.Add(opportunityGroup);

            // 右侧图表区
            var chartGroup = new GroupBox { Text = "图表区", Dock = DockStyle.Fill, Padding = new Padding(5) };
            // 这里可以添加图表相关的控件
            middleSplit.Panel2.Controls.Add(chartGroup);

            // 第三行：输出信息区
            var logGroup = new GroupBox { Text = "输出信息区", Dock = DockStyle.Fill, Padding = new Padding(5) };
            logTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 9)
            };
            logGroup.Controls.Add(logTextBox);
            mainLayout.Controls.Add(logGroup, 0, 2);

            // 将主布局添加到容器面板
            mainPanel.Controls.Add(mainLayout);

            // 修改顶部面板部分的代码
            // 创建顶部面板
            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,  // 保持在顶部
                Height = 55,  // 设置固定高度以容纳两行
                Padding = new Padding(3, 0, 3, 0)  // 只保留左右边距
            };

            // 创建菜单栏
            MenuStrip menuStrip = new MenuStrip();
            ToolStripMenuItem settingsMenu = new ToolStripMenuItem("设置");
            ToolStripMenuItem dbSettingsItem = new ToolStripMenuItem("数据库设置");
            ToolStripMenuItem apiSettingsItem = new ToolStripMenuItem("API 设置");

            dbSettingsItem.Click += DbSettingsItem_Click;
            apiSettingsItem.Click += ApiSettingsItem_Click;

            settingsMenu.DropDownItems.Add(dbSettingsItem);
            settingsMenu.DropDownItems.Add(apiSettingsItem);
            menuStrip.Items.Add(settingsMenu);

            // 创建账户选择面板
            Panel accountPanel = new Panel
            {
                Dock = DockStyle.Bottom,  // 停靠在底部
                Height = 25,  // 设置固定高度
                Padding = new Padding(3, 0, 3, 0)  // 只保留左右边距
            };

            accountComboBox = new ComboBox
            {
                Dock = DockStyle.Left,  // 停靠在左侧
                Width = 200,  // 设置固定宽度
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            accountComboBox.SelectedIndexChanged += AccountComboBox_SelectedIndexChanged;

            // 调整控件添加顺序
            accountPanel.Controls.Add(accountComboBox);
            topPanel.Controls.Add(accountPanel);
            topPanel.Controls.Add(menuStrip);

            // 调整控件添加顺序
            this.Controls.Clear();
            this.Controls.Add(mainPanel);    // 添加主面板
            this.Controls.Add(topPanel);     // 添加顶部面板

            // 设置菜单栏为窗体的主菜单
            this.MainMenuStrip = menuStrip;

            // 调整主面板的 Padding
            mainPanel.Padding = new Padding(3);  // 恢复正常边距

            // 修改分隔比例设置方式
            this.Shown += (s, e) =>  // 改用 Shown 事件而不是 Load 事件
            {
                try
                {
                    // 确保控件已经完成布局
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // 设置第一行的分隔比例（订单区和下单区）
                            if (topSplit.Width > topSplit.Panel1MinSize + topSplit.Panel2MinSize)
                            {
                                topSplit.SplitterDistance = topSplit.Width / 2;
                            }

                            // 设置第二行的分隔比例（机会区和图表区）
                            if (middleSplit.Width > middleSplit.Panel1MinSize + middleSplit.Panel2MinSize)
                            {
                                middleSplit.SplitterDistance = (int)(middleSplit.Width * 0.3);
                            }

                            // 设置下单区的上下分隔比例
                            if (placementSplit.Height > placementSplit.Panel1MinSize + placementSplit.Panel2MinSize)
                            {
                                placementSplit.SplitterDistance = (int)(placementSplit.Height * 0.7);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"设置分隔比例失败: {ex.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"初始化分隔比例失败: {ex.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show($"界面初始化失败: {ex.Message}\n{ex.StackTrace}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }

    private void InitializeMenu()
    {
        if (this.MainMenuStrip != null)
        {
            ToolStripMenuItem settingsMenu = new ToolStripMenuItem("设置");
            
            ToolStripMenuItem dbSettingsItem = new ToolStripMenuItem("数据库设置");
            dbSettingsItem.Click += DbSettingsItem_Click;
            
            ToolStripMenuItem apiSettingsItem = new ToolStripMenuItem("API 设置");
            apiSettingsItem.Click += ApiSettingsItem_Click;
            
            settingsMenu.DropDownItems.Add(dbSettingsItem);
            settingsMenu.DropDownItems.Add(apiSettingsItem);
            this.MainMenuStrip.Items.Add(settingsMenu);
        }
    }

    private void InitializeControls()
    {
        // 只保留账户选择下拉框相关代码
        accountComboBox.SelectedIndexChanged += AccountComboBox_SelectedIndexChanged;
        
        // 删除所有机会区相关代码
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var accounts = await _dbService.GetActiveAccountsAsync();
            if (!IsDisposed)
            {
                await this.InvokeAsync(() =>
                {
                    if (!IsDisposed)
                    {
                        accountComboBox.Items.Clear();
                        accountComboBox.Items.AddRange(accounts.ToArray());
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("加载账户失败", ex);
            if (!IsDisposed)
            {
                await this.InvokeAsync(() =>
                {
                    MessageBox.Show($"加载账户失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        }
    }

    private void AccountComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (accountComboBox.SelectedItem is AccountItem selectedAccount)
        {
            _logger.Log($"选择账户：{selectedAccount.AccountName}");
            _ = RefreshDataAsync(selectedAccount.AccountId);  // 使用弃元运算符处理异步调用
        }
    }

    private async Task RefreshDataAsync(string accountId)
    {
        if (long.TryParse(accountId, out long accId))
        {
            try
            {
                _logger.Log($"正在刷新账户 {accountId} 的订单数据...");
                
                // 获取持仓订单（status = 'open'）
                var activeOrders = await _dbService.GetActiveOrdersAsync(accId);
                await this.InvokeAsync(() =>
                {
                    if (!IsDisposed)
                    {
                        activeOrdersGrid.Rows.Clear();
                        foreach (var order in activeOrders)
                        {
                            activeOrdersGrid.Rows.Add(
                                order.OrderId,
                                order.Contract,
                                order.Direction,
                                order.Quantity,
                                order.EntryPrice,
                                order.CurrentStopLoss,
                                order.HighestPrice,
                                order.MaxFloatingProfit,
                                order.Status
                            );
                        }
                    }
                });

                // 获取已平仓订单（status = 'closed'）
                var completedOrders = await _dbService.GetCompletedOrdersAsync(accId);
                await this.InvokeAsync(() =>
                {
                    if (!IsDisposed)
                    {
                        completedOrdersGrid.Rows.Clear();
                        foreach (var order in completedOrders)
                        {
                            completedOrdersGrid.Rows.Add(
                                order.OrderId,
                                order.Contract,
                                order.Direction,
                                order.Quantity,
                                order.EntryPrice,
                                order.ClosePrice,
                                order.RealizedProfit,
                                order.CloseTime,
                                order.CloseType
                            );
                        }
                    }
                });

                _logger.Log($"账户 {accountId} 的订单数据刷新完成");
            }
            catch (Exception ex)
            {
                _logger.LogError("刷新数据失败", ex);
                MessageBox.Show($"刷新数据失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void DbSettingsItem_Click(object? sender, EventArgs e)
    {
        using (var dbConfigForm = new DbConfigForm())
        {
            dbConfigForm.ShowDialog();
        }
    }

    private void ApiSettingsItem_Click(object? sender, EventArgs e)
    {
        using var apiConfigForm = new ApiConfigForm();
        apiConfigForm.ShowDialog();
    }

    private TextBox AddLabelAndTextBox(TableLayoutPanel panel, string labelText, int row, int col)
    {
        Label label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3)
        };

        TextBox textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(3)
        };

        panel.Controls.Add(label, col, row);
        panel.Controls.Add(textBox, col + 1, row);

        return textBox;
    }

    // 在添加控件时修改开仓方向的添加方式
    private ComboBox AddDirectionComboBox(TableLayoutPanel panel, string labelText, int row, int col)
    {
        Label label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        directionComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        directionComboBox.Items.AddRange(new string[] { "buy", "sell" });
        directionComboBox.SelectedIndex = 0;
        directionComboBox.SelectedIndexChanged += DirectionComboBox_SelectedIndexChanged;

        panel.Controls.Add(label, col, row);
        panel.Controls.Add(directionComboBox, col + 1, row);

        return directionComboBox;
    }

    // 添加其他输入框的事件处理方法
    private void MarginTextBox_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)Keys.Enter)
        {
            e.Handled = true;
            if (decimal.TryParse(marginTextBox.Text, out decimal margin) &&
                int.TryParse(leverageTextBox.Text, out int leverage))
            {
                decimal totalValue = margin * leverage;
                if (decimal.TryParse(faceValueTextBox.Text, out decimal faceValue))
                {
                    int quantity = TradingCalculator.CalculateQuantity(totalValue, faceValue, leverage);
                    quantityTextBox.Text = quantity.ToString();
                }
                totalValueTextBox.Text = totalValue.ToString("F2");
                UpdateOrderPreview();
            }
        }
    }

    private void StopLossPriceTextBox_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)Keys.Enter)
        {
            e.Handled = true;
            if (decimal.TryParse(stopLossPriceTextBox.Text, out decimal stopLossPrice) &&
                decimal.TryParse(entryPriceTextBox.Text, out decimal entryPrice) &&
                int.TryParse(quantityTextBox.Text, out int quantity) &&
                decimal.TryParse(faceValueTextBox.Text, out decimal faceValue))
            {
                string direction = directionComboBox.SelectedItem?.ToString() ?? "buy";
                decimal stopLossAmount = TradingCalculator.CalculateStopLossAmount(
                    quantity, entryPrice, stopLossPrice, faceValue, direction);
                
                if (decimal.TryParse(marginTextBox.Text, out decimal margin))
                {
                    decimal stopLossPercentage = TradingCalculator.CalculateStopLossPercentage(
                        stopLossAmount, margin);
                    stopLossPercentageTextBox.Text = stopLossPercentage.ToString("F2");
                }
                
                stopLossAmountTextBox.Text = stopLossAmount.ToString("F2");
                UpdateOrderPreview();
            }
        }
    }

    private void StopLossPercentageTextBox_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)Keys.Enter)
        {
            e.Handled = true;
            if (decimal.TryParse(stopLossPercentageTextBox.Text, out decimal stopLossPercentage) &&
                decimal.TryParse(entryPriceTextBox.Text, out decimal entryPrice) &&
                int.TryParse(quantityTextBox.Text, out int quantity) &&
                decimal.TryParse(faceValueTextBox.Text, out decimal faceValue) &&
                decimal.TryParse(marginTextBox.Text, out decimal margin))
            {
                string direction = directionComboBox.SelectedItem?.ToString() ?? "buy";
                decimal stopLossPrice = TradingCalculator.CalculateStopLossPrice(
                    entryPrice, stopLossPercentage, margin, quantity, faceValue, direction);
                
                decimal stopLossAmount = TradingCalculator.CalculateStopLossAmount(
                    quantity, entryPrice, stopLossPrice, faceValue, direction);
                
                stopLossPriceTextBox.Text = stopLossPrice.ToString("F4");
                stopLossAmountTextBox.Text = stopLossAmount.ToString("F2");
                UpdateOrderPreview();
            }
        }
    }

    private void DirectionComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // 当方向改变时，重新计算止损相关数值
        if (decimal.TryParse(stopLossPriceTextBox.Text, out _))
        {
            var e2 = new KeyPressEventArgs((char)Keys.Enter);
            StopLossPriceTextBox_KeyPress(stopLossPriceTextBox, e2);
        }
    }

    // 修改 NumberOnlyTextBox_KeyPress 方法签名
    private void NumberOnlyTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.')
        {
            e.Handled = true;
        }

        // 只允许一个小数点
        if (e.KeyChar == '.' && textBox.Text.Contains('.'))
        {
            e.Handled = true;
        }
    }

    // 在 InitializeComponent 中为相关输入框添加验证
    private void InitializeInputValidation()
    {
        TextBox[] numberOnlyTextBoxes = {
            quantityTextBox,
            totalValueTextBox,
            marginTextBox,
            entryPriceTextBox,
            faceValueTextBox,
            leverageTextBox,
            stopLossAmountTextBox,
            stopLossPercentageTextBox,
            stopLossPriceTextBox,
            takeProfitPriceTextBox,
            takeProfitDrawdownTextBox
        };

        foreach (var textBox in numberOnlyTextBoxes)
        {
            textBox.KeyPress += NumberOnlyTextBox_KeyPress;
        }
    }

    // 修改 UpdateOrderPreview 方法，添加更多信息
    private void UpdateOrderPreview()
    {
        try
        {
            StringBuilder preview = new StringBuilder();
            preview.AppendLine("=== 订单信息 ===");
            preview.AppendLine($"下单手数: {quantityTextBox.Text} 手");
            preview.AppendLine($"下单市值: {totalValueTextBox.Text} 元");
            preview.AppendLine($"保证金: {marginTextBox.Text} 元");
            preview.AppendLine($"开仓价格: {entryPriceTextBox.Text}");
            preview.AppendLine($"开仓方向: {directionComboBox.SelectedItem?.ToString()?.ToUpper()}");
            preview.AppendLine($"面值: {faceValueTextBox.Text}");
            preview.AppendLine($"杠杆倍数: {leverageTextBox.Text}倍");
            preview.AppendLine("\n=== 止损信息 ===");
            preview.AppendLine($"止损价格: {stopLossPriceTextBox.Text}");
            preview.AppendLine($"止损金额: {stopLossAmountTextBox.Text} 元");
            preview.AppendLine($"止损比例: {stopLossPercentageTextBox.Text}%");
            
            preview.AppendLine("\n=== 止盈信息 ===");
            if (!string.IsNullOrEmpty(takeProfitPriceTextBox.Text))
                preview.AppendLine($"止盈价格: {takeProfitPriceTextBox.Text}");
            
            if (!string.IsNullOrEmpty(takeProfitDrawdownTextBox.Text))
                preview.AppendLine($"止盈回撤: {takeProfitDrawdownTextBox.Text}%");

            orderPreviewBox.Text = preview.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError("更新订单预览失败", ex);
        }
    }

    private ListView CreateContractListView()
    {
        var listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };

        // 调整列宽
        listView.Columns.Add("合约", 120);
        listView.Columns.Add("最新价", 120);
        listView.Columns.Add("涨跌幅", 100);
        listView.Columns.Add("24H量", 150);

        return listView;
    }

    private void AddDefaultContracts(ListView listView)
    {
        var defaultContracts = new[]
        {
            "BTC/USDT",
            "ETH/USDT",
            "SOL/USDT",
            "XRP/USDT"
        };

        foreach (var contract in defaultContracts)
        {
            var item = new ListViewItem(contract);
            item.SubItems.Add("--");  // 最新价
            item.SubItems.Add("--");  // 涨跌幅
            item.SubItems.Add("--");  // 24H量
            listView.Items.Add(item);
        }
    }

    private void AddTrendLongContracts(ListView listView)
    {
        var trendLongContracts = new[]
        {
            "BTC/USDT",
            "ETH/USDT",
            "SOL/USDT"
        };

        foreach (var contract in trendLongContracts)
        {
            var item = new ListViewItem(contract);
            item.SubItems.Add("--");  // 最新价
            item.SubItems.Add("--");  // 涨跌幅
            item.SubItems.Add("--");  // 24H量
            listView.Items.Add(item);
        }
    }

    private void AddTrendShortContracts(ListView listView)
    {
        var trendShortContracts = new[]
        {
            "XRP/USDT"
        };

        foreach (var contract in trendShortContracts)
        {
            var item = new ListViewItem(contract);
            item.SubItems.Add("--");  // 最新价
            item.SubItems.Add("--");  // 涨跌幅
            item.SubItems.Add("--");  // 24H量
            listView.Items.Add(item);
        }
    }

    // 添加行情更新事件处理方法
    private async void MarketDataService_TickerUpdated(object? sender, Dictionary<string, FuturesTicker> tickers)
    {
        if (IsDisposed) return;

        await this.InvokeAsync(() =>
        {
            if (IsDisposed) return;

            UpdateListViewTickers(customListView, tickers);
            UpdateListViewTickers(trendLongListView, tickers);
            UpdateListViewTickers(trendShortListView, tickers);
        });
    }

    private void UpdateListViewTickers(ListView listView, Dictionary<string, FuturesTicker> tickers)
    {
        foreach (ListViewItem item in listView.Items)
        {
            string symbol = item.Text;
            if (tickers.TryGetValue(symbol, out var ticker))
            {
                item.SubItems[1].Text = ticker.Last;  // 最新价
                item.SubItems[2].Text = $"{ticker.ChangePercentage}%";  // 涨跌幅
                item.SubItems[3].Text = ticker.Volume24h;  // 24H成交量

                // 根据涨跌幅设置颜色
                if (decimal.TryParse(ticker.ChangePercentage, out decimal change))
                {
                    item.ForeColor = change switch
                    {
                        > 0 => Color.Red,
                        < 0 => Color.Green,
                        _ => Color.Black
                    };
                }
            }
        }
    }

    // 在窗体关闭时停止行情服务
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        _marketDataService.Stop();
    }

    private GroupBox CreatePositionGroup()
    {
        var positionGroup = new GroupBox
        {
            Text = "建议下单头寸",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            Margin = new Padding(3)
        };

        var positionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4,
            Padding = new Padding(3),
            AutoSize = true
        };

        // 设置列宽和行高
        for (int i = 0; i < 4; i++)
        {
            positionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            positionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }

        // 添加头寸区域的控件
        quantityTextBox = AddLabelAndTextBox(positionLayout, "下单手数:", 0, 0);
        totalValueTextBox = AddLabelAndTextBox(positionLayout, "下单市值:", 1, 0);
        marginTextBox = AddLabelAndTextBox(positionLayout, "下单保证金:", 2, 0);
        entryPriceTextBox = AddLabelAndTextBox(positionLayout, "开仓价格:", 0, 2);
        faceValueTextBox = AddLabelAndTextBox(positionLayout, "面值:", 2, 2);
        leverageTextBox = AddLabelAndTextBox(positionLayout, "杠杆倍数:", 3, 0);
        directionComboBox = AddDirectionComboBox(positionLayout, "开仓方向:", 1, 2);

        positionGroup.Controls.Add(positionLayout);
        return positionGroup;
    }

    private GroupBox CreateStopLossGroup()
    {
        var stopLossGroup = new GroupBox
        {
            Text = "止损策略",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            Margin = new Padding(3)
        };

        var stopLossLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(3),
            AutoSize = true
        };

        // 设置列宽和行高
        for (int i = 0; i < 4; i++)
        {
            stopLossLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }
        for (int i = 0; i < 2; i++)
        {
            stopLossLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }

        stopLossAmountTextBox = AddLabelAndTextBox(stopLossLayout, "止损金额:", 0, 0);
        stopLossPercentageTextBox = AddLabelAndTextBox(stopLossLayout, "止损比例:", 0, 2);
        stopLossPriceTextBox = AddLabelAndTextBox(stopLossLayout, "止损价格:", 1, 0);

        stopLossGroup.Controls.Add(stopLossLayout);
        return stopLossGroup;
    }

    private GroupBox CreateTakeProfitGroup()
    {
        var takeProfitGroup = new GroupBox
        {
            Text = "止盈策略",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            Margin = new Padding(3)
        };

        var takeProfitLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(3),
            AutoSize = true
        };

        // 设置列宽和行高
        for (int i = 0; i < 4; i++)
        {
            takeProfitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }
        for (int i = 0; i < 2; i++)
        {
            takeProfitLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }

        takeProfitPriceTextBox = AddLabelAndTextBox(takeProfitLayout, "指定价格止盈:", 0, 0);
        takeProfitDrawdownTextBox = AddLabelAndTextBox(takeProfitLayout, "回撤比例止盈:", 1, 0);

        takeProfitGroup.Controls.Add(takeProfitLayout);
        return takeProfitGroup;
    }

    private GroupBox CreateOrderPreviewGroup()
    {
        var orderPreviewGroup = new GroupBox
        {
            Text = "订单预览",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            Margin = new Padding(3)
        };

        var orderPreviewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,  // 改为2列
            RowCount = 1,     // 改为1行
            Padding = new Padding(3)
        };

        // 设置列宽比例 8:2
        orderPreviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80));
        orderPreviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        // 订单预览文本框
        orderPreviewBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9)
        };

        // 下单按钮
        var submitButton = new Button
        {
            Text = "确认下单",
            Dock = DockStyle.Fill,
            Height = 40,
            Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        orderPreviewLayout.Controls.Add(orderPreviewBox, 0, 0);
        orderPreviewLayout.Controls.Add(submitButton, 1, 0);

        orderPreviewGroup.Controls.Add(orderPreviewLayout);
        return orderPreviewGroup;
    }

    private void AddOrderPanelContent(Panel panel)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(3)
        };

        // 设置行高比例
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        // 添加各个组
        layout.Controls.Add(CreatePositionGroup(), 0, 0);
        layout.Controls.Add(CreateStopLossGroup(), 0, 1);
        layout.Controls.Add(CreateTakeProfitGroup(), 0, 2);
        layout.Controls.Add(CreateOrderPreviewGroup(), 0, 3);

        panel.Controls.Add(layout);
    }

    private void AddOrderPlacementPanelContent(Panel panel)
    {
        // 创建活动订单和已完成订单的表格
        activeOrdersGrid = new DataGridView
        {
            Dock = DockStyle.Top,
            Height = panel.Height / 2,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            ReadOnly = true
        };

        completedOrdersGrid = new DataGridView
        {
            Dock = DockStyle.Bottom,
            Height = panel.Height / 2,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            ReadOnly = true
        };

        panel.Controls.Add(activeOrdersGrid);
        panel.Controls.Add(completedOrdersGrid);
    }

    private void AddListPanelContent(Panel panel)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(3)
        };

        // 设置行高比例
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

        // 创建并添加三个ListView
        customListView = CreateContractListView();
        trendLongListView = CreateContractListView();
        trendShortListView = CreateContractListView();

        // 添加默认合约
        AddDefaultContracts(customListView);
        AddTrendLongContracts(trendLongListView);
        AddTrendShortContracts(trendShortListView);

        // 创建分组框
        var customGroup = new GroupBox { Text = "自选合约", Dock = DockStyle.Fill };
        var trendLongGroup = new GroupBox { Text = "趋势多", Dock = DockStyle.Fill };
        var trendShortGroup = new GroupBox { Text = "趋势空", Dock = DockStyle.Fill };

        customGroup.Controls.Add(customListView);
        trendLongGroup.Controls.Add(trendLongListView);
        trendShortGroup.Controls.Add(trendShortListView);

        layout.Controls.Add(customGroup, 0, 0);
        layout.Controls.Add(trendLongGroup, 0, 1);
        layout.Controls.Add(trendShortGroup, 0, 2);

        panel.Controls.Add(layout);
    }

    private void AddChartPanelContent(Panel panel)
    {
        // 添加日志文本框
        logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Consolas", 9)
        };

        panel.Controls.Add(logTextBox);
    }

    // 添加新的日志面板内容方法
    private void AddLogPanelContent(Panel panel)
    {
        logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Consolas", 9)
        };

        var logGroup = new GroupBox
        {
            Text = "系统日志",
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        logGroup.Controls.Add(logTextBox);
        panel.Controls.Add(logGroup);
    }

    // 修改异步初始化方法
    private async Task InitializeAsync()
    {
        try
        {
            await LoadAccountsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError("初始化失败", ex);
        }
    }
}

public class AccountItem
{
    public string AccountId { get; set; } = "";
    public string AccountName { get; set; } = "";

    public override string ToString()
    {
        return AccountName;
    }
}

// 修改扩展方法
public static class ControlExtensions
{
    public static async Task InvokeAsync(this Control control, Action action)
    {
        if (control.InvokeRequired)
        {
            await Task.Run(() => control.Invoke(action));
        }
        else
        {
            action();
        }
    }
} 