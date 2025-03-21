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
using System.Linq;
using Dapper;
using System.Threading;
using GateFuturesTicker = Io.Gate.GateApi.Model.FuturesTicker;  // 为 Gate.io 的 FuturesTicker 创建别名

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
    private TextBox takeProfitAmountTextBox = null!;

    // 添加机会区的ListView字段
    private ListView customListView = null!;
    private ListView trendLongListView = null!;
    private ListView trendShortListView = null!;

    // 添加信息区的字段
    private TextBox logTextBox = null!;

    // 添加开仓品种输入框字段
    private TextBox contractTextBox = null!;

    // 添加一个取消令牌源
    private CancellationTokenSource? _refreshCancellationTokenSource;

    // 在 MainForm 类的字段声明部分添加
    private TableLayoutPanel referenceLayout = null!;

    // 在 MainForm 类中添加右键菜单相关字段
    private ContextMenuStrip orderContextMenu = null!;
    private OrderModel? selectedOrder;

    // 将 accountComboBox 改为 AccountComboBox 的属性
    private ComboBox AccountComboBox
    {
        get { return accountComboBox; }
        set { accountComboBox = value; }
    }

    // 添加字段声明
    private ComboBox directionComboBox = null!;

    public MainForm()
    {
        try
        {
            InitializeComponent();
            InitializeListViewEvents();  // 添加这一行

            var config = DbConfig.Load();
            _logger = new Logger();
            _dbService = new MySqlDbService(config.GetConnectionString(), _logger as DatabaseConfigDemo.Services.ILogger);
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
            
            // 初始化合约订阅
            InitializeContractSubscription();
            
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

            // 修改主布局的行高比例
            mainLayout.RowStyles.Clear();
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));  // 第一行 40%
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));  // 第二行 40%
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));  // 第三行缩小到 20%

            // 第一行：订单区和下单区 (40%-60%)
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

            // 修改持仓订单表格的列定义
            activeOrdersGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Contract", HeaderText = "合约", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "Direction", HeaderText = "方向", Width = 60 },
                new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "数量", Width = 80 },
                new DataGridViewTextBoxColumn { Name = "EntryPrice", HeaderText = "开仓价", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "StopLossPrice", HeaderText = "止损价", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "StopLossAmount", HeaderText = "预计止损额", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "LastPrice", HeaderText = "最新价", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "FloatingPnL", HeaderText = "浮动盈亏", Width = 100 },
                new DataGridViewTextBoxColumn { Name = "OpenTime", HeaderText = "开仓时间", Width = 130 }
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

            // 上部分：并排放置四个区域
            var upperPanel = new Panel { Dock = DockStyle.Fill };
            AddOrderPanelContent(upperPanel);  // 直接调用这个方法来添加四个区域

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
            var logGroup = new GroupBox { 
                Text = "输出信息区", 
                Dock = DockStyle.Fill, 
                Padding = new Padding(5) 
            };
            
            logTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,  // 同时启用垂直和水平滚动条
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 9),
                WordWrap = false  // 禁用自动换行，确保水平滚动条正常工作
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
                                topSplit.SplitterDistance = (int)(topSplit.Width * 0.4);
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

            // 在 InitializeComponent 方法中添加订单表格的右键菜单初始化
            InitializeOrderContextMenu();
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
                        accountComboBox.Items.AddRange(accounts.Cast<object>().ToArray());
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

    // 修改账户选择事件处理方法
    private async void AccountComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (AccountComboBox.SelectedItem is AccountItem selectedAccount)
        {
            try
            {
                // 取消之前的刷新操作
                _refreshCancellationTokenSource?.Cancel();
                _refreshCancellationTokenSource = new CancellationTokenSource();
                var ct = _refreshCancellationTokenSource.Token;

                _logger.Log($"选择账户：{selectedAccount.AccountName}");

                // 使用后台线程刷新数据
                await Task.Run(async () =>
                {
                    try
                    {
                        await RefreshDataAsync(selectedAccount.AccountId, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        // 忽略取消操作导致的异常
                    }
                    catch (Exception ex)
                    {
                        await this.InvokeAsync(() =>
                        {
                            _logger.LogError("刷新数据失败", ex);
                            MessageBox.Show($"刷新数据失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        });
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("切换账户失败", ex);
                MessageBox.Show($"切换账户失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // 修改数据刷新方法
    private async Task RefreshDataAsync(string accountId, CancellationToken ct = default)
    {
        try
        {
            _logger.Log($"正在刷新账户 {accountId} 的数据...");

            // 并行获取所有数据
            var tasks = new[]
            {
                Task.Run(async () =>
                {
                    if (long.TryParse(accountId, out long accId))
                    {
                        return await _dbService.GetAccountDataAsync(accId);
                    }
                    return new AccountData();
                }, ct),

                Task.Run(async () =>
                {
                    if (long.TryParse(accountId, out long accId))
                    {
                        return await _dbService.GetAccountRiskDataAsync(accId);
                    }
                    return new AccountRiskData();
                }, ct),

                Task.Run(async () =>
                {
                    return await _orderService.GetActiveOrdersAsync(accountId);
                }, ct),

                Task.Run(async () =>
                {
                    return await _orderService.GetCompletedOrdersAsync(accountId);
                }, ct)
            };

            await Task.WhenAll(tasks);
            ct.ThrowIfCancellationRequested();

            // 在UI线程更新界面
            await this.InvokeAsync(() =>
            {
                if (!IsDisposed)
                {
                    var accountData = tasks[0].Result;
                    var riskData = tasks[1].Result;
                    var activeOrders = tasks[2].Result;
                    var completedOrders = tasks[3].Result;

                    // 更新参考数据区域
                    UpdateReferenceData(accountData);

                    // 更新订单表格
                    UpdateOrderGridView(activeOrders);
                    UpdateCompletedOrderGridView(completedOrders);  // 确保调用这个方法

                    // 更新其他UI元素
                    UpdateOtherUIElements();

                    // 更新合约订阅
                    InitializeContractSubscription();

                    _logger.Log($"已平仓订单数量：{completedOrders.Count}");
                }
            });

            _logger.Log($"账户 {accountId} 的数据刷新完成");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("刷新数据失败", ex);
            throw;
        }
    }

    private async Task UpdateReferenceDataAsync()
    {
        try
        {
            if (AccountComboBox.SelectedItem is AccountItem selectedAccount)
            {
                var accountData = await _dbService.GetAccountDataAsync(long.Parse(selectedAccount.AccountId));
                var riskData = await _dbService.GetAccountRiskDataAsync(long.Parse(selectedAccount.AccountId));
                
                await this.InvokeAsync(() =>
                {
                    if (!IsDisposed)
                    {
                        UpdateReferenceData(accountData);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("更新参考数据失败", ex);
        }
    }

    private void UpdateReferenceData(AccountData accountData)
    {
        try
        {
            // 更新所有参考数据标签
            UpdateLabelValue("总权益:", $"{accountData.TotalEquity:F2}");
            UpdateLabelValue("初始值:", $"{accountData.InitialValue:F2}");
            UpdateLabelValue("总市值:", $"{accountData.TotalValue:F2}");
            UpdateLabelValue("杠杆率:", $"{accountData.LeverageRatio:F4}");
            UpdateLabelValue("已用保证金:", $"{accountData.UsedMargin:F2}");
            UpdateLabelValue("可用保证金:", $"{accountData.AvailableMargin:F2}");
            UpdateLabelValue("总风险金:", $"{accountData.TotalRisk:F2}");
            UpdateLabelValue("已用风险:", $"{accountData.UsedRisk:F2}");
            UpdateLabelValue("可用风险:", $"{accountData.AvailableRisk:F2}");
            UpdateLabelValue("单笔最大风险:", $"{accountData.MaxSingleRisk:F2}");
            UpdateLabelValue("建议风险金:", $"{accountData.SuggestedRisk:F2}");

            _logger.Log($"更新参考数据成功: " +
                       $"总权益={accountData.TotalEquity:F2}, " +
                       $"初始值={accountData.InitialValue:F2}, " +
                       $"总市值={accountData.TotalValue:F2}, " +
                       $"杠杆率={accountData.LeverageRatio:F4}, " +
                       $"已用保证金={accountData.UsedMargin:F2}, " +
                       $"可用保证金={accountData.AvailableMargin:F2}, " +
                       $"总风险金={accountData.TotalRisk:F2}, " +
                       $"已用风险={accountData.UsedRisk:F2}, " +
                       $"可用风险={accountData.AvailableRisk:F2}, " +
                       $"单笔最大风险={accountData.MaxSingleRisk:F2}, " +
                       $"建议风险金={accountData.SuggestedRisk:F2}");
        }
        catch (Exception ex)
        {
            _logger.LogError("更新参考数据失败", ex);
        }
    }

    private decimal CalculateStopLossAmount(string direction, decimal quantity, decimal entryPrice, decimal stopLossPrice)
    {
        if (direction.ToLower() == "buy")
        {
            return (stopLossPrice - entryPrice) * quantity;
        }
        else // sell
        {
            return (entryPrice - stopLossPrice) * quantity;
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
            TextAlign = ContentAlignment.MiddleRight,  // 文本右对齐
            Margin = new Padding(3, 6, 3, 3)  // 调整上边距以垂直居中
        };

        TextBox textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            Anchor = AnchorStyles.Left | AnchorStyles.Right  // 确保输入框水平填充
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
        try
        {
            // 先获取并验证所有必要的值
            if (!decimal.TryParse(entryPriceTextBox.Text, out decimal currentPrice))
            {
                _logger.Log("当前价格无效");
                return;
            }

            if (!decimal.TryParse(stopLossPercentageTextBox.Text, out decimal stopLossPercentage))
            {
                _logger.Log("止损比例无效");
                return;
            }

            if (!decimal.TryParse(takeProfitDrawdownTextBox.Text, out decimal takeProfitPercentage))
            {
                _logger.Log("止盈比例无效");
                return;
            }

            string direction = directionComboBox.SelectedItem?.ToString() ?? "buy";
            
            // 更新止损价格
            decimal stopLossPrice = CalculateStopLossPrice(currentPrice, stopLossPercentage, direction);
            stopLossPriceTextBox.Text = stopLossPrice.ToString("F4");

            // 更新止盈价格
            decimal takeProfitPrice = CalculateTakeProfitPrice(currentPrice, direction);
            takeProfitPriceTextBox.Text = takeProfitPrice.ToString("F4");

            // 更新止损和止盈金额
            if (decimal.TryParse(totalValueTextBox.Text, out decimal totalValue))
            {
                // 计算止损金额
                decimal stopLossAmount = totalValue * (stopLossPercentage / 100);
                stopLossAmountTextBox.Text = stopLossAmount.ToString("F2");

                // 计算止盈金额
                decimal takeProfitAmount = totalValue * (takeProfitPercentage / 100);
                takeProfitAmountTextBox.Text = takeProfitAmount.ToString("F2");
            }

            _logger.Log($"方向变更 - 方向：{direction}, 当前价：{currentPrice}, " +
                       $"止损比例：{stopLossPercentage}%, 止损价：{stopLossPrice}, " +
                       $"止盈比例：{takeProfitPercentage}%, 止盈价：{takeProfitPrice}");
        }
        catch (Exception ex)
        {
            _logger.LogError("更新止损止盈价格失败", ex);
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
            takeProfitDrawdownTextBox,
            takeProfitAmountTextBox
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
            // 确保必要的数据都存在
            if (string.IsNullOrEmpty(contractTextBox.Text) || 
                directionComboBox.SelectedItem == null || 
                !int.TryParse(quantityTextBox.Text, out int quantity) ||
                !decimal.TryParse(entryPriceTextBox.Text, out decimal entryPrice) ||
                !decimal.TryParse(stopLossPriceTextBox.Text, out decimal stopLoss))
            {
                _logger.Log("订单预览所需数据不完整，跳过更新");
                return;
            }

            // 计算订单相关数值
            var direction = directionComboBox.SelectedItem.ToString();
            var leverage = int.Parse(leverageTextBox.Text);
            var margin = CalculateMargin(entryPrice, quantity, leverage);
            var risk = Math.Abs(entryPrice - stopLoss) * quantity;

            // 更新预览信息
            UpdateLabelValue("预计保证金:", $"{margin:F2}");
            UpdateLabelValue("预计风险:", $"{risk:F2}");

            _logger.Log($"更新订单预览成功: 保证金={margin:F2}, 风险={risk:F2}");
        }
        catch (Exception ex)
        {
            _logger.LogError("更新订单预览失败", ex);
        }
    }

    private decimal CalculateMargin(decimal price, int quantity, int leverage)
    {
        try
        {
            return price * quantity / leverage;
        }
        catch (Exception ex)
        {
            _logger.LogError("计算保证金失败", ex);
            return 0;
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

    // 添加一个辅助方法来标准化合约名称
    private string NormalizeContractName(string contract)
    {
        // 移除 /USDT 和 _USDT 后缀
        return contract.Replace("/USDT", "").Replace("_USDT", "");
    }

    // 修改行情更新事件处理方法
    private void MarketDataService_TickerUpdated(object? sender, Dictionary<string, GateFuturesTicker> tickers)
    {
        try
        {
            // 更新机会区的行情数据
            UpdateListViewTickers(customListView, tickers);
            UpdateListViewTickers(trendLongListView, tickers);
            UpdateListViewTickers(trendShortListView, tickers);

            // 如果选择了账户，则更新订单数据
            if (AccountComboBox.SelectedItem is AccountItem selectedAccount)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var orders = await _dbService.GetActiveOrdersAsync(long.Parse(selectedAccount.AccountId));
                        var updatedOrders = new List<OrderModel>();

                        foreach (var order in orders)
                        {
                            string normalizedContract = NormalizeContractName(order.Contract) + "_USDT";
                            if (!tickers.TryGetValue(normalizedContract, out var ticker))
                                continue;

                            if (!decimal.TryParse(ticker.Last, out decimal lastPrice))
                                continue;
                            
                            order.LastPrice = lastPrice;
                            
                            if (order.Direction == "buy" && 
                                (order.HighestPrice == null || lastPrice > order.HighestPrice))
                            {
                                order.HighestPrice = lastPrice;
                            }

                            order.FloatingPnL = CalculateFloatingPnL(
                                order.Direction, 
                                order.Quantity, 
                                order.EntryPrice, 
                                lastPrice);

                            updatedOrders.Add(order);
                        }

                        if (updatedOrders.Any())
                        {
                            await UpdateOrdersAsync(updatedOrders);
                        }

                        await this.InvokeAsync(() =>
                        {
                            if (!IsDisposed)
                            {
                                UpdateOrderGridView(orders);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("更新行情数据失败", ex);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("处理行情更新失败", ex);
        }
    }

    // 添加更新 ListView 行情数据的方法
    private void UpdateListViewTickers(ListView listView, Dictionary<string, GateFuturesTicker> tickers)
    {
        try
        {
            if (listView.InvokeRequired)
            {
                listView.Invoke(new Action(() => UpdateListViewTickers(listView, tickers)));
                return;
            }

            foreach (ListViewItem item in listView.Items)
            {
                string contract = item.Text;
                string normalizedContract = NormalizeContractName(contract) + "_USDT";

                if (tickers.TryGetValue(normalizedContract, out var ticker))
                {
                    // 更新最新价
                    item.SubItems[1].Text = ticker.Last;

                    // 更新涨跌幅
                    if (decimal.TryParse(ticker.ChangePercentage, out decimal change))
                    {
                        item.SubItems[2].Text = $"{change:F2}%";
                        // 设置涨跌幅的颜色
                        item.SubItems[2].ForeColor = change > 0 ? Color.Red : 
                                                   change < 0 ? Color.Green : 
                                                   Color.Black;
                    }

                    // 更新24H量
                    if (decimal.TryParse(ticker.Volume24h, out decimal volume))  // 使用 Volume24h 替代 Volume
                    {
                        item.SubItems[3].Text = $"{volume:N0}";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("更新行情列表失败", ex);
        }
    }

    // 修改 UpdateOrdersAsync 方法
    private async Task UpdateOrdersAsync(List<OrderModel> orders)
    {
        try
        {
            using var connection = await _dbService.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var order in orders)
                {
                    var sql = @"
                        UPDATE simulation_orders 
                        SET highest_price = @HighestPrice,
                            last_price = @LastPrice,
                            floating_pnl = @FloatingPnL
                        WHERE order_id = @OrderId";

                    await connection.ExecuteAsync(sql, new { 
                        order.OrderId,
                        order.HighestPrice,
                        order.LastPrice,
                        order.FloatingPnL
                    }, transaction);
                }

                transaction.Commit();
                _logger.Log($"成功更新 {orders.Count} 个订单的最新价格和浮动盈亏");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("更新订单数据失败", ex);
            throw;
        }
    }

    // 添加计算止损价格的方法
    private decimal CalculateStopLossPrice(decimal currentPrice, decimal stopLossPercentage, string direction)
    {
        decimal stopLossRatio = stopLossPercentage / 100;
        if (direction.ToLower() == "buy")
        {
            // 做多：当前价格 * (1 - 止损比例)
            return currentPrice * (1 - stopLossRatio);
        }
        else
        {
            // 做空：当前价格 * (1 + 止损比例)
            return currentPrice * (1 + stopLossRatio);
        }
    }

    // 添加计算止盈价格的方法
    private decimal CalculateTakeProfitPrice(decimal currentPrice, string direction)
    {
        if (direction.ToLower() == "buy")
        {
            // 做多：当前价格 * 2
            return currentPrice * 2;
        }
        else
        {
            // 做空：当前价格 * 0
            return 0;
        }
    }

    // 修改止损比例输入框的事件处理
    private void StopLossPercentageTextBox_TextChanged(object? sender, EventArgs e)
    {
        try
        {
            // 先验证止损比例
            if (!decimal.TryParse(stopLossPercentageTextBox.Text, out decimal stopLossPercentage))
            {
                _logger.Log("无效的止损比例");
                return;
            }

            // 更新止损金额和止盈金额
            if (decimal.TryParse(totalValueTextBox.Text, out decimal totalValue) &&
                decimal.TryParse(takeProfitDrawdownTextBox.Text, out decimal takeProfitPercentage))
            {
                // 计算止损金额
                decimal stopLossAmount = totalValue * (stopLossPercentage / 100);
                stopLossAmountTextBox.Text = stopLossAmount.ToString("F2");

                // 计算止盈金额
                decimal takeProfitAmount = totalValue * (takeProfitPercentage / 100);
                takeProfitAmountTextBox.Text = takeProfitAmount.ToString("F2");

                _logger.Log($"止损比例变更 - 比例：{stopLossPercentage}%, 下单市值：{totalValue}, " +
                           $"止损金额：{stopLossAmount}, 止盈比例：{takeProfitPercentage}%, " +
                           $"止盈金额：{takeProfitAmount}");
            }

            // 更新止损价格
            if (decimal.TryParse(entryPriceTextBox.Text, out decimal currentPrice))
            {
                string direction = directionComboBox.SelectedItem?.ToString() ?? "buy";
                decimal stopLossPrice = CalculateStopLossPrice(currentPrice, stopLossPercentage, direction);
                stopLossPriceTextBox.Text = stopLossPrice.ToString("F4");

                _logger.Log($"更新止损价格 - 当前价：{currentPrice}, 方向：{direction}, " +
                           $"止损比例：{stopLossPercentage}%, 止损价：{stopLossPrice}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("更新止损数据失败", ex);
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
        // 先初始化所有控件
        contractTextBox = new TextBox();
        entryPriceTextBox = new TextBox();
        directionComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        directionComboBox.Items.AddRange(new string[] { "buy", "sell" });
        directionComboBox.SelectedIndex = 0;
        
        quantityTextBox = new TextBox();
        totalValueTextBox = new TextBox();
        marginTextBox = new TextBox();
        faceValueTextBox = new TextBox();
        leverageTextBox = new TextBox();

        var positionGroup = new GroupBox
        {
            Text = "下单头寸",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            Margin = new Padding(3)
        };

        var positionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 5,  // 增加一行用于放置按钮
            Padding = new Padding(3)
        };

        // 设置列宽比例为 20:30:20:30
        positionLayout.ColumnStyles.Clear();
        positionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        positionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        positionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        positionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        // 设置每行高度
        for (int i = 0; i < 5; i++)  // 增加一行
        {
            positionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }

        // 添加控件到布局
        AddLabelAndControl(positionLayout, 0, "开仓品种:", contractTextBox, "开仓价格:", entryPriceTextBox);
        AddLabelAndControl(positionLayout, 1, "开仓方向:", directionComboBox, "下单手数:", quantityTextBox);
        AddLabelAndControl(positionLayout, 2, "下单市值:", totalValueTextBox, "下单保证金:", marginTextBox);
        AddLabelAndControl(positionLayout, 3, "面值:", faceValueTextBox, "杠杆倍数:", leverageTextBox);  // 修正这一行的顺序和对齐

        // 添加按钮面板
        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(3)
        };

        // 设置按钮面板的列宽比例
        for (int i = 0; i < 3; i++)
        {
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        }

        var halfConfigButton = new Button
        {
            Text = "减半配置",
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            Height = 25
        };
        halfConfigButton.Click += (s, e) => ConfigButton_Click(s, e, 0.5m);

        var fullConfigButton = new Button
        {
            Text = "全额配置",
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            Height = 25
        };
        fullConfigButton.Click += (s, e) => ConfigButton_Click(s, e, 1.0m);

        var doubleConfigButton = new Button
        {
            Text = "加倍配置",
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            Height = 25
        };
        doubleConfigButton.Click += (s, e) => ConfigButton_Click(s, e, 2.0m);

        buttonPanel.Controls.Add(halfConfigButton, 0, 0);
        buttonPanel.Controls.Add(fullConfigButton, 1, 0);
        buttonPanel.Controls.Add(doubleConfigButton, 2, 0);

        // 将按钮面板添加到最后一行，并跨越所有列
        positionLayout.Controls.Add(buttonPanel, 0, 4);
        positionLayout.SetColumnSpan(buttonPanel, 4);

        // 设置只读属性
        contractTextBox.ReadOnly = true;
        entryPriceTextBox.ReadOnly = true;
        faceValueTextBox.ReadOnly = true;

        // 设置杠杆倍数默认值
        leverageTextBox.Text = "3";

        positionGroup.Controls.Add(positionLayout);
        return positionGroup;
    }

    private void AddLabelAndControl(TableLayoutPanel panel, int row, 
        string label1, Control control1,
        string label2, Control control2)
    {
        // 第一组：标签和控件
        var label1Control = new Label
        {
            Text = label1,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 6, 5, 3),
            AutoSize = false
        };

        control1.Dock = DockStyle.Fill;
        control1.Margin = new Padding(0, 3, 3, 3);

        // 第二组：标签和控件
        var label2Control = new Label
        {
            Text = label2,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 6, 5, 3),
            AutoSize = false
        };

        control2.Dock = DockStyle.Fill;
        control2.Margin = new Padding(0, 3, 3, 3);

        // 添加到面板，确保顺序正确
        panel.Controls.Add(label1Control, 0, row);
        panel.Controls.Add(control1, 1, row);
        panel.Controls.Add(label2Control, 2, row);
        panel.Controls.Add(control2, 3, row);
    }

    private GroupBox CreateStopLossGroup()
    {
        stopLossAmountTextBox = new TextBox();
        stopLossPercentageTextBox = new TextBox { Text = "10" };
        stopLossPriceTextBox = new TextBox();

        var stopLossGroup = new GroupBox
        {
            Text = "止损策略",
            Dock = DockStyle.Fill,
            Padding = new Padding(3),
            Margin = new Padding(2)
        };

        var stopLossLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(2)
        };

        // 设置行高
        for (int i = 0; i < 3; i++)
        {
            stopLossLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }

        // 添加控件
        AddLabelAndTextBox(stopLossLayout, "止损金额:", stopLossAmountTextBox, 0, true);
        AddLabelAndTextBox(stopLossLayout, "止损比例:", stopLossPercentageTextBox, 1, false);
        AddLabelAndTextBox(stopLossLayout, "止损价格:", stopLossPriceTextBox, 2, false);

        // 添加止损比例变更事件
        stopLossPercentageTextBox.TextChanged += StopLossPercentageTextBox_TextChanged;
        
        // 添加下单市值变更事件处理
        totalValueTextBox.TextChanged += TotalValueTextBox_TextChanged;

        stopLossGroup.Controls.Add(stopLossLayout);
        return stopLossGroup;
    }

    private GroupBox CreateTakeProfitGroup()
    {
        takeProfitPriceTextBox = new TextBox();
        takeProfitDrawdownTextBox = new TextBox { Text = "20" };
        takeProfitAmountTextBox = new TextBox { ReadOnly = true };  // 新增止盈金额输入框

        var takeProfitGroup = new GroupBox
        {
            Text = "止盈策略",
            Dock = DockStyle.Fill,
            Padding = new Padding(3),
            Margin = new Padding(2)
        };

        var takeProfitLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,  // 增加一行用于止盈金额
            Padding = new Padding(2)
        };

        // 设置行高
        for (int i = 0; i < 3; i++)
        {
            takeProfitLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }

        // 添加控件
        AddLabelAndTextBox(takeProfitLayout, "止盈金额:", takeProfitAmountTextBox, 0, true);  // 新增止盈金额
        AddLabelAndTextBox(takeProfitLayout, "止盈价格:", takeProfitPriceTextBox, 1, false);
        AddLabelAndTextBox(takeProfitLayout, "回撤比例:", takeProfitDrawdownTextBox, 2, false);

        takeProfitGroup.Controls.Add(takeProfitLayout);

        // 添加止盈比例变更事件处理
        takeProfitDrawdownTextBox.TextChanged += TakeProfitDrawdownTextBox_TextChanged;

        return takeProfitGroup;
    }

    // 添加止盈比例变更事件处理
    private void TakeProfitDrawdownTextBox_TextChanged(object? sender, EventArgs e)
    {
        try
        {
            if (decimal.TryParse(totalValueTextBox.Text, out decimal totalValue) &&
                decimal.TryParse(takeProfitDrawdownTextBox.Text, out decimal takeProfitPercentage))
            {
                // 计算止盈金额
                decimal takeProfitAmount = totalValue * (takeProfitPercentage / 100);
                takeProfitAmountTextBox.Text = takeProfitAmount.ToString("F2");

                _logger.Log($"止盈比例变更 - 比例：{takeProfitPercentage}%, " +
                           $"下单市值：{totalValue}, 止盈金额：{takeProfitAmount}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("更新止盈数据失败", ex);
        }
    }

    // 添加下单市值变更事件处理
    private void TotalValueTextBox_TextChanged(object? sender, EventArgs e)
    {
        try
        {
            if (decimal.TryParse(totalValueTextBox.Text, out decimal totalValue) &&
                decimal.TryParse(stopLossPercentageTextBox.Text, out decimal stopLossPercentage) &&
                decimal.TryParse(takeProfitDrawdownTextBox.Text, out decimal takeProfitPercentage))
            {
                // 计算止损金额
                decimal stopLossAmount = totalValue * (stopLossPercentage / 100);
                stopLossAmountTextBox.Text = stopLossAmount.ToString("F2");

                // 计算止盈金额（使用止盈比例）
                decimal takeProfitAmount = totalValue * (takeProfitPercentage / 100);
                takeProfitAmountTextBox.Text = takeProfitAmount.ToString("F2");

                _logger.Log($"更新止损止盈金额 - 下单市值：{totalValue}, " +
                           $"止损比例：{stopLossPercentage}%, 止损金额：{stopLossAmount}, " +
                           $"止盈比例：{takeProfitPercentage}%, 止盈金额：{takeProfitAmount}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("更新止损止盈金额失败", ex);
        }
    }

    private GroupBox CreateOrderPreviewGroup()
    {
        var orderPreviewGroup = new GroupBox
        {
            Text = "订单预览",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            Margin = new Padding(3),
            Height = 60
        };

        var orderPreviewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Padding = new Padding(3),
            Height = 40
        };

        // 设置列宽比例
        orderPreviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));  // 预览表格占50%
        orderPreviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); // 预览按钮
        orderPreviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); // 下单按钮

        // 创建预览表格面板
        var previewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 10,   // 增加列数以容纳所有信息
            RowCount = 1,
            AutoScroll = true,
            Margin = new Padding(0)
        };

        // 设置预览表格的列宽
        string[] columns = new[] {
            "合约", "方向", "数量", "价格", "市值", "保证金", "止损价", "预期止损", "止盈价", "预期止盈"
        };

        foreach (var column in columns)
        {
            var headerLabel = new Label
            {
                Text = column,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightGray,
                Margin = new Padding(1)
            };

            var valueLabel = new Label
            {
                Text = "--",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(1),
                Tag = column
            };

            int columnIndex = previewPanel.Controls.Count / 2;
            previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f));  // 每列占10%
            previewPanel.Controls.Add(headerLabel, columnIndex, 0);
            previewPanel.Controls.Add(valueLabel, columnIndex, 1);
        }

        // 预览按钮
        var previewButton = new Button
        {
            Text = "预览",
            Dock = DockStyle.Fill,
            Height = 25,
            Font = new Font("Microsoft YaHei", 9),
            BackColor = Color.FromArgb(240, 240, 240),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(3)
        };
        previewButton.Click += (s, e) => UpdatePreviewTable(previewPanel);

        // 下单按钮
        var submitButton = new Button
        {
            Text = "确认下单",
            Dock = DockStyle.Fill,
            Height = 25,
            Font = new Font("Microsoft YaHei", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(3)
        };
        submitButton.Click += SubmitOrder_Click;

        orderPreviewLayout.Controls.Add(previewPanel, 0, 0);
        orderPreviewLayout.Controls.Add(previewButton, 1, 0);
        orderPreviewLayout.Controls.Add(submitButton, 2, 0);

        orderPreviewGroup.Controls.Add(orderPreviewLayout);
        return orderPreviewGroup;
    }

    // 修改更新预览表格的方法
    private void UpdatePreviewTable(TableLayoutPanel previewPanel)
    {
        try
        {
            foreach (Control control in previewPanel.Controls)
            {
                if (control is Label label && label.Tag != null)
                {
                    string value = "--";
                    switch (label.Tag.ToString())
                    {
                        case "合约":
                            value = contractTextBox.Text;
                            break;
                        case "方向":
                            value = directionComboBox.SelectedItem?.ToString()?.ToUpper() ?? "--";
                            break;
                        case "数量":
                            value = quantityTextBox.Text + "张";
                            break;
                        case "价格":
                            value = entryPriceTextBox.Text;
                            break;
                        case "市值":
                            value = totalValueTextBox.Text;
                            break;
                        case "保证金":
                            value = marginTextBox.Text;
                            break;
                        case "止损价":
                            value = stopLossPriceTextBox.Text;
                            break;
                        case "预期止损":
                            value = stopLossAmountTextBox.Text + " 元";
                            break;
                        case "止盈价":
                            value = takeProfitPriceTextBox.Text;
                            break;
                        case "预期止盈":
                            value = takeProfitAmountTextBox.Text + " 元";
                            break;
                    }
                    label.Text = value;

                    // 为止损和止盈金额添加颜色
                    if (label.Tag.ToString() == "预期止损" && decimal.TryParse(stopLossAmountTextBox.Text, out decimal stopLoss))
                    {
                        label.ForeColor = stopLoss < 0 ? Color.Red : Color.Green;
                    }
                    else if (label.Tag.ToString() == "预期止盈" && decimal.TryParse(takeProfitAmountTextBox.Text, out decimal takeProfit))
                    {
                        label.ForeColor = takeProfit > 0 ? Color.Red : Color.Green;
                    }
                }
            }

            _logger.Log("订单预览更新完成");
        }
        catch (Exception ex)
        {
            _logger.LogError("更新订单预览失败", ex);
            MessageBox.Show("生成订单预览失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private GroupBox CreateReferenceDataGroup()
    {
        var referenceGroup = new GroupBox
        {
            Text = "参考数据",
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            Margin = new Padding(3)
        };

        referenceLayout = new TableLayoutPanel
        {
            Name = "referenceLayout",
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 5,  // 减少一行，不再需要按钮行
            Padding = new Padding(3)
        };

        // 设置列宽比例为 25:25:25:25
        for (int i = 0; i < 4; i++)
        {
            referenceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }

        // 设置行高
        for (int i = 0; i < 5; i++)
        {
            referenceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }

        // 添加标签和值
        var pairs = new[]
        {
            ("总权益:", "初始值:"),
            ("总市值:", "杠杆率:"),
            ("已用保证金:", "可用保证金:"),
            ("总风险金:", "可用风险金:"),
            ("单笔最大风险:", "建议风险金:")
        };

        for (int i = 0; i < pairs.Length; i++)
        {
            var (label1, label2) = pairs[i];
            
            // 第一组标签和值
            var labelControl1 = new Label
            {
                Text = label1,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Margin = new Padding(3),
                Tag = label1
            };

            var valueControl1 = new Label
            {
                Text = "0",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                Margin = new Padding(3)
            };

            // 第二组标签和值
            var labelControl2 = new Label
            {
                Text = label2,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Margin = new Padding(3),
                Tag = label2
            };

            var valueControl2 = new Label
            {
                Text = "0",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                Margin = new Padding(3)
            };

            referenceLayout.Controls.Add(labelControl1, 0, i);
            referenceLayout.Controls.Add(valueControl1, 1, i);
            referenceLayout.Controls.Add(labelControl2, 2, i);
            referenceLayout.Controls.Add(valueControl2, 3, i);
        }

        referenceGroup.Controls.Add(referenceLayout);
        return referenceGroup;
    }

    // 修改配置按钮点击事件
    private void ConfigButton_Click(object? sender, EventArgs e, decimal multiplier)
    {
        try
        {
            // 1. 获取单笔最大风险金并计算实际使用的风险金
            var maxSingleRiskLabel = FindLabelValue("单笔最大风险:");
            _logger.Log($"获取到单笔最大风险金：{maxSingleRiskLabel}");
            
            if (!decimal.TryParse(maxSingleRiskLabel, out decimal maxSingleRisk))
            {
                var ex = new Exception($"无法解析单笔最大风险金：{maxSingleRiskLabel}");
                _logger.LogError($"无法解析单笔最大风险金：{maxSingleRiskLabel}", ex);
                MessageBox.Show("无法获取单笔最大风险金", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 根据按钮计算实际使用的风险金
            decimal riskAmount = maxSingleRisk * multiplier;
            _logger.Log($"计算风险金：{maxSingleRisk} * {multiplier} = {riskAmount}");

            // 2. 获取止损比例
            if (!decimal.TryParse(stopLossPercentageTextBox.Text, out decimal stopLossPercentage))
            {
                MessageBox.Show("请输入有效的止损比例", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 3. 计算开仓市值：风险金/止损比例
            decimal totalValue = riskAmount / (stopLossPercentage / 100);
            _logger.Log($"计算开仓市值：{riskAmount} / {(stopLossPercentage / 100)} = {totalValue}");

            // 4. 获取杠杆倍数
            if (!decimal.TryParse(leverageTextBox.Text, out decimal leverage) || leverage <= 0)
            {
                MessageBox.Show("请输入有效的杠杆倍数", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 5. 计算下单保证金：开仓市值/杠杆倍数
            decimal margin = totalValue / leverage;
            _logger.Log($"计算下单保证金：{totalValue} / {leverage} = {margin}");

            // 6. 获取面值
            if (!decimal.TryParse(faceValueTextBox.Text, out decimal faceValue) || faceValue <= 0)
            {
                MessageBox.Show("请先选择合约以获取面值", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 7. 计算下单手数：开仓市值/面值（向下取整）
            int quantity = (int)Math.Floor(totalValue / faceValue);
            _logger.Log($"计算下单手数：{totalValue} / {faceValue} = {quantity}");

            // 8. 更新界面显示
            totalValueTextBox.Text = totalValue.ToString("F2");
            marginTextBox.Text = margin.ToString("F2");
            quantityTextBox.Text = quantity.ToString();

            // 9. 更新建议风险金显示
            UpdateLabelValue("建议风险金:", riskAmount.ToString("F2"));

            // 10. 更新订单预览
            UpdateOrderPreview();

            _logger.Log("配置计算完成");
        }
        catch (Exception ex)
        {
            _logger.LogError("配置计算失败", ex);
            MessageBox.Show($"配置计算失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // 修改 FindLabelValue 方法
    private string FindLabelValue(string labelText)
    {
        try
        {
            // 直接从 referenceLayout 中查找
            foreach (Control control in referenceLayout.Controls)
            {
                if (control is Label label && label.Tag?.ToString() == labelText)
                {
                    // 获取标签对应的值控件（在标签右边的单元格）
                    var valueLabel = referenceLayout.GetControlFromPosition(
                        referenceLayout.GetColumn(label) + 1,
                        referenceLayout.GetRow(label)
                    ) as Label;

                    if (valueLabel != null)
                    {
                        _logger.Log($"找到标签 {labelText} 的值：{valueLabel.Text}");
                        return valueLabel.Text;
                    }
                }
            }

            var ex = new Exception($"未找到标签 {labelText} 的值");
            _logger.LogError($"未找到标签 {labelText} 的值", ex);
            return "0";
        }
        catch (Exception ex)
        {
            _logger.LogError($"获取标签 {labelText} 的值失败", ex);
            return "0";
        }
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

    private void InitializeContractSubscription()
    {
        try 
        {
            // 收集所有合约并去重
            var allContracts = new HashSet<string>();
            
            // 从自选区收集合约
            foreach (ListViewItem item in customListView.Items)
            {
                string contract = item.Text;
                string normalizedContract = NormalizeContractName(contract) + "_USDT";  // 添加标准后缀
                allContracts.Add(normalizedContract);
                _logger.Log($"添加自选区合约订阅：{normalizedContract}");
            }
            
            // 从趋势多区收集合约
            foreach (ListViewItem item in trendLongListView.Items)
            {
                string contract = item.Text;
                string normalizedContract = NormalizeContractName(contract) + "_USDT";  // 添加标准后缀
                allContracts.Add(normalizedContract);
                _logger.Log($"添加趋势多区合约订阅：{normalizedContract}");
            }
            
            // 从趋势空区收集合约
            foreach (ListViewItem item in trendShortListView.Items)
            {
                string contract = item.Text;
                string normalizedContract = NormalizeContractName(contract) + "_USDT";  // 添加标准后缀
                allContracts.Add(normalizedContract);
                _logger.Log($"添加趋势空区合约订阅：{normalizedContract}");
            }

            // 从持仓订单中收集合约
            foreach (DataGridViewRow row in activeOrdersGrid.Rows)
            {
                string contract = row.Cells["Contract"].Value?.ToString() ?? "";
                if (!string.IsNullOrEmpty(contract))
                {
                    string normalizedContract = NormalizeContractName(contract) + "_USDT";  // 添加标准后缀
                    allContracts.Add(normalizedContract);
                    _logger.Log($"添加持仓订单合约订阅：{normalizedContract}");
                }
            }

            // 确保停止之前的订阅
            _marketDataService.Stop();

            // 订阅所有合约
            _marketDataService.SubscribeSymbols(allContracts);
            _marketDataService.Start();  // 启动行情服务

            // 添加日志
            _logger.Log($"已订阅合约：{string.Join(", ", allContracts)}");
        }
        catch (Exception ex)
        {
            _logger.LogError("初始化合约订阅失败", ex);
        }
    }

    // 修改 ListView 的双击事件处理
    private async void ListView_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (sender is ListView listView && listView.SelectedItems.Count > 0)
        {
            try
            {
                var selectedItem = listView.SelectedItems[0];
                string contract = selectedItem.Text;
                
                // 更新开仓品种输入框
                contractTextBox.Text = contract;
                
                // 如果有最新价，也更新开仓价格
                string lastPrice = selectedItem.SubItems[1].Text;
                if (lastPrice != "--")
                {
                    entryPriceTextBox.Text = lastPrice;
                }

                // 查询合约面值
                string normalizedContract = NormalizeContractName(contract) + "_USDT";
                decimal contractSize = await _marketDataService.GetContractSizeAsync(normalizedContract);
                faceValueTextBox.Text = contractSize.ToString();
                
                // 如果已选择账户，刷新参考数据
                if (AccountComboBox.SelectedItem is AccountItem selectedAccount)
                {
                    await RefreshDataAsync(selectedAccount.AccountId);
                }
                
                _logger.Log($"获取合约 {contract} 面值：{contractSize}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"获取合约面值失败：{ex.Message}", ex);
                MessageBox.Show($"获取合约面值失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // 修改 AddLabelAndTextBoxPair 方法
    private void AddLabelAndTextBoxPair(TableLayoutPanel panel, int row, 
        string label1, TextBox textBox1, bool readOnly1,
        string label2, TextBox? textBox2, bool readOnly2)  // 修改参数类型为可空
    {
        // 第一组：标签和输入框
        var label1Control = new Label
        {
            Text = label1,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 6, 5, 3),
            AutoSize = false
        };

        textBox1.Dock = DockStyle.Fill;
        textBox1.Margin = new Padding(0, 3, 3, 3);
        textBox1.ReadOnly = readOnly1;

        // 添加第一组控件
        panel.Controls.Add(label1Control, 0, row);
        panel.Controls.Add(textBox1, 1, row);

        // 只有当第二个标签不为空且有对应的textBox2时才添加第二组控件
        if (!string.IsNullOrEmpty(label2) && textBox2 != null)
        {
            var label2Control = new Label
            {
                Text = label2,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(3, 6, 5, 3),
                AutoSize = false
            };

            textBox2.Dock = DockStyle.Fill;
            textBox2.Margin = new Padding(0, 3, 3, 3);
            textBox2.ReadOnly = readOnly2;

            panel.Controls.Add(label2Control, 2, row);
            panel.Controls.Add(textBox2, 3, row);
        }
    }

    // 添加 InitializeListViewEvents 方法
    private void InitializeListViewEvents()
    {
        // 为所有机会区的 ListView 添加双击事件
        foreach (var listView in new[] { customListView, trendLongListView, trendShortListView })
        {
            listView.MouseDoubleClick += ListView_MouseDoubleClick;
        }
    }

    // 添加清理方法
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshCancellationTokenSource?.Cancel();
            _refreshCancellationTokenSource?.Dispose();
        }
        base.Dispose(disposing);
    }

    // 修改 UpdateOrderGridView 方法
    private void UpdateOrderGridView(List<OrderModel> orders)
    {
        try
        {
            _logger.Log($"开始更新活跃订单表格，订单数量：{orders.Count}");
            
            if (activeOrdersGrid.InvokeRequired)
            {
                activeOrdersGrid.Invoke(new Action(() => UpdateOrderGridView(orders)));
                return;
            }

            // 如果传入的订单列表为空且表格中已有数据，则保留现有数据
            if (orders.Count == 0 && activeOrdersGrid.Rows.Count > 0)
            {
                _logger.Log("收到空订单列表，保留现有数据");
                return;
            }

            activeOrdersGrid.SuspendLayout();
            activeOrdersGrid.Rows.Clear();

            foreach (var order in orders)
            {
                try
                {
                    var row = new DataGridViewRow();
                    row.CreateCells(activeOrdersGrid);
                    row.Tag = order;

                    // 设置单元格值
                    row.Cells[0].Value = order.Contract;
                    row.Cells[1].Value = order.Direction;
                    row.Cells[2].Value = order.Quantity;
                    row.Cells[3].Value = order.EntryPrice.ToString();
                    row.Cells[4].Value = order.CurrentStopLoss?.ToString() ?? "--";
                    row.Cells[5].Value = order.StopLossAmount?.ToString() ?? "--";
                    row.Cells[6].Value = order.LastPrice?.ToString() ?? "--";
                    row.Cells[7].Value = order.FloatingPnL?.ToString() ?? "--";
                    row.Cells[8].Value = order.OpenTime.ToString();

                    // 设置浮动盈亏的颜色
                    var floatingPnL = order.FloatingPnL ?? 0;
                    if (floatingPnL != 0)
                    {
                        row.Cells[7].Style.ForeColor = floatingPnL > 0 ? Color.Red : Color.Green;
                    }
                    else
                    {
                        row.Cells[7].Style.ForeColor = Color.Black;
                    }

                    activeOrdersGrid.Rows.Add(row);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"添加订单行失败：{ex.Message}", ex);
                }
            }

            activeOrdersGrid.ResumeLayout();
            _logger.Log($"订单表格更新完成，显示 {activeOrdersGrid.Rows.Count} 行");
        }
        catch (Exception ex)
        {
            _logger.LogError($"更新订单表格失败：{ex.Message}", ex);
        }
    }

    // 修改 InitializeComponent 中的 DataGridView 初始化
    private void InitializeDataGridView()
    {
        activeOrdersGrid.AutoGenerateColumns = false;
        activeOrdersGrid.Columns.Clear();
        
        // 添加列定义
        activeOrdersGrid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn 
            { 
                Name = "Contract",
                HeaderText = "合约",
                ReadOnly = true,
                Width = 100
            },
            new DataGridViewTextBoxColumn 
            { 
                Name = "Direction",
                HeaderText = "方向",
                ReadOnly = true,
                Width = 60
            },
            new DataGridViewTextBoxColumn 
            { 
                Name = "Quantity",
                HeaderText = "数量",
                ReadOnly = true,
                Width = 80
            },
            new DataGridViewTextBoxColumn 
            { 
                Name = "EntryPrice",
                HeaderText = "开仓价",
                ReadOnly = true,
                Width = 100
            },
            new DataGridViewTextBoxColumn 
            { 
                Name = "StopLoss",
                HeaderText = "止损价",
                ReadOnly = true,
                Width = 100
            },
            new DataGridViewTextBoxColumn 
            { 
                Name = "RealizedProfit",
                HeaderText = "预期止损金额",
                ReadOnly = true,
                Width = 120
            },
            new DataGridViewTextBoxColumn 
            { 
                Name = "LastPrice",
                HeaderText = "最新价",
                ReadOnly = true,
                Width = 100
            },
            new DataGridViewTextBoxColumn 
            { 
                Name = "FloatingPnL",
                HeaderText = "浮动盈亏",
                ReadOnly = true,
                Width = 100
            },
            new DataGridViewTextBoxColumn 
            { 
                Name = "OpenTime",
                HeaderText = "开仓时间",
                ReadOnly = true,
                Width = 150
            }
        });
    }

    // 添加 UpdateOtherUIElements 方法
    private void UpdateOtherUIElements()
    {
        try
        {
            // 更新其他UI元素的逻辑
            // 例如：更新按钮状态、更新状态栏等
            // 目前似乎不需要特别的更新，所以保持为空实现
        }
        catch (Exception ex)
        {
            _logger.LogError("更新其他UI元素失败", ex);
        }
    }

    // 添加已完成订单表格更新方法
    private void UpdateCompletedOrderGridView(List<OrderModel> orders)
    {
        try
        {
            if (completedOrdersGrid.InvokeRequired)
            {
                completedOrdersGrid.Invoke(new Action(() => UpdateCompletedOrderGridView(orders)));
                return;
            }

            completedOrdersGrid.SuspendLayout();
            completedOrdersGrid.Rows.Clear();

            foreach (var order in orders)
            {
                try
                {
                    var row = new DataGridViewRow();
                    row.CreateCells(completedOrdersGrid);
                    row.Tag = order;

                    // 设置单元格值
                    row.Cells[0].Value = order.OrderId;
                    row.Cells[1].Value = order.Contract;
                    row.Cells[2].Value = order.Direction;
                    row.Cells[3].Value = order.Quantity;
                    row.Cells[4].Value = order.EntryPrice.ToString();
                    row.Cells[5].Value = order.ClosePrice?.ToString() ?? "--";
                    row.Cells[6].Value = order.RealizedProfit?.ToString() ?? "--";
                    row.Cells[7].Value = order.CloseTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
                    row.Cells[8].Value = order.CloseType;

                    // 设置实现盈亏的颜色
                    if (order.RealizedProfit.HasValue)
                    {
                        row.Cells[6].Style.ForeColor = order.RealizedProfit.Value > 0 ? Color.Red : 
                                                      order.RealizedProfit.Value < 0 ? Color.Green : 
                                                      Color.Black;
                    }

                    completedOrdersGrid.Rows.Add(row);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"添加已平仓订单行失败：{ex.Message}", ex);
                }
            }

            completedOrdersGrid.ResumeLayout();
            _logger.Log($"已平仓订单表格更新完成，显示 {completedOrdersGrid.Rows.Count} 行");
        }
        catch (Exception ex)
        {
            _logger.LogError($"更新已平仓订单表格失败：{ex.Message}", ex);
        }
    }

    // 添加 AddOrderPanelContent 方法
    private void AddOrderPanelContent(Panel panel)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(3)
        };

        // 修改列宽比例为 30:30:20:20
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));  // 参考数据区
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));  // 下单头寸区
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));  // 止损策略区
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));  // 止盈策略区

        // 添加四个区域
        layout.Controls.Add(CreateReferenceDataGroup(), 0, 0);
        layout.Controls.Add(CreatePositionGroup(), 1, 0);
        layout.Controls.Add(CreateStopLossGroup(), 2, 0);
        layout.Controls.Add(CreateTakeProfitGroup(), 3, 0);

        panel.Controls.Add(layout);
    }

    // 添加 AddLabelPair 方法
    private void AddLabelPair(TableLayoutPanel panel, int row, 
        string label1Text, string value1Text, 
        string label2Text, string value2Text)
    {
        // 第一组标签
        var label1 = new Label
        {
            Text = label1Text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 6, 5, 3),
            Tag = label1Text  // 用于后续查找和更新
        };

        var value1 = new Label
        {
            Text = value1Text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Margin = new Padding(3, 0, 3, 0)
        };

        // 第二组标签和值
        var label2 = new Label
        {
            Text = label2Text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Margin = new Padding(3, 6, 5, 3),
            Tag = label2Text  // 用于后续查找和更新
        };

        var value2 = new Label
        {
            Text = value2Text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Margin = new Padding(3, 0, 3, 0)
        };

        // 添加到面板
        panel.Controls.Add(label1, 0, row);
        panel.Controls.Add(value1, 1, row);
        panel.Controls.Add(label2, 2, row);
        panel.Controls.Add(value2, 3, row);
    }

    // 添加 UpdateLabelValue 方法
    private void UpdateLabelValue(string labelText, string value)
    {
        try
        {
            // 在 TableLayoutPanel 中查找标签
            foreach (Control control in referenceLayout.Controls)
            {
                if (control is Label label && label.Tag?.ToString() == labelText)
                {
                    // 获取标签对应的值控件（在标签右边的单元格）
                    var valueLabel = referenceLayout.GetControlFromPosition(
                        referenceLayout.GetColumn(label) + 1,
                        referenceLayout.GetRow(label)
                    ) as Label;

                    if (valueLabel != null)
                    {
                        if (valueLabel.InvokeRequired)
                        {
                            valueLabel.Invoke(new Action(() => valueLabel.Text = value));
                        }
                        else
                        {
                            valueLabel.Text = value;
                        }
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"更新标签值失败：{labelText}", ex);
        }
    }

    // 添加新的辅助方法
    private void AddLabelAndTextBox(TableLayoutPanel panel, string labelText, TextBox textBox, int row, bool readOnly)
    {
        // 创建标签
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Left,  // 改为左对齐
            TextAlign = ContentAlignment.MiddleLeft,  // 改为左对齐
            AutoSize = true,  // 允许标签自动调整大小
            Margin = new Padding(5, 0, 0, 0)  // 左边距5像素
        };

        // 设置输入框属性
        textBox.Dock = DockStyle.Right;  // 改为右对齐
        textBox.Width = 80;  // 固定宽度
        textBox.Height = 23;  // 固定高度
        textBox.ReadOnly = readOnly;
        textBox.MaxLength = 10;
        textBox.Font = new Font(textBox.Font.FontFamily, 9);
        textBox.Margin = new Padding(0, 3, 5, 3);  // 右边距5像素

        // 创建容器面板
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 30,
            Margin = new Padding(0)
        };

        // 添加标签和输入框到容器
        container.Controls.Add(textBox);  // 先添加输入框
        container.Controls.Add(label);    // 再添加标签

        // 将容器添加到面板
        panel.Controls.Add(container, 0, row);
        panel.SetColumnSpan(container, 2);  // 跨越两列
    }

    // 添加缺失的 CalculateFloatingPnL 方法
    private decimal CalculateFloatingPnL(string direction, int quantity, decimal entryPrice, decimal lastPrice)
    {
        try
        {
            if (direction.ToLower() == "buy")
            {
                // 做多：(最新价 - 开仓价) * 数量
                return (lastPrice - entryPrice) * quantity;
            }
            else
            {
                // 做空：(开仓价 - 最新价) * 数量
                return (entryPrice - lastPrice) * quantity;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("计算浮动盈亏失败", ex);
            return 0;
        }
    }

    // 在 InitializeComponent 方法中添加订单表格的右键菜单初始化
    private void InitializeOrderContextMenu()
    {
        orderContextMenu = new ContextMenuStrip();
        
        // 添加菜单项
        var closePositionItem = new ToolStripMenuItem("平仓");
        var modifyTakeProfitItem = new ToolStripMenuItem("修改止盈");
        var modifyStopLossItem = new ToolStripMenuItem("修改止损");
        var adjustLeverageItem = new ToolStripMenuItem("调节杠杆");

        // 添加事件处理
        closePositionItem.Click += async (s, e) => await ShowClosePositionDialog();
        modifyTakeProfitItem.Click += async (s, e) => await ShowModifyTakeProfitDialog();
        modifyStopLossItem.Click += async (s, e) => await ShowModifyStopLossDialog();
        adjustLeverageItem.Click += async (s, e) => await ShowAdjustLeverageDialog();

        // 添加到菜单
        orderContextMenu.Items.AddRange(new ToolStripItem[] 
        {
            closePositionItem,
            modifyTakeProfitItem,
            modifyStopLossItem,
            adjustLeverageItem
        });

        // 为活跃订单表格添加右键菜单事件
        activeOrdersGrid.CellMouseClick += ActiveOrdersGrid_CellMouseClick;
    }

    private void ActiveOrdersGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
        {
            activeOrdersGrid.ClearSelection();
            activeOrdersGrid.Rows[e.RowIndex].Selected = true;
            
            // 保存选中的订单
            selectedOrder = activeOrdersGrid.Rows[e.RowIndex].Tag as OrderModel;
            
            // 根据订单状态启用/禁用菜单项
            if (selectedOrder != null)
            {
                var adjustLeverageItem = orderContextMenu.Items[3] as ToolStripMenuItem;
                if (adjustLeverageItem != null)
                {
                    // 只有在盈利时才能调节杠杆
                    adjustLeverageItem.Enabled = selectedOrder.FloatingPnL > 0;
                }
            }

            // 显示右键菜单
            orderContextMenu.Show(activeOrdersGrid, activeOrdersGrid.PointToClient(Cursor.Position));
        }
    }

    // 平仓对话框
    private async Task ShowClosePositionDialog()
    {
        if (selectedOrder == null) return;

        var dialog = new Form
        {
            Text = "平仓确认",
            Size = new Size(300, 200),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10)
        };

        // 添加信息显示
        AddDialogRow(layout, "合约:", selectedOrder.Contract, 0);
        AddDialogRow(layout, "开仓价:", selectedOrder.EntryPrice.ToString(), 1);
        AddDialogRow(layout, "当前价:", selectedOrder.LastPrice.ToString(), 2);
        AddDialogRow(layout, "预计盈亏:", selectedOrder.FloatingPnL.ToString(), 3);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40,
            Padding = new Padding(10, 5, 10, 5)
        };

        var confirmButton = new Button { Text = "确认平仓", Width = 80 };
        var cancelButton = new Button { Text = "取消", Width = 80 };

        confirmButton.Click += async (s, e) =>
        {
            try
            {
                await ClosePosition(selectedOrder);
                dialog.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError("平仓失败", ex);
                MessageBox.Show($"平仓失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        cancelButton.Click += (s, e) => dialog.DialogResult = DialogResult.Cancel;

        buttonPanel.Controls.AddRange(new Control[] { confirmButton, cancelButton });

        dialog.Controls.AddRange(new Control[] { layout, buttonPanel });
        dialog.ShowDialog(this);  // 移除 await
    }

    // 修改止盈对话框
    private async Task ShowModifyTakeProfitDialog()
    {
        if (selectedOrder == null) return;

        var dialog = new Form
        {
            Text = "修改止盈",
            Size = new Size(300, 200),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(10)
        };

        var takeProfitPriceBox = new TextBox();
        AddDialogRow(layout, "当前价:", selectedOrder.LastPrice.ToString(), 0);
        AddDialogRow(layout, "新止盈价:", takeProfitPriceBox, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40,
            Padding = new Padding(10, 5, 10, 5)
        };

        var confirmButton = new Button { Text = "确认", Width = 80 };
        var cancelButton = new Button { Text = "取消", Width = 80 };

        confirmButton.Click += async (s, e) =>
        {
            if (decimal.TryParse(takeProfitPriceBox.Text, out decimal newTakeProfit))
            {
                try
                {
                    await ModifyTakeProfit(selectedOrder, newTakeProfit);
                    dialog.DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    _logger.LogError("修改止盈失败", ex);
                    MessageBox.Show($"修改止盈失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("请输入有效的止盈价格", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        cancelButton.Click += (s, e) => dialog.DialogResult = DialogResult.Cancel;

        buttonPanel.Controls.AddRange(new Control[] { confirmButton, cancelButton });

        dialog.Controls.AddRange(new Control[] { layout, buttonPanel });
        dialog.ShowDialog(this);  // 移除 await
    }

    // 修改止损对话框
    private async Task ShowModifyStopLossDialog()
    {
        if (selectedOrder == null) return;

        var dialog = new Form
        {
            Text = "修改止损",
            Size = new Size(300, 200),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(10)
        };

        var stopLossPriceBox = new TextBox();
        AddDialogRow(layout, "当前价:", selectedOrder.LastPrice.ToString(), 0);
        AddDialogRow(layout, "新止损价:", stopLossPriceBox, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40,
            Padding = new Padding(10, 5, 10, 5)
        };

        var confirmButton = new Button { Text = "确认", Width = 80 };
        var cancelButton = new Button { Text = "取消", Width = 80 };

        confirmButton.Click += async (s, e) =>
        {
            if (decimal.TryParse(stopLossPriceBox.Text, out decimal newStopLoss))
            {
                try
                {
                    await ModifyStopLoss(selectedOrder, newStopLoss);
                    dialog.DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    _logger.LogError("修改止损失败", ex);
                    MessageBox.Show($"修改止损失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("请输入有效的止损价格", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        cancelButton.Click += (s, e) => dialog.DialogResult = DialogResult.Cancel;

        buttonPanel.Controls.AddRange(new Control[] { confirmButton, cancelButton });

        dialog.Controls.AddRange(new Control[] { layout, buttonPanel });
        dialog.ShowDialog(this);
    }

    // 调节杠杆对话框
    private async Task ShowAdjustLeverageDialog()
    {
        if (selectedOrder == null || selectedOrder.FloatingPnL <= 0) return;

        var dialog = new Form
        {
            Text = "调节杠杆",
            Size = new Size(300, 200),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(10)
        };

        var leverageBox = new TextBox { Text = selectedOrder.Leverage.ToString() };
        AddDialogRow(layout, "当前杠杆:", selectedOrder.Leverage.ToString(), 0);
        AddDialogRow(layout, "新杠杆:", leverageBox, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40,
            Padding = new Padding(10, 5, 10, 5)
        };

        var confirmButton = new Button { Text = "确认", Width = 80 };
        var cancelButton = new Button { Text = "取消", Width = 80 };

        confirmButton.Click += async (s, e) =>
        {
            if (int.TryParse(leverageBox.Text, out int newLeverage))
            {
                try
                {
                    await AdjustLeverage(selectedOrder, newLeverage);
                    dialog.DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    _logger.LogError("调节杠杆失败", ex);
                    MessageBox.Show($"调节杠杆失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("请输入有效的杠杆倍数", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        cancelButton.Click += (s, e) => dialog.DialogResult = DialogResult.Cancel;

        buttonPanel.Controls.AddRange(new Control[] { confirmButton, cancelButton });

        dialog.Controls.AddRange(new Control[] { layout, buttonPanel });
        dialog.ShowDialog(this);  // 移除 await
    }

    // 辅助方法：添加对话框行
    private void AddDialogRow(TableLayoutPanel layout, string label, string value, int row)
    {
        layout.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleRight }, 0, row);
        layout.Controls.Add(new Label { Text = value, TextAlign = ContentAlignment.MiddleLeft }, 1, row);
    }

    private void AddDialogRow(TableLayoutPanel layout, string label, Control control, int row)
    {
        layout.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleRight }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    // 平仓操作
    private async Task ClosePosition(OrderModel order)
    {
        try
        {
            using var connection = await _dbService.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. 更新订单状态
                var updateOrderSql = @"
                    UPDATE simulation_orders 
                    SET status = 'closed', 
                        close_price = @closePrice,
                        close_time = @closeTime,
                        realized_profit = @realizedProfit,
                        close_type = 'manual'
                    WHERE order_id = @orderId";

                await connection.ExecuteAsync(updateOrderSql, new
                {
                    orderId = order.OrderId,
                    closePrice = order.LastPrice,
                    closeTime = DateTime.Now,
                    realizedProfit = order.FloatingPnL
                }, transaction);

                // 2. 更新账户风险监控
                var updateRiskSql = @"
                    UPDATE account_risk_monitor 
                    SET used_margin = used_margin - @margin,
                        available_margin = available_margin + @margin,
                        used_risk = used_risk - @risk,
                        available_risk = available_risk + @risk
                    WHERE account_id = @accountId";

                await connection.ExecuteAsync(updateRiskSql, new
                {
                    accountId = order.AccountId,  // 已经是 long 类型，无需转换
                    margin = order.Margin,
                    risk = order.RiskAmount
                }, transaction);

                transaction.Commit();
                _logger.Log($"订单 {order.OrderId} 平仓成功");

                // 刷新数据
                await RefreshDataAsync(order.AccountId.ToString());
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("平仓失败", ex);
            throw;
        }
    }

    // 修改止盈
    private async Task ModifyTakeProfit(OrderModel order, decimal newTakeProfit)
    {
        try
        {
            var sql = @"
                UPDATE take_profit_strategies 
                SET take_profit_price = @takeProfitPrice,
                    update_time = @updateTime
                WHERE order_id = @orderId";

            await _dbService.ExecuteAsync(sql, new
            {
                orderId = order.OrderId,
                takeProfitPrice = newTakeProfit,
                updateTime = DateTime.Now
            });

            _logger.Log($"订单 {order.OrderId} 修改止盈价格为 {newTakeProfit} 成功");

            // 刷新数据
            if (AccountComboBox.SelectedItem is AccountItem selectedAccount)
            {
                await RefreshDataAsync(selectedAccount.AccountId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("修改止盈失败", ex);
            throw;
        }
    }

    // 修改止损
    private async Task ModifyStopLoss(OrderModel order, decimal newStopLoss)
    {
        try
        {
            var sql = @"
                UPDATE simulation_orders 
                SET current_stop_loss = @stopLoss
                WHERE order_id = @orderId";

            await _dbService.ExecuteAsync(sql, new
            {
                orderId = order.OrderId,
                stopLoss = newStopLoss
            });

            _logger.Log($"订单 {order.OrderId} 修改止损价格为 {newStopLoss} 成功");

            // 刷新数据
            await RefreshDataAsync(order.AccountId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError("修改止损失败", ex);
            throw;
        }
    }

    // 调节杠杆
    private async Task AdjustLeverage(OrderModel order, int newLeverage)
    {
        if (order.FloatingPnL <= 0)
        {
            throw new InvalidOperationException("只有在盈利时才能调整杠杆");
        }

        try
        {
            var sql = @"
                UPDATE simulation_orders 
                SET leverage = @leverage
                WHERE order_id = @orderId";

            await _dbService.ExecuteAsync(sql, new
            {
                orderId = order.OrderId,
                leverage = newLeverage
            });

            _logger.Log($"订单 {order.OrderId} 修改杠杆为 {newLeverage} 成功");

            // 刷新数据
            await RefreshDataAsync(order.AccountId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError("调节杠杆失败", ex);
            throw;
        }
    }

    // 添加下单事件处理方法
    private async void SubmitOrder_Click(object? sender, EventArgs e)
    {
        try
        {
            // 验证必要的输入
            if (!ValidateOrderInput(out string errorMessage))
            {
                MessageBox.Show(errorMessage, "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (AccountComboBox.SelectedItem is not AccountItem selectedAccount)
            {
                MessageBox.Show("请选择账户", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 构建订单数据
            var order = new OrderModel
            {
                OrderId = Guid.NewGuid().ToString(),
                AccountId = long.Parse(selectedAccount.AccountId),
                Contract = contractTextBox.Text,
                Direction = directionComboBox.SelectedItem?.ToString() ?? "buy",
                Quantity = int.Parse(quantityTextBox.Text),
                EntryPrice = decimal.Parse(entryPriceTextBox.Text),
                InitialStopLoss = decimal.Parse(stopLossPriceTextBox.Text),
                CurrentStopLoss = decimal.Parse(stopLossPriceTextBox.Text),
                Leverage = int.Parse(leverageTextBox.Text),
                Margin = decimal.Parse(marginTextBox.Text),
                TotalValue = decimal.Parse(totalValueTextBox.Text),
                Status = "open",
                OpenTime = DateTime.Now
            };

            // 构建止盈策略数据
            var takeProfitStrategies = new List<TakeProfitStrategy>();

            // 如果设置了固定价格止盈
            if (decimal.TryParse(takeProfitPriceTextBox.Text, out decimal takeProfitPrice))
            {
                takeProfitStrategies.Add(new TakeProfitStrategy
                {
                    OrderId = order.OrderId,
                    StrategyType = "fixed_price",
                    TriggerPrice = takeProfitPrice,
                    Status = "active"
                });
            }

            // 如果设置了回撤止盈
            if (decimal.TryParse(takeProfitDrawdownTextBox.Text, out decimal drawdownPercentage))
            {
                takeProfitStrategies.Add(new TakeProfitStrategy
                {
                    OrderId = order.OrderId,
                    StrategyType = "drawdown",
                    DrawdownPercentage = drawdownPercentage,
                    Status = "active"
                });
            }

            // 执行下单操作
            await PlaceOrderAsync(order, takeProfitStrategies);

            _logger.Log($"下单成功：{order.Contract}, {order.Direction}, 数量：{order.Quantity}");
            MessageBox.Show("下单成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 刷新数据
            await RefreshDataAsync(selectedAccount.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError("下单失败", ex);
            MessageBox.Show($"下单失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool ValidateOrderInput(out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrEmpty(contractTextBox.Text))
        {
            errorMessage = "请选择合约";
            return false;
        }

        if (directionComboBox.SelectedItem == null)
        {
            errorMessage = "请选择交易方向";
            return false;
        }

        if (!int.TryParse(quantityTextBox.Text, out _))
        {
            errorMessage = "请输入有效的数量";
            return false;
        }

        if (!decimal.TryParse(entryPriceTextBox.Text, out _))
        {
            errorMessage = "请输入有效的开仓价格";
            return false;
        }

        if (!decimal.TryParse(stopLossPriceTextBox.Text, out _))
        {
            errorMessage = "请输入有效的止损价格";
            return false;
        }

        if (!decimal.TryParse(marginTextBox.Text, out _))
        {
            errorMessage = "请输入有效的保证金";
            return false;
        }

        return true;
    }

    private async Task PlaceOrderAsync(OrderModel order, List<TakeProfitStrategy> takeProfitStrategies)
    {
        using var connection = await _dbService.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. 插入订单记录
            var insertOrderSql = @"
                INSERT INTO simulation_orders (
                    order_id, account_id, contract, direction, quantity, 
                    entry_price, initial_stop_loss, current_stop_loss,
                    leverage, margin, total_value, status, open_time
                ) VALUES (
                    @OrderId, @AccountId, @Contract, @Direction, @Quantity,
                    @EntryPrice, @InitialStopLoss, @CurrentStopLoss,
                    @Leverage, @Margin, @TotalValue, @Status, @OpenTime
                )";

            await connection.ExecuteAsync(insertOrderSql, order, transaction);

            // 2. 更新账户风险监控
            var updateRiskSql = @"
                UPDATE account_risk_monitor 
                SET used_margin = used_margin + @Margin,
                    available_margin = available_margin - @Margin,
                    used_risk = used_risk + @Risk,
                    available_risk = available_risk - @Risk
                WHERE account_id = @AccountId";

            // 修改这里：确保 InitialStopLoss 有值，如果没有则使用默认值
            decimal stopLossPrice = order.InitialStopLoss ?? order.EntryPrice;
            decimal riskAmount = Math.Abs(order.EntryPrice - stopLossPrice) * order.Quantity;

            await connection.ExecuteAsync(updateRiskSql, new
            {
                order.AccountId,
                order.Margin,
                Risk = riskAmount
            }, transaction);

            // 3. 插入止盈策略记录
            if (takeProfitStrategies.Any())
            {
                var insertStrategySql = @"
                    INSERT INTO take_profit_strategies (
                        order_id, strategy_type, trigger_price, drawdown_percentage, status
                    ) VALUES (
                        @OrderId, @StrategyType, @TriggerPrice, @DrawdownPercentage, @Status
                    )";

                foreach (var strategy in takeProfitStrategies)
                {
                    await connection.ExecuteAsync(insertStrategySql, strategy, transaction);
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
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