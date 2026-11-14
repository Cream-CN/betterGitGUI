using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace betterGitGUI
{
    /// <summary>
    /// 远程地址切换窗口：列出仓库的所有远程，识别当前是 SSH 还是 HTTPS，
    /// 直接切换到另一种写法（git remote set-url）。
    /// </summary>
    public class RemotesForm : Form
    {
        private ListView listRemotes;
        private Label lblHint;
        private Label lblPreview;
        private RadioButton rbSsh;
        private RadioButton rbHttps;
        private Button btnApply;
        private Button btnCopy;
        private Button btnAdd;
        private Button btnClose;

        private readonly List<RemoteEntry> _remotes = new List<RemoteEntry>();

        /// <summary>有点击过「应用」就置位，主窗口据此刷新。</summary>
        public bool Changed { get; private set; }

        private class RemoteEntry
        {
            public string Name = "";
            public string Url = "";
        }

        public RemotesForm()
        {
            BuildUi();
            LoadRemotes();
        }

        private void BuildUi()
        {
            Text = "远程地址（SSH / HTTPS）";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 470);
            MinimumSize = new Size(700, 420);
            Font = new Font("Microsoft YaHei UI", 9F);

            lblHint = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Text = "  选择一个远程；程序会识别它当前的写法，再切换到另一种（SSH ↔ HTTPS）。",
                TextAlign = ContentAlignment.MiddleLeft,
            };

            listRemotes = new ListView
            {
                Dock = DockStyle.Top,
                Height = 180,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                HideSelection = false,
            };
            listRemotes.Columns.Add("远程名", 130);
            listRemotes.Columns.Add("当前地址", 650);
            listRemotes.SelectedIndexChanged += (s, e) => OnRemoteSelected();

            var box = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "切换成",
                Padding = new Padding(10, 8, 10, 8),
            };

            rbSsh = new RadioButton
            {
                Text = "SSH（git@host:owner/repo.git）",
                Location = new Point(14, 26),
                Size = new Size(320, 24),
            };
            rbSsh.CheckedChanged += (s, e) => UpdatePreview();

            rbHttps = new RadioButton
            {
                Text = "HTTPS（https://host/owner/repo.git）",
                Location = new Point(14, 54),
                Size = new Size(320, 24),
            };
            rbHttps.CheckedChanged += (s, e) => UpdatePreview();

            lblPreview = new Label
            {
                Location = new Point(14, 92),
                Size = new Size(620, 46),
                Font = new Font("Consolas", 9F),
                Text = "",
            };

            btnCopy = new Button
            {
                Text = "复制",
                Location = new Point(646, 92),
                Size = new Size(70, 26),
            };
            btnCopy.Click += (s, e) =>
            {
                if (lblPreview.Text.Length > 0) Clipboard.SetText(lblPreview.Text);
            };

            var lblTip = new Label
            {
                Location = new Point(14, 146),
                Size = new Size(700, 40),
                ForeColor = Color.FromArgb(110, 110, 110),
                Text = "说明：改的是 git 的远程地址，不会动本地文件和历史。HTTPS 推送时需要账号/令牌；SSH 需要本机已配置密钥。",
            };

            box.Controls.AddRange(new Control[] { rbSsh, rbHttps, lblPreview, btnCopy, lblTip });

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52 };

            btnApply = new Button { Text = "应用新地址", Location = new Point(10, 12), Size = new Size(120, 30) };
            btnApply.Click += (s, e) => ApplyUrl();

            btnAdd = new Button { Text = "添加远程...", Location = new Point(140, 12), Size = new Size(110, 30) };
            btnAdd.Click += (s, e) => AddRemote();

            btnClose = new Button
            {
                Text = "关闭",
                Location = new Point(710, 12),
                Size = new Size(100, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnClose.Click += (s, e) => Close();

            bottom.Controls.AddRange(new Control[] { btnApply, btnAdd, btnClose });

            Controls.Add(box);
            Controls.Add(listRemotes);
            Controls.Add(lblHint);
            Controls.Add(bottom);
        }

        private void LoadRemotes()
        {
            _remotes.Clear();
            listRemotes.Items.Clear();

            var r = GitRunner.RunUi("remote -v");
            if (!r.Success)
            {
                lblPreview.Text = "读取远程失败：" + r.StdErr.Trim();
                return;
            }

            using (var reader = new StringReader(r.StdOut))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0) continue;
                    var parts = line.Split('\t');
                    if (parts.Length < 2) continue;

                    var name = parts[0].Trim();
                    var rest = parts[1].Trim();
                    // 只取 fetch 的那一行，push 行地址可能与 fetch 不同
                    if (!rest.EndsWith("(fetch)")) continue;
                    var url = rest.Substring(0, rest.Length - "(fetch)".Length).Trim();

                    var entry = new RemoteEntry { Name = name, Url = url };
                    _remotes.Add(entry);

                    var item = new ListViewItem(name);
                    item.SubItems.Add(url);
                    item.Tag = entry;

                    var protocol = RemoteUrl.Detect(url);
                    if (protocol == RemoteProtocol.Ssh) item.ForeColor = Color.FromArgb(20, 110, 40);
                    else if (protocol == RemoteProtocol.Https) item.ForeColor = Color.DarkBlue;
                    else item.ForeColor = Color.Firebrick;

                    listRemotes.Items.Add(item);
                }
            }

            if (_remotes.Count == 0)
            {
                listRemotes.Enabled = false;
                rbSsh.Enabled = false;
                rbHttps.Enabled = false;
                btnApply.Enabled = false;
                btnCopy.Enabled = false;
                lblPreview.Text = "当前仓库还没有远程。点「添加远程...」先加一个 origin。";
                return;
            }

            listRemotes.Enabled = true;
            rbSsh.Enabled = true;
            rbHttps.Enabled = true;
            btnApply.Enabled = true;
            btnCopy.Enabled = true;
            listRemotes.Items[0].Selected = true;
        }

        private RemoteEntry Selected()
        {
            if (listRemotes.SelectedItems.Count == 0) return null;
            return listRemotes.SelectedItems[0].Tag as RemoteEntry;
        }

        /// <summary>
        /// 切换选中项时：识别当前写法，把单选框默认落在"另一种"上，少点一次。
        /// 单选框的点击只刷新预览，不会反过来把用户的选择改掉。
        /// </summary>
        private void OnRemoteSelected()
        {
            var entry = Selected();
            if (entry == null) { lblPreview.Text = ""; return; }

            var current = RemoteUrl.Detect(entry.Url);
            if (current == RemoteProtocol.Ssh) rbHttps.Checked = true;
            else if (current == RemoteProtocol.Https) rbSsh.Checked = true;

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var entry = Selected();
            if (entry == null) { lblPreview.Text = ""; return; }

            var current = RemoteUrl.Detect(entry.Url);
            if (current == RemoteProtocol.Unknown)
            {
                lblPreview.Text = "当前地址无法识别成 SSH 或 HTTPS：" + entry.Url;
                return;
            }

            var target = rbSsh.Checked ? RemoteProtocol.Ssh : RemoteProtocol.Https;
            ShowPreview(entry, current, target);
        }

        private void ShowPreview(RemoteEntry entry, RemoteProtocol current, RemoteProtocol target)
        {
            string result;
            string error;
            var ok = target == RemoteProtocol.Ssh
                ? RemoteUrl.TryToSsh(entry.Url, out result, out error)
                : RemoteUrl.TryToHttps(entry.Url, out result, out error);

            if (!ok)
            {
                lblPreview.Text = "无法转换：" + error;
                return;
            }
            if (target == current)
            {
                lblPreview.Text = "当前已经是 " + ProtocolName(current)
                    + " 写法，无需转换。\n" + result;
                btnApply.Enabled = false;
                return;
            }
            btnApply.Enabled = true;
            lblPreview.Text = "当前 " + ProtocolName(current)
                + " → 切换后：" + result;
        }

        private static string ProtocolName(RemoteProtocol protocol)
        {
            if (protocol == RemoteProtocol.Ssh) return "SSH";
            if (protocol == RemoteProtocol.Https) return "HTTPS";
            return "未知";
        }

        private void ApplyUrl()
        {
            var entry = Selected();
            if (entry == null) { MessageBox.Show(this, "请先选择一个远程。"); return; }

            var target = rbSsh.Checked ? RemoteProtocol.Ssh : RemoteProtocol.Https;
            if (RemoteUrl.Detect(entry.Url) == target)
            {
                MessageBox.Show(this, "这个远程已经是 " + ProtocolName(target) + " 写法了。",
                    "无需修改", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string result;
            string error;
            var ok = target == RemoteProtocol.Ssh
                ? RemoteUrl.TryToSsh(entry.Url, out result, out error)
                : RemoteUrl.TryToHttps(entry.Url, out result, out error);

            if (!ok)
            {
                MessageBox.Show(this, "无法转换：" + error, "转换失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var answer = MessageBox.Show(this,
                "把远程「" + entry.Name + "」改成：\n\n" + result + "\n\n原来的地址：" + entry.Url,
                "确认修改远程地址", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            var r = GitRunner.Run("remote set-url " + GitRunner.Quote(entry.Name) + " " + GitRunner.Quote(result));
            if (!r.Success)
            {
                MessageBox.Show(this, (r.StdErr + r.StdOut).Trim(), "git remote set-url 失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Changed = true;
            LoadRemotes();
            MessageBox.Show(this, "已修改为：\n" + result, "完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AddRemote()
        {
            var name = Form1.Prompt(this, "添加远程", "远程名（一般用 origin）:", "origin");
            if (name == null || name.Trim().Length == 0) return;

            var url = Form1.Prompt(this, "添加远程", "远程地址（https:// 或 git@host:owner/repo.git）:", "");
            if (url == null || url.Trim().Length == 0) return;

            var r = GitRunner.Run("remote add " + GitRunner.Quote(name.Trim()) + " " + GitRunner.Quote(url.Trim()));
            if (!r.Success)
            {
                MessageBox.Show(this, (r.StdErr + r.StdOut).Trim(), "git remote add 失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Changed = true;
            LoadRemotes();
            if (listRemotes.Items.Count > 0) listRemotes.Items[0].Selected = true;
        }
    }
}
