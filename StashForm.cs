using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace betterGitGUI
{
    /// <summary>
    /// 储藏（stash）窗口：保存当前改动、查看储藏内容、弹出 / 应用 / 删除。
    /// 想临时切分支又不想提交时，这是最常用的一步。
    /// </summary>
    public class StashForm : Form
    {
        private ListBox listStash;
        private RichTextBox boxPreview;
        private Label lblHint;
        private List<string> _refs = new List<string>();
        private bool _busy;

        public StashForm()
        {
            BuildUi();
            RefreshStashes();
        }

        private void BuildUi()
        {
            Text = "储藏（Stash）";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(960, 540);
            MinimumSize = new Size(720, 420);
            Font = new Font("Microsoft YaHei UI", 9F);

            var top = new Panel { Dock = DockStyle.Top, Height = 46 };
            var btnSave = new Button
            {
                Text = "把当前改动存进储藏...",
                Location = new Point(10, 8),
                Size = new Size(170, 28),
            };
            btnSave.Click += (s, e) => SaveStash();

            var btnRefresh = new Button
            {
                Text = "刷新",
                Location = new Point(188, 8),
                Size = new Size(70, 28),
            };
            btnRefresh.Click += (s, e) => RefreshStashes();

            lblHint = new Label
            {
                Location = new Point(272, 14),
                Size = new Size(660, 20),
                ForeColor = Color.FromArgb(100, 100, 100),
                Text = "储藏保存的是未提交的改动；弹出(pop)会应用并从列表移除，应用(apply)只应用不删除。",
            };
            top.Controls.AddRange(new Control[] { btnSave, btnRefresh, lblHint });

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 46 };
            var btnPop = new Button { Text = "弹出 (pop)", Location = new Point(10, 8), Size = new Size(110, 28) };
            var btnApply = new Button { Text = "应用 (apply)", Location = new Point(128, 8), Size = new Size(110, 28) };
            var btnDrop = new Button { Text = "删除 (drop)", Location = new Point(246, 8), Size = new Size(110, 28) };
            var btnClose = new Button
            {
                Text = "关闭",
                Location = new Point(844, 8),
                Size = new Size(100, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnPop.Click += (s, e) => DoStashAction("pop");
            btnApply.Click += (s, e) => DoStashAction("apply");
            btnDrop.Click += (s, e) => DoStashAction("drop");
            btnClose.Click += (s, e) => Close();
            bottom.Controls.AddRange(new Control[] { btnPop, btnApply, btnDrop, btnClose });

            listStash = new ListBox
            {
                Dock = DockStyle.Left,
                Width = 330,
                IntegralHeight = false,
                HorizontalScrollbar = true,
            };
            listStash.SelectedIndexChanged += (s, e) => ShowPreview();

            boxPreview = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                DetectUrls = false,
                HideSelection = false,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White,
            };

            var splitter = new Splitter { Dock = DockStyle.Left, Width = 4 };

            Controls.Add(boxPreview);
            Controls.Add(splitter);
            Controls.Add(listStash);
            Controls.Add(bottom);
            Controls.Add(top);
        }

        private string SelectedRef()
        {
            if (listStash.SelectedIndex < 0 || listStash.SelectedIndex >= _refs.Count) return null;
            return _refs[listStash.SelectedIndex];
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            UseWaitCursor = busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void RefreshStashes()
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                _refs.Clear();
                listStash.Items.Clear();

                var r = GitRunner.RunUi("stash list --format=\"%gd%x09%ci%x09%s\"");
                if (!r.Success)
                {
                    boxPreview.Text = "读取储藏列表失败：\n" + r.StdErr;
                    return;
                }

                using (var reader = new StringReader(r.StdOut))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Trim().Length == 0) continue;
                        var parts = line.Split('\t');
                        var reference = parts.Length > 0 ? parts[0].Trim() : "";
                        var date = parts.Length > 1 ? parts[1].Trim() : "";
                        var message = parts.Length > 2 ? parts[2] : "";
                        _refs.Add(reference);
                        listStash.Items.Add(reference + "   " + date + "   " + message);
                    }
                }

                if (listStash.Items.Count == 0)
                {
                    boxPreview.Clear();
                    boxPreview.Text = "（当前没有储藏）";
                }
            }
            finally
            {
                SetBusy(false);
            }

            // 选中第一项会触发 ShowPreview，但刷新过程中 _busy 还是 true、事件里会被拦掉，
            // 所以选中和预览都放到 busy 解除之后再做。
            if (listStash.Items.Count > 0)
            {
                listStash.SelectedIndex = 0;
                ShowPreview();
            }
        }

        private void ShowPreview()
        {
            var reference = SelectedRef();
            if (reference == null) { boxPreview.Clear(); return; }
            if (_busy) return;

            SetBusy(true);
            try
            {
                var r = GitRunner.RunUi("stash show --no-color -p " + GitRunner.Quote(reference));
                var text = r.Success ? r.StdOut : "读取储藏内容失败：\n" + r.StdErr;
                if (string.IsNullOrWhiteSpace(text)) text = "（这次储藏没有内容）";
                DiffRenderer.ApplyTo(boxPreview, DiffRenderer.BuildLines(text));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SaveStash()
        {
            var message = Form1.Prompt(this, "保存到储藏", "储藏说明:", "");
            if (message == null) return;

            SetBusy(true);
            try
            {
                var msg = message.Trim();
                var args = "stash push -u" + (msg.Length > 0 ? " -m " + GitRunner.Quote(msg) : "");
                var r = GitRunner.Run(args);
                if (!r.Success)
                {
                    MessageBox.Show(this, (r.StdOut + r.StdErr).Trim(), "储藏失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else if (r.StdOut.Trim().Length == 0)
                {
                    MessageBox.Show(this, "当前没有需要储藏的改动。", "储藏",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                SetBusy(false);
            }
            RefreshStashes();
        }

        private void DoStashAction(string action)
        {
            var reference = SelectedRef();
            if (reference == null)
            {
                MessageBox.Show(this, "请先在左侧选择一个储藏。");
                return;
            }

            if (action == "drop")
            {
                var ok = MessageBox.Show(this,
                    "删除储藏 " + reference + "？删除后只能从 reflog 里找回。",
                    "删除储藏", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (ok != DialogResult.Yes) return;
            }

            SetBusy(true);
            var result = GitRunner.Run("stash " + action + " " + GitRunner.Quote(reference));
            SetBusy(false);

            if (!result.Success)
            {
                MessageBox.Show(this, (result.StdOut + "\n" + result.StdErr).Trim(), "git stash " + action + " 失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (action == "pop" || action == "apply")
            {
                MessageBox.Show(this, "已" + (action == "pop" ? "弹出" : "应用") + "该储藏。\n"
                    + "如果出现冲突，请回到主窗口用冲突处理按钮解决。",
                    "储藏", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            RefreshStashes();
        }
    }
}
