using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace betterGitGUI
{
    /// <summary>
    /// 「刷绿」对话框。
    /// 上面是 GitHub 贡献图样式的选日面板（点选 / 拖动选择要在哪些天留下提交），
    /// 左下是生成方式（按文件拆、指定提交数拆、只生成空提交刷绿），右下是预览表。
    /// 提交会平均铺到所选日期上，具体时刻带随机抖动。
    /// </summary>
    public class SplitCommitForm : Form
    {
        private readonly string _baseMessage;

        private ContributionCalendar calendar;
        private Label lblSelection;
        private Button btnClearDays;
        private Button btnRecent7;
        private Button btnRecent30;
        private Button btnRecent90;

        private RadioButton rbByFile;
        private RadioButton rbByCount;
        private RadioButton rbEmpty;
        private NumericUpDown numCount;
        private Label lblCountUnit;
        private Label lblCountHint;

        private TextBox txtBase;

        private Button btnPreview;
        private Button btnRun;
        private Button btnCancel;

        private DataGridView grid;
        private Label lblInfo;

        private SplitPlan _plan;
        private bool _executed;
        private bool _indexTouched;

        public bool Executed { get { return _executed; } }
        public int CommitMadeCount { get; private set; }

        public SplitCommitForm(string baseMessage)
        {
            _baseMessage = baseMessage ?? "";
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "刷绿 - 贡献图 / 拆分提交";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1004, 724);
            Font = new Font("Microsoft YaHei UI", 9F);

            // ---- 贡献图选日 ----
            var grpDays = new GroupBox
            {
                Text = "选择日期（GitHub 贡献图样式：单击切换，按住拖动可连续选择）",
                Location = new Point(12, 8),
                Size = new Size(980, 192),
            };

            calendar = new ContributionCalendar
            {
                // 贡献图整体居中，左右留白对称，下面那排按钮与方格左边缘对齐
                Location = new Point(51, 20),
            };
            calendar.SelectionChanged += (s, e) => { UpdateSelectionInfo(); calendar.ClearDayCounts(); };

            btnClearDays = SmallButton("清空", 51, 154);
            btnRecent7 = SmallButton("最近 7 天", 133, 154);
            btnRecent30 = SmallButton("最近 30 天", 215, 154);
            btnRecent90 = SmallButton("最近 90 天", 297, 154);

            lblSelection = new Label
            {
                Location = new Point(389, 158),
                Size = new Size(578, 20),
                ForeColor = Color.FromArgb(87, 96, 106),
            };

            grpDays.Controls.AddRange(new Control[]
            {
                calendar, btnClearDays, btnRecent7, btnRecent30, btnRecent90, lblSelection,
            });

            // ---- 生成方式 ----
            var grpGrain = new GroupBox
            {
                Text = "生成方式",
                Location = new Point(12, 208),
                Size = new Size(306, 146),
            };

            rbByFile = new RadioButton
            {
                Text = "按文件拆分：每个文件一个提交",
                Location = new Point(12, 22),
                Size = new Size(282, 22),
                Checked = true,
            };
            rbByCount = new RadioButton
            {
                Text = "指定提交数：",
                Location = new Point(12, 48),
                Size = new Size(100, 22),
            };
            numCount = new NumericUpDown
            {
                Location = new Point(114, 46),
                Size = new Size(64, 24),
                Minimum = 1,
                Maximum = 1000,
                Value = 5,
                Enabled = false,
            };
            lblCountUnit = new Label
            {
                Text = "个",
                Location = new Point(182, 50),
                Size = new Size(40, 20),
            };
            lblCountHint = new Label
            {
                Text = "（改动行不够时，多出的部分是空提交）",
                Location = new Point(12, 70),
                Size = new Size(284, 18),
                ForeColor = Color.Gray,
                Font = new Font("Microsoft YaHei UI", 8F),
            };
            rbEmpty = new RadioButton
            {
                Text = "只刷绿：直接生成空提交",
                Location = new Point(12, 96),
                Size = new Size(282, 22),
            };
            var lblEmptyHint = new Label
            {
                Text = "（不碰工作区文件，只往贡献图上添提交）",
                Location = new Point(12, 120),
                Size = new Size(284, 18),
                ForeColor = Color.Gray,
                Font = new Font("Microsoft YaHei UI", 8F),
            };

            grpGrain.Controls.AddRange(new Control[]
            {
                rbByFile, rbByCount, numCount, lblCountUnit, lblCountHint, rbEmpty, lblEmptyHint,
            });

            // ---- 提交信息 ----
            var grpMsg = new GroupBox
            {
                Text = "提交信息",
                Location = new Point(12, 362),
                Size = new Size(306, 212),
            };
            var lblMsgHint = new Label
            {
                Text = "基础信息会作为前缀，自动加「（1/N）」，右侧每一条都可以单独修改。",
                Location = new Point(12, 20),
                Size = new Size(284, 32),
                ForeColor = Color.Gray,
                Font = new Font("Microsoft YaHei UI", 8F),
            };
            txtBase = new TextBox
            {
                Location = new Point(12, 56),
                Size = new Size(282, 146),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                // 多行 TextBox 只认 CRLF，不换行会连成一行
                Text = _baseMessage.Replace("\r\n", "\n").Replace("\n", "\r\n"),
            };
            grpMsg.Controls.AddRange(new Control[] { lblMsgHint, txtBase });

            // ---- 按钮 ----
            btnPreview = new Button
            {
                Text = "生成预览",
                Location = new Point(12, 584),
                Size = new Size(96, 32),
            };
            btnRun = new Button
            {
                Text = "执行",
                Location = new Point(114, 584),
                Size = new Size(96, 32),
                Enabled = false,
            };
            btnCancel = new Button
            {
                Text = "关闭",
                Location = new Point(216, 584),
                Size = new Size(82, 32),
            };

            lblInfo = new Label
            {
                Text = "先在贡献图上点选日期，再点「生成预览」。",
                Location = new Point(12, 622),
                Size = new Size(306, 92),
                ForeColor = Color.DimGray,
            };

            // ---- 预览表 ----
            var lblGrid = new Label
            {
                Text = "拆分预览（提交信息可直接编辑）",
                Location = new Point(330, 208),
                Size = new Size(400, 20),
            };

            grid = new DataGridView
            {
                Location = new Point(330, 230),
                Size = new Size(662, 484),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            };
            grid.Columns.Add(MakeColumn("序号", 40, true));
            grid.Columns.Add(MakeColumn("文件", 108, true));
            grid.Columns.Add(MakeColumn("行数", 52, true));
            grid.Columns.Add(MakeColumn("提交时间", 132, true));
            // 剩下的宽度给提交信息，方便直接改
            grid.Columns.Add(MakeColumn("提交信息", 310, false));

            Controls.AddRange(new Control[]
            {
                grpDays, grpGrain, grpMsg, btnPreview, btnRun, btnCancel, lblInfo, lblGrid, grid,
            });

            rbByFile.CheckedChanged += (s, e) => UpdateCountUi();
            rbByCount.CheckedChanged += (s, e) => UpdateCountUi();
            rbEmpty.CheckedChanged += (s, e) => UpdateCountUi();
            numCount.ValueChanged += (s, e) => UpdateSelectionInfo();

            btnClearDays.Click += (s, e) => calendar.ClearSelection();
            btnRecent7.Click += (s, e) => calendar.SelectRecentDays(7);
            btnRecent30.Click += (s, e) => calendar.SelectRecentDays(30);
            btnRecent90.Click += (s, e) => calendar.SelectRecentDays(90);

            UpdateCountUi();
            calendar.SelectRecentDays(1); // 默认只选今天，和以前「保持今天」的习惯一致

            btnPreview.Click += BtnPreview_Click;
            btnRun.Click += BtnRun_Click;
            btnCancel.Click += (s, e) => Close();
            FormClosed += SplitCommitForm_FormClosed;
        }

        private static Button SmallButton(string text, int x, int y)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(76, 26),
                Font = new Font("Microsoft YaHei UI", 8.5F),
            };
        }

        private static DataGridViewTextBoxColumn MakeColumn(string header, int width, bool readOnly)
        {
            var col = new DataGridViewTextBoxColumn();
            col.HeaderText = header;
            col.Width = width;
            col.ReadOnly = readOnly;
            col.SortMode = DataGridViewColumnSortMode.NotSortable;
            return col;
        }

        // ---------- 界面联动 ----------

        private void UpdateCountUi()
        {
            // 只刷绿时，数字的含义是「每天几个提交」；拆分时是「一共几个提交」
            bool perDay = rbEmpty.Checked;
            numCount.Enabled = rbByCount.Checked || perDay;
            numCount.Maximum = perDay ? 30 : 1000;
            if (numCount.Value > numCount.Maximum) numCount.Value = numCount.Maximum;
            lblCountUnit.Text = perDay ? "个/天" : "个";
            lblCountHint.Text = perDay
                ? "（每天固定数量，时刻随机错开）"
                : "（改动行不够时，多出的部分是空提交）";
            // 数字框是共用的：只刷绿时它的含义是「每天几个」，把这个说法写进单选标题里免得看混
            rbEmpty.Text = perDay
                ? "只刷绿：按上面「个/天」生成空提交"
                : "只刷绿：直接生成空提交";
            UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            int days = calendar.SelectedDayCount;
            if (days == 0)
            {
                lblSelection.Text = "尚未选择日期：点击方格选中，按住拖动可连续选择";
                return;
            }

            if (rbEmpty.Checked)
            {
                int perDay = (int)numCount.Value;
                lblSelection.Text = string.Format("已选 {0} 天 · 每天 {1} 个 · 共 {2} 个空提交",
                    days, perDay, days * perDay);
            }
            else
            {
                var first = calendar.SelectedDays[0];
                var last = calendar.SelectedDays[days - 1];
                lblSelection.Text = string.Format("已选 {0} 天（{1:yyyy-MM-dd} ~ {2:yyyy-MM-dd}）· 提交会平均铺开",
                    days, first, last);
            }
        }

        private SplitOptions CollectOptions()
        {
            var opt = new SplitOptions();
            if (rbEmpty.Checked) opt.Granularity = SplitGranularity.EmptyOnly;
            else if (rbByCount.Checked) opt.Granularity = SplitGranularity.ByCount;
            else opt.Granularity = SplitGranularity.ByFile;

            var days = calendar.SelectedDays;
            opt.SelectedDays = days;
            opt.BaseMessage = txtBase.Text.Trim();

            int perDay = (int)numCount.Value;
            opt.CommitCount = opt.Granularity == SplitGranularity.EmptyOnly
                ? perDay * Math.Max(1, days.Count)  // 每天固定份数，总数自然被天数放大
                : perDay;
            return opt;
        }

        // ---------- 预览 ----------

        private void BtnPreview_Click(object sender, EventArgs e)
        {
            var opt = CollectOptions();
            if (opt.SelectedDays.Count == 0)
            {
                MessageBox.Show(this, "请先在贡献图上点选至少一天。", "还没有选日期",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (opt.CommitCount > 1000)
            {
                MessageBox.Show(this,
                    string.Format("计划 {0} 个提交，超过 1000 个上限。请减少每天提交数或减少选中的天数。", opt.CommitCount),
                    "提交数太多", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            UseWaitCursor = true;
            btnPreview.Enabled = false;
            try
            {
                _plan = SplitCommitEngine.BuildPlan(opt);
                _indexTouched = opt.Granularity != SplitGranularity.EmptyOnly;
                FillGrid();
                ShowPlanSummary(opt);
                btnRun.Enabled = _plan.Slices.Count > 0;
            }
            catch (Exception ex)
            {
                _plan = null;
                btnRun.Enabled = false;
                grid.Rows.Clear();
                calendar.ClearDayCounts();
                MessageBox.Show(this, ex.Message, "无法生成预览", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                btnPreview.Enabled = true;
                UseWaitCursor = false;
            }
        }

        private void ShowPlanSummary(SplitOptions opt)
        {
            // 把每天的提交数回填到贡献图上，格子的浓淡就是执行后贡献图的样子
            var counts = new Dictionary<DateTime, int>();
            foreach (var slice in _plan.Slices)
            {
                var day = slice.When.Date;
                counts[day] = counts.ContainsKey(day) ? counts[day] + 1 : 1;
            }
            calendar.SetDayCounts(counts);

            var usedDays = new HashSet<DateTime>(counts.Keys);
            lblInfo.Text = string.Format(
                "{0}\r\n覆盖 {1} 天，共 {2} 个提交。\r\n核对预览后点「执行」。",
                opt.Granularity == SplitGranularity.EmptyOnly
                    ? "空提交模式：不改动任何文件。"
                    : string.Format("共 {0} 个文件、{1} 行改动。", _plan.TotalFiles, _plan.TotalLines),
                usedDays.Count, _plan.Slices.Count);
        }

        private void FillGrid()
        {
            grid.Rows.Clear();
            foreach (var slice in _plan.Slices)
            {
                string files = "(空提交)";
                if (slice.Paths.Count > 0)
                {
                    files = string.Join("，", slice.Paths.ToArray());
                    if (files.Length > 46) files = files.Substring(0, 45) + "…";
                }
                // 二进制没有「行」的概念，这里不给行数、也不给差异
                string linesText;
                if (slice.WholeFile) linesText = slice.Binary ? "二进制" : slice.Lines.ToString();
                else linesText = slice.Binary ? "二进制" : (slice.Paths.Count == 0 ? "—" : slice.Lines.ToString());

                grid.Rows.Add(
                    (slice.Index + 1).ToString(),
                    files,
                    linesText,
                    slice.When.ToString("yyyy-MM-dd HH:mm:ss"),
                    slice.Message);
            }
        }

        // ---------- 执行 ----------

        private async void BtnRun_Click(object sender, EventArgs e)
        {
            if (_plan == null) return;

            for (int i = 0; i < _plan.Slices.Count; i++)
            {
                var value = grid.Rows[i].Cells[4].Value;
                var message = value == null ? "" : value.ToString().Trim();
                if (message.Length == 0)
                {
                    MessageBox.Show(this, string.Format("第 {0} 个提交的信息是空的，请填写。", i + 1),
                        "提交信息为空", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    grid.CurrentCell = grid.Rows[i].Cells[4];
                    return;
                }
                _plan.Slices[i].Message = message;
            }

            var first = _plan.Slices.Count > 0 ? _plan.Slices[0].When : DateTime.Today;
            var last = _plan.Slices.Count > 0 ? _plan.Slices[_plan.Slices.Count - 1].When : DateTime.Today;
            bool emptyMode = _plan.Granularity == SplitGranularity.EmptyOnly;
            var confirm = MessageBox.Show(this,
                emptyMode
                    ? string.Format("将创建 {0} 个空提交（不修改任何文件）：\n时间范围 {1:yyyy-MM-dd HH:mm} ~ {2:yyyy-MM-dd HH:mm}\n\n确定继续？",
                        _plan.Slices.Count, first, last)
                    : string.Format("将创建 {0} 个提交：\n时间范围 {1:yyyy-MM-dd HH:mm} ~ {2:yyyy-MM-dd HH:mm}\n\n拆分后暂存区会被这些提交清空，工作区文件不变。确定继续？",
                        _plan.Slices.Count, first, last),
                "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            btnPreview.Enabled = false;
            btnRun.Enabled = false;
            UseWaitCursor = true;
            try
            {
                var made = await System.Threading.Tasks.Task.Run(() =>
                    SplitCommitEngine.Execute(_plan,
                        s => BeginInvoke(new Action(() => { lblInfo.Text = s; }))));
                CommitMadeCount = made;
                _executed = true;
                MessageBox.Show(this, string.Format("完成：已创建 {0} 个提交。", made),
                    "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "执行失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!_executed)
                {
                    btnPreview.Enabled = true;
                    btnRun.Enabled = _plan != null;
                }
                UseWaitCursor = false;
            }
        }

        private void SplitCommitForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 没有执行就关闭：清掉预览阶段留下的 intent-to-add 记录，避免暂存区看起来被改动
            if (!_executed && _indexTouched)
            {
                try { SplitCommitEngine.CleanupIndex(); }
                catch { /* 忽略 */ }
            }
        }
    }
}
