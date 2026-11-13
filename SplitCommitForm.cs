using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace betterGitGUI
{
    /// <summary>
    /// 「刷绿」对话框：把工作区未提交的改动按选定方式拆成多个提交，
    /// 支持按文件 / 指定提交数两种粒度，提交时间可落在今天、最近 N 天或指定区间。
    /// </summary>
    public class SplitCommitForm : Form
    {
        private readonly string _baseMessage;

        private RadioButton rbByFile;
        private RadioButton rbByCount;
        private NumericUpDown numCount;

        private RadioButton rbToday;
        private RadioButton rbRecent;
        private NumericUpDown numDays;
        private RadioButton rbRange;
        private DateTimePicker dtStart;
        private DateTimePicker dtEnd;

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
            Text = "刷绿 - 拆分提交";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 600);
            Font = new Font("Microsoft YaHei UI", 9F);

            // ---- 拆分方式 ----
            var grpGrain = new GroupBox
            {
                Text = "拆分方式",
                Location = new Point(12, 10),
                Size = new Size(330, 96),
            };

            rbByFile = new RadioButton
            {
                Text = "按文件：每个文件一个提交",
                Location = new Point(12, 24),
                Size = new Size(300, 24),
                Checked = true,
            };
            rbByCount = new RadioButton
            {
                Text = "指定提交数量：",
                Location = new Point(12, 56),
                Size = new Size(120, 24),
            };
            numCount = new NumericUpDown
            {
                Location = new Point(140, 54),
                Size = new Size(70, 24),
                Minimum = 1,
                Maximum = 1000,
                Value = 5,
            };
            var lblCountUnit = new Label { Text = "个", Location = new Point(226, 58), Size = new Size(24, 20) };
            var lblCountHint = new Label
            {
                Text = "（改动行不够时，多出的部分为空提交）",
                Location = new Point(12, 76),
                Size = new Size(300, 16),
                ForeColor = Color.Gray,
                Font = new Font("Microsoft YaHei UI", 8F),
            };

            grpGrain.Controls.AddRange(new Control[]
            {
                rbByFile, rbByCount, numCount, lblCountUnit, lblCountHint,
            });

            // ---- 提交时间 ----
            var grpDate = new GroupBox
            {
                Text = "提交时间（决定贡献图能刷到哪些天）",
                Location = new Point(12, 114),
                Size = new Size(330, 132),
            };

            rbToday = new RadioButton
            {
                Text = "保持今天",
                Location = new Point(12, 22),
                Size = new Size(300, 24),
                Checked = true,
            };
            rbRecent = new RadioButton
            {
                Text = "最近",
                Location = new Point(12, 50),
                Size = new Size(56, 24),
            };
            numDays = new NumericUpDown
            {
                Location = new Point(72, 48),
                Size = new Size(64, 24),
                Minimum = 1,
                Maximum = 3650,
                Value = 7,
            };
            var lblDaysUnit = new Label { Text = "天内随机", Location = new Point(142, 52), Size = new Size(80, 20) };

            rbRange = new RadioButton
            {
                Text = "指定区间：",
                Location = new Point(12, 78),
                Size = new Size(100, 24),
            };
            dtStart = new DateTimePicker
            {
                Location = new Point(112, 76),
                Size = new Size(120, 24),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                Value = DateTime.Today.AddDays(-6),
                Enabled = false,
            };
            var lblTo = new Label { Text = "至", Location = new Point(238, 80), Size = new Size(20, 20) };
            dtEnd = new DateTimePicker
            {
                Location = new Point(258, 76),
                Size = new Size(120, 24),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                Value = DateTime.Today,
                Enabled = false,
            };
            var lblDateHint = new Label
            {
                Text = "未来的时间不会被贡献图统计，会自动收拢到今天之前。",
                Location = new Point(12, 106),
                Size = new Size(310, 16),
                ForeColor = Color.Gray,
                Font = new Font("Microsoft YaHei UI", 8F),
            };

            grpDate.Controls.AddRange(new Control[]
            {
                rbToday, rbRecent, numDays, lblDaysUnit, rbRange, dtStart, lblTo, dtEnd, lblDateHint,
            });

            // ---- 提交信息 ----
            var grpMsg = new GroupBox
            {
                Text = "提交信息",
                Location = new Point(12, 254),
                Size = new Size(330, 250),
            };
            var lblMsgHint = new Label
            {
                Text = "基础信息会作为前缀，自动加「（1/N）」，右侧每一条都可以单独修改。",
                Location = new Point(12, 20),
                Size = new Size(310, 32),
                ForeColor = Color.Gray,
                Font = new Font("Microsoft YaHei UI", 8F),
            };
            txtBase = new TextBox
            {
                Location = new Point(12, 78),
                Size = new Size(306, 158),
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
                Location = new Point(12, 514),
                Size = new Size(104, 32),
            };
            btnRun = new Button
            {
                Text = "执行拆分",
                Location = new Point(126, 514),
                Size = new Size(104, 32),
                Enabled = false,
            };
            btnCancel = new Button
            {
                Text = "关闭",
                Location = new Point(240, 514),
                Size = new Size(80, 32),
            };

            lblInfo = new Label
            {
                Text = "尚未生成预览。设定好方式与时间后点「生成预览」。",
                Location = new Point(12, 546),
                Size = new Size(330, 46),
                ForeColor = Color.DimGray,
            };

            // ---- 预览表 ----
            var lblGrid = new Label
            {
                Text = "拆分预览（提交信息可直接编辑）",
                Location = new Point(356, 12),
                Size = new Size(300, 20),
            };

            grid = new DataGridView
            {
                Location = new Point(356, 34),
                Size = new Size(532, 554),
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
            grid.Columns.Add(MakeColumn("序号", 34, true));
            grid.Columns.Add(MakeColumn("文件", 108, true));
            grid.Columns.Add(MakeColumn("行数", 52, true));
            grid.Columns.Add(MakeColumn("提交时间", 118, true));
            // 剩下的宽度给提交信息，方便直接改
            grid.Columns.Add(MakeColumn("提交信息", 220, false));

            Controls.AddRange(new Control[]
            {
                grpGrain, grpDate, grpMsg, btnPreview, btnRun, btnCancel, lblInfo, lblGrid, grid,
            });

            rbByFile.CheckedChanged += (s, e) => numCount.Enabled = rbByCount.Checked;
            rbByCount.CheckedChanged += (s, e) => numCount.Enabled = rbByCount.Checked;
            numCount.Enabled = false;

            rbToday.CheckedChanged += (s, e) => UpdateDateInputs();
            rbRecent.CheckedChanged += (s, e) => UpdateDateInputs();
            rbRange.CheckedChanged += (s, e) => UpdateDateInputs();
            UpdateDateInputs();

            btnPreview.Click += BtnPreview_Click;
            btnRun.Click += BtnRun_Click;
            btnCancel.Click += (s, e) => Close();
            FormClosed += SplitCommitForm_FormClosed;
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

        private void UpdateDateInputs()
        {
            numDays.Enabled = rbRecent.Checked;
            dtStart.Enabled = rbRange.Checked;
            dtEnd.Enabled = rbRange.Checked;
        }

        private SplitOptions CollectOptions()
        {
            var opt = new SplitOptions();
            opt.Granularity = rbByCount.Checked ? SplitGranularity.ByCount : SplitGranularity.ByFile;
            opt.CommitCount = (int)numCount.Value;
            opt.BaseMessage = txtBase.Text.Trim();

            if (rbRecent.Checked) opt.DateMode = SplitDateMode.RecentDays;
            else if (rbRange.Checked) opt.DateMode = SplitDateMode.DateRange;
            else opt.DateMode = SplitDateMode.KeepToday;

            opt.RecentDays = (int)numDays.Value;
            opt.RangeStart = dtStart.Value.Date;
            opt.RangeEnd = dtEnd.Value.Date;
            return opt;
        }

        // ---------- 预览 ----------

        private void BtnPreview_Click(object sender, EventArgs e)
        {
            var opt = CollectOptions();
            UseWaitCursor = true;
            btnPreview.Enabled = false;
            try
            {
                _plan = SplitCommitEngine.BuildPlan(opt);
                _indexTouched = true;
                FillGrid();
                lblInfo.Text = string.Format(
                    "共 {0} 个文件、{1} 行改动\r\n将生成 {2} 个提交，核对预览后执行。",
                    _plan.TotalFiles, _plan.TotalLines, _plan.Slices.Count);
                btnRun.Enabled = _plan.Slices.Count > 0;
            }
            catch (Exception ex)
            {
                _plan = null;
                btnRun.Enabled = false;
                grid.Rows.Clear();
                MessageBox.Show(this, ex.Message, "无法生成预览", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                btnPreview.Enabled = true;
                UseWaitCursor = false;
            }
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
                var linesText = slice.Binary ? "二进制" : slice.Lines.ToString();
                grid.Rows.Add(
                    (slice.Index + 1).ToString(),
                    files,
                    linesText,
                    slice.When.ToString("yyyy-MM-dd HH:mm"),
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
            var confirm = MessageBox.Show(this,
                string.Format("将创建 {0} 个提交：\n时间范围 {1:yyyy-MM-dd HH:mm} ~ {2:yyyy-MM-dd HH:mm}\n\n拆分后暂存区会被这些提交清空，工作区文件不变。确定继续？",
                    _plan.Slices.Count, first, last),
                "确认拆分", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
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
                    "拆分完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "拆分失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            // 没有执行拆分就关闭：清掉预览阶段留下的 intent-to-add 记录，避免暂存区看起来被改动
            if (!_executed && _indexTouched)
            {
                try { SplitCommitEngine.CleanupIndex(); }
                catch { /* 忽略 */ }
            }
        }
    }
}
